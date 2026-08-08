[CmdletBinding()]
param(
    [switch]$PublishOnly,
    [switch]$DeployOnly,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PublishOnly -and $DeployOnly) { throw '-PublishOnly and -DeployOnly cannot be used together.' }

$repositoryRoot = $PSScriptRoot
. (Join-Path $repositoryRoot 'scripts\common.ps1')
. (Join-Path $repositoryRoot 'scripts\publish.ps1')
. (Join-Path $repositoryRoot 'scripts\deploy-desktop.ps1')
. (Join-Path $repositoryRoot 'scripts\deploy-web.ps1')

$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$desktopArtifact = Join-Path $artifactsRoot 'desktop'
$webArtifact = Join-Path $artifactsRoot 'web'
$metadataPath = Join-Path $artifactsRoot 'release.json'
$releaseId = $null
$lockPath = $null
$lockAcquired = $false

function Get-GitReleaseInfo {
    param([switch]$AllowDirtySource)

    $commit = (& git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { throw 'Unable to determine the current Git commit.' }
    $changes = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to validate the Git working tree.' }
    $isDirty = $changes.Count -gt 0
    if ($isDirty -and -not $AllowDirtySource) {
        throw 'Working tree contains uncommitted changes. Commit the changes or use -AllowDirty.'
    }
    return [pscustomobject]@{ Commit = $commit; IsDirty = $isDirty }
}

function Write-ReleaseMetadata {
    param([Parameter(Mandatory)][hashtable]$Metadata)
    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
    $Metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
}

function Get-ArtifactFingerprint {
    $webContentPath = Get-WebDeployContentPath -WebArtifactPath $webArtifact
    return [ordered]@{
        desktopSize = Get-DirectorySize -Path $desktopArtifact
        desktopExecutableSha256 = Get-FileSha256 -Path (Join-Path $desktopArtifact 'MAP.H.Desktop.exe')
        webSize = Get-DirectorySize -Path $webContentPath
        webIndexSha256 = Get-FileSha256 -Path (Join-Path $webContentPath 'index.html')
    }
}

function Read-DeployOnlyMetadata {
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { throw 'DeployOnly requires artifacts/release.json.' }
    Test-ReleaseArtifacts -DesktopArtifactPath $desktopArtifact -WebArtifactPath $webArtifact
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($metadata.release) -or [string]::IsNullOrWhiteSpace($metadata.startedUtc)) {
        throw 'DeployOnly artifacts/release.json is invalid.'
    }
    if ($null -ne $metadata.artifacts) {
        $fingerprint = Get-ArtifactFingerprint
        if ($metadata.artifacts.desktopExecutableSha256 -ne $fingerprint.desktopExecutableSha256 -or
            $metadata.artifacts.webIndexSha256 -ne $fingerprint.webIndexSha256) {
            throw 'DeployOnly artifacts do not match artifacts/release.json.'
        }
    }
    return $metadata
}

function Enter-ReleaseLock {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][hashtable]$Content)
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes(($Content | ConvertTo-Json -Compress))
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally { $stream.Dispose() }
    }
    catch [System.IO.IOException] { throw 'Another MAP release is already in progress.' }
}

function Get-SafeErrorMessage {
    param([Parameter(Mandatory)]$ErrorRecord)
    $message = $ErrorRecord.Exception.Message
    foreach ($name in @('MAP_WEB_DEPLOY_PASSWORD', 'MAP_WEB_IIS_PASSWORD')) {
        $secret = Get-OptionalSetting -Name $name
        if (-not [string]::IsNullOrEmpty($secret)) { $message = $message.Replace($secret, '***') }
    }
    return $message
}

