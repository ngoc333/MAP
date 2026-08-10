[CmdletBinding()]
param(
    [switch]$PublishOnly,
    [switch]$DeployOnly,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PublishOnly -and $DeployOnly) {
    throw '-PublishOnly and -DeployOnly cannot be used together.'
}

$repositoryRoot = $PSScriptRoot
. (Join-Path $repositoryRoot 'scripts\common.ps1')
. (Join-Path $repositoryRoot 'scripts\publish.ps1')
. (Join-Path $repositoryRoot 'scripts\deploy-desktop.ps1')
. (Join-Path $repositoryRoot 'scripts\deploy-web.ps1')

$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$desktopArtifactPath = Join-Path $artifactsRoot 'desktop'
$webArtifactPath = Join-Path $artifactsRoot 'web'
$metadataPath = Join-Path $artifactsRoot 'release.json'
$releaseId = $null
$commit = $null
$releaseTimestampUtc = $null
$metadata = $null
$deploymentRoot = $null
$lockPath = $null
$lockAcquired = $false
$phase = 'initialization'

function Get-PreparedReleaseMetadata {
    if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
        throw 'DeployOnly requires artifacts/release.json.'
    }

    $storedMetadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    foreach ($propertyName in @('release', 'commit', 'startedUtc')) {
        $property = $storedMetadata.PSObject.Properties[$propertyName]
        if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw 'DeployOnly artifacts/release.json is invalid.'
        }
    }

    Test-ReleaseArtifacts -DesktopArtifactPath $desktopArtifactPath -WebArtifactPath $webArtifactPath
    $artifactsProperty = $storedMetadata.PSObject.Properties['artifacts']
    $storedArtifacts = $null
    if ($null -ne $artifactsProperty) {
        $storedArtifacts = $artifactsProperty.Value
    }
    if ($null -ne $storedArtifacts) {
        $fingerprint = Get-ArtifactFingerprint `
            -DesktopArtifactPath $desktopArtifactPath `
            -WebArtifactPath $webArtifactPath
        if ([string]$storedArtifacts.desktopExecutableSha256 -ne [string]$fingerprint.desktopExecutableSha256 -or
            [string]$storedArtifacts.webIndexSha256 -ne [string]$fingerprint.webIndexSha256) {
            throw 'DeployOnly artifacts do not match artifacts/release.json.'
        }
    }

    try {
        $startedUtc = ([datetime]::Parse([string]$storedMetadata.startedUtc)).ToUniversalTime()
    }
    catch {
        throw 'DeployOnly artifacts/release.json contains an invalid startedUtc value.'
    }

    $source = 'local'
    $sourceProperty = $storedMetadata.PSObject.Properties['source']
    if ($null -ne $sourceProperty -and -not [string]::IsNullOrWhiteSpace([string]$sourceProperty.Value)) {
        $source = [string]$sourceProperty.Value
    }
    $machine = [Environment]::MachineName
    $machineProperty = $storedMetadata.PSObject.Properties['machine']
    if ($null -ne $machineProperty -and -not [string]::IsNullOrWhiteSpace([string]$machineProperty.Value)) {
        $machine = [string]$machineProperty.Value
    }
    $user = [Environment]::UserName
    $userProperty = $storedMetadata.PSObject.Properties['user']
    if ($null -ne $userProperty -and -not [string]::IsNullOrWhiteSpace([string]$userProperty.Value)) {
        $user = [string]$userProperty.Value
    }
    if ($null -eq $storedArtifacts) {
        $storedArtifacts = Get-ArtifactFingerprint `
            -DesktopArtifactPath $desktopArtifactPath `
            -WebArtifactPath $webArtifactPath
    }

    return [ordered]@{
        release = [string]$storedMetadata.release
        commit = [string]$storedMetadata.commit
        source = $source
        machine = $machine
        user = $user
        startedUtc = $startedUtc.ToString('o')
        status = 'prepared'
        artifacts = $storedArtifacts
    }
}

