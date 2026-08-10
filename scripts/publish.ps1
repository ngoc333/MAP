Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-WebContentDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $indexPath = Join-Path $Path 'index.html'
    $frameworkPath = Join-Path $Path '_framework'
    return ((Test-Path -LiteralPath $indexPath -PathType Leaf) -and
        (Test-Path -LiteralPath $frameworkPath -PathType Container))
}

function Get-WebDeployContentPath {
    param([Parameter(Mandatory)][string]$WebArtifactPath)

    if (-not (Test-Path -LiteralPath $WebArtifactPath -PathType Container)) {
        throw "Web artifact directory was not found: $WebArtifactPath"
    }

    $nestedContentPath = Join-Path $WebArtifactPath 'wwwroot'

    # A Blazor publish can be either the content directory itself or an IIS
    # wrapper containing web.config and a wwwroot directory. Preserve the
    # wrapper when its rewrite configuration is part of the deployable site.
    if (Test-WebContentDirectory -Path $WebArtifactPath) {
        return (Resolve-Path -LiteralPath $WebArtifactPath).Path
    }

    if (Test-WebContentDirectory -Path $nestedContentPath) {
        $webConfigPath = Join-Path $WebArtifactPath 'web.config'
        if (Test-Path -LiteralPath $webConfigPath -PathType Leaf) {
            return (Resolve-Path -LiteralPath $WebArtifactPath).Path
        }

        return (Resolve-Path -LiteralPath $nestedContentPath).Path
    }

    throw 'Web artifact is invalid: expected index.html and _framework in the publish content.'
}

function Test-ReleaseArtifacts {
    param(
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath
    )

    if (-not (Test-Path -LiteralPath $DesktopArtifactPath -PathType Container)) {
        throw "Desktop artifact directory was not found: $DesktopArtifactPath"
    }

    $desktopExecutable = Join-Path $DesktopArtifactPath 'MAP.H.Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktopExecutable -PathType Leaf)) {
        throw 'Desktop artifact is invalid: MAP.H.Desktop.exe was not found.'
    }

    $webContentPath = Get-WebDeployContentPath -WebArtifactPath $WebArtifactPath
    $contentRoot = $webContentPath
    if (-not (Test-Path -LiteralPath (Join-Path $contentRoot 'index.html') -PathType Leaf)) {
        $contentRoot = Join-Path $webContentPath 'wwwroot'
    }

    foreach ($requiredRelativePath in @('index.html', '_framework')) {
        $requiredPath = Join-Path $contentRoot $requiredRelativePath
        $requiredType = 'Leaf'
        if ($requiredRelativePath -eq '_framework') {
            $requiredType = 'Container'
        }
        if (-not (Test-Path -LiteralPath $requiredPath -PathType $requiredType)) {
            throw "Web artifact is invalid: '$requiredRelativePath' was not found."
        }
    }

    # These files are part of MAP.H.Web.csproj and must be present in a
    # complete standalone Web artifact.
    foreach ($mapFile in @('page.json', 'db-api.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $contentRoot $mapFile) -PathType Leaf)) {
            throw "Web artifact is invalid: '$mapFile' was not found."
        }
    }
}

function Get-ArtifactFingerprint {
    param(
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath
    )

    $webContentPath = Get-WebDeployContentPath -WebArtifactPath $WebArtifactPath
    $webIndexPath = $webContentPath
    if (-not (Test-Path -LiteralPath (Join-Path $webContentPath 'index.html') -PathType Leaf)) {
        $webIndexPath = Join-Path $webContentPath 'wwwroot'
    }

    return [ordered]@{
        desktopSize = Get-DirectorySize -Path $DesktopArtifactPath
        desktopExecutableSha256 = Get-FileSha256 -Path (Join-Path $DesktopArtifactPath 'MAP.H.Desktop.exe')
        webSize = Get-DirectorySize -Path $webContentPath
        webIndexSha256 = Get-FileSha256 -Path (Join-Path $webIndexPath 'index.html')
    }
}

function Invoke-ReleasePublish {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath,
        [Parameter(Mandatory)][string]$DesktopRuntime
    )

    foreach ($artifactPath in @($DesktopArtifactPath, $WebArtifactPath)) {
        if (Test-Path -LiteralPath $artifactPath) {
            Remove-Item -LiteralPath $artifactPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
    }

    $desktopProject = Join-Path $RepositoryRoot 'MAP.H.Desktop\MAP.H.Desktop.csproj'
    $webProject = Join-Path $RepositoryRoot 'MAP.H.Web\MAP.H.Web.csproj'
    $testProject = Join-Path $RepositoryRoot 'Tests\MAP.C.Runtime.Tests\MAP.C.Runtime.Tests.csproj'

    Write-Step '[2/6] Build & test'

    # Restore only the release/test project graphs. The separate client
    # bootstrapper project is intentionally not part of this pipeline. Restore Web and Desktop
    # first, then restore discovered Desktop modules with the release RID;
    # restoring Web after that would overwrite their RID-specific assets.
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $desktopProject, '-r', $DesktopRuntime, '--nologo') -Description 'Desktop restore'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $webProject, '--nologo') -Description 'Web restore'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $testProject, '--nologo') -Description 'Test restore'

    $moduleProjects = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Modules') -Recurse -Filter '*.csproj' -File | Sort-Object FullName)
    foreach ($moduleProject in $moduleProjects) {
        Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $moduleProject.FullName, '-r', $DesktopRuntime, '--nologo') -Description "Restore $($moduleProject.Name)"
    }

    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('build', $desktopProject, '-c', 'Release', '--no-restore', '--nologo') -Description 'Desktop build'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('build', $webProject, '-c', 'Release', '--no-restore', '--nologo') -Description 'Web build'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('test', $testProject, '-c', 'Release', '--no-restore', '--nologo') -Description 'Test'
    Write-Success '[2/6] Build & test ................. OK'

    Write-Step '[3/6] Publish'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @(
        'publish', $desktopProject, '-c', 'Release', '-r', $DesktopRuntime,
        '--self-contained', 'true', '--no-restore', '-o', $DesktopArtifactPath, '--nologo') -Description 'Desktop publish'

    # Only the standalone Blazor WebAssembly project is published. The Web
    # host is deliberately not a release artifact.
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @(
        'publish', $webProject, '-c', 'Release', '--no-restore', '-o', $WebArtifactPath, '--nologo') -Description 'Web publish'

    Test-ReleaseArtifacts -DesktopArtifactPath $DesktopArtifactPath -WebArtifactPath $WebArtifactPath
    Write-Success '  Desktop .......................... OK'
    Write-Success '  Web .............................. OK'
}