try {
    Write-Host '===================================================='
    Write-Host ' MAP RELEASE'
    Write-Host '===================================================='

    if ($DeployOnly) {
        $metadata = Read-DeployOnlyMetadata
        $releaseId = $metadata.release
        $commit = $metadata.commit
        $releaseTimestampUtc = [datetime]::Parse($metadata.startedUtc).ToUniversalTime()
    }
    else {
        Write-Step '[1/6] Validate source'
        $git = Get-GitReleaseInfo -AllowDirtySource:$AllowDirty
        $commit = $git.Commit
        $releaseTimestampUtc = [datetime]::UtcNow
        $releaseId = '{0}-{1}' -f (Get-Date).ToString('yyyyMMdd-HHmm'), $commit
        if ($git.IsDirty) { $releaseId += '-dirty' }
        Write-Success '[1/6] Validate source .............. OK'
    }

    Write-Host "Release : $releaseId"
    Write-Host "Commit  : $commit"
    Write-Host 'Source  : Local'

    if (-not $DeployOnly) {
        $runtime = Get-OptionalSetting -Name 'MAP_DESKTOP_RUNTIME'
        if ([string]::IsNullOrWhiteSpace($runtime)) { $runtime = 'win-x64' }
        Invoke-ReleasePublish -RepositoryRoot $repositoryRoot -DesktopArtifactPath $desktopArtifact -WebArtifactPath $webArtifact -DesktopRuntime $runtime
        $metadata = [ordered]@{
            release = $releaseId; commit = $commit; source = 'local'; machine = $env:COMPUTERNAME
            user = $env:USERNAME; startedUtc = $releaseTimestampUtc.ToString('o'); status = 'published'
            completedUtc = $null; desktopNew = $null; desktopChanged = $null; desktopUnchanged = $null
            artifacts = Get-ArtifactFingerprint
        }
        Write-ReleaseMetadata -Metadata $metadata
    }

    if ($PublishOnly) {
        Write-Success 'Publish-only release completed successfully.'
        exit 0
    }

    Write-Step '[4/6] Preflight'
    Test-ReleaseDeploymentPreflight -DesktopArtifactPath $desktopArtifact -WebArtifactPath $webArtifact
    Write-Success '[4/6] Preflight .................... OK'

    $deploymentRoot = Get-RequiredSetting -Name 'MAP_DESKTOP_DEPLOY_PATH'
    $lockPath = Join-Path $deploymentRoot '.release.lock'
    $lockContent = [ordered]@{ release = $releaseId; machine = $env:COMPUTERNAME; user = $env:USERNAME; startedUtc = $releaseTimestampUtc.ToString('o') }
    Enter-ReleaseLock -Path $lockPath -Content $lockContent
    $lockAcquired = $true

    Write-Step '[5/6] Deploy Web'
    Invoke-WebDeployment -WebArtifactPath $webArtifact
    Write-Success '  Web Deploy ....................... OK'

    Write-Step '[6/6] Deploy Desktop'
    $desktopResult = Invoke-DesktopDeployment -SourcePath $desktopArtifact -DeploymentRoot $deploymentRoot -ReleaseTimestampUtc $releaseTimestampUtc
    $metadata.status = 'success'
    $metadata.completedUtc = [datetime]::UtcNow.ToString('o')
    $metadata.desktopNew = $desktopResult.New
    $metadata.desktopChanged = $desktopResult.Changed
    $metadata.desktopUnchanged = $desktopResult.Unchanged
    Write-ReleaseMetadata -Metadata $metadata
    Copy-Item -LiteralPath $metadataPath -Destination (Join-Path $deploymentRoot 'release.json') -Force

    Write-Host '===================================================='
    Write-Success ' RELEASE SUCCESS'
    Write-Host " $releaseId"
    Write-Host '===================================================='
}
catch {
    Write-Host '====================================================' -ForegroundColor Red
    Write-Failure ' RELEASE FAILED'
    Write-Host '====================================================' -ForegroundColor Red
    Write-Host "Phase   : release"
    Write-Host "Release : $releaseId"
    Write-Failure "Reason  : $(Get-SafeErrorMessage -ErrorRecord $_)"
    exit 1
}
finally {
    if ($lockAcquired -and $lockPath -and (Test-Path -LiteralPath $lockPath)) {
        Remove-Item -LiteralPath $lockPath -Force
    }
}