try {
    Write-Host '===================================================='
    Write-Host ' MAP RELEASE'
    Write-Host '===================================================='

    if ($DeployOnly) {
        $phase = 'artifact validation'
        $metadata = Get-PreparedReleaseMetadata
        $releaseId = [string]$metadata.release
        $commit = [string]$metadata.commit
        $releaseTimestampUtc = [datetime]::Parse([string]$metadata.startedUtc).ToUniversalTime()
    }
    else {
        $phase = 'source validation'
        Write-Step '[1/6] Validate source'
        $git = Get-GitReleaseInfo -AllowDirtySource:$AllowDirty
        $commit = $git.Commit
        $releaseTimestampUtc = [datetime]::UtcNow
        $releaseId = '{0}-{1}' -f $releaseTimestampUtc.ToString('yyyyMMdd-HHmm'), $commit
        if ($git.IsDirty) {
            $releaseId = '{0}-dirty' -f $releaseId
        }
        Write-Success '[1/6] Validate source .............. OK'
    }

    Write-Host "Release : $releaseId"
    Write-Host "Commit  : $commit"
    Write-Host 'Source  : Local'

    if (-not $DeployOnly) {
        $phase = 'build, test, and publish'
        $runtime = Get-OptionalSetting -Name 'MAP_DESKTOP_RUNTIME'
        if ([string]::IsNullOrWhiteSpace($runtime)) {
            $runtime = 'win-x64'
        }

        if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
            Remove-Item -LiteralPath $metadataPath -Force
        }
        Invoke-ReleasePublish `
            -RepositoryRoot $repositoryRoot `
            -DesktopArtifactPath $desktopArtifactPath `
            -WebArtifactPath $webArtifactPath `
            -DesktopRuntime $runtime

        $metadata = [ordered]@{
            release = $releaseId
            commit = $commit
            source = 'local'
            machine = [Environment]::MachineName
            user = [Environment]::UserName
            startedUtc = $releaseTimestampUtc.ToString('o')
            status = 'prepared'
            artifacts = Get-ArtifactFingerprint `
                -DesktopArtifactPath $desktopArtifactPath `
                -WebArtifactPath $webArtifactPath
        }
        Write-ReleaseMetadata -Metadata $metadata -Path $metadataPath
    }

    if ($PublishOnly) {
        Write-Success 'Publish-only release completed successfully.'
        exit 0
    }

    $phase = 'deployment preflight'
    Write-Step '[4/6] Preflight'
    Test-ReleaseDeploymentPreflight `
        -DesktopArtifactPath $desktopArtifactPath `
        -WebArtifactPath $webArtifactPath
    Write-Success '[4/6] Preflight .................... OK'

    $deploymentRoot = Get-RequiredSetting -Name 'MAP_DESKTOP_DEPLOY_PATH'
    $lockPath = Join-Path $deploymentRoot '.release.lock'
    $lockContent = [ordered]@{
        release = $releaseId
        machine = [Environment]::MachineName
        user = [Environment]::UserName
        startedUtc = $releaseTimestampUtc.ToString('o')
    }

    $phase = 'acquiring release lock'
    Enter-ReleaseLock -Path $lockPath -Content $lockContent
    $lockAcquired = $true

    $phase = 'Web Deploy'
    Write-Step '[5/6] Deploy Web'
    Invoke-WebDeployment -WebArtifactPath $webArtifactPath
    Write-Success '  Web Deploy ....................... OK'

    $phase = 'Desktop Deploy'
    Write-Step '[6/6] Deploy Desktop'
    $desktopResult = Invoke-DesktopDeployment `
        -SourcePath $desktopArtifactPath `
        -DeploymentRoot $deploymentRoot `
        -ReleaseTimestampUtc $releaseTimestampUtc

    $metadata.status = 'success'
    $metadata.completedUtc = [datetime]::UtcNow.ToString('o')
    $metadata.desktopNew = $desktopResult.New
    $metadata.desktopChanged = $desktopResult.Changed
    $metadata.desktopUnchanged = $desktopResult.Unchanged
    Write-ReleaseMetadata -Metadata $metadata -Path $metadataPath

    Write-Host '===================================================='
    Write-Success ' RELEASE SUCCESS'
    Write-Host " $releaseId"
    Write-Host '===================================================='
}
catch {
    if ($null -ne $metadata) {
        try {
            $metadata.status = 'failed'
            $metadata.completedUtc = [datetime]::UtcNow.ToString('o')
            Write-ReleaseMetadata -Metadata $metadata -Path $metadataPath
        }
        catch {
            Write-ReleaseWarning 'Unable to update artifacts/release.json with the failure status.'
        }
    }

    Write-Host '====================================================' -ForegroundColor Red
    Write-Failure ' RELEASE FAILED'
    Write-Host '====================================================' -ForegroundColor Red
    Write-Host "Phase   : $phase"
    Write-Host "Release : $releaseId"
    Write-Failure "Reason  : $(Get-SafeErrorMessage -ErrorRecord $_ -SensitiveValues @((Get-OptionalSetting -Name 'MAP_WEB_DEPLOY_PASSWORD')))"
    exit 1
}
finally {
    if ($lockAcquired -and $lockPath -and (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        try {
            Remove-Item -LiteralPath $lockPath -Force
        }
        catch {
            Write-ReleaseWarning "Unable to remove the release lock: $lockPath"
        }
    }
}
