Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-WebDeployContentPath {
    param([Parameter(Mandatory)][string]$WebArtifactPath)

    foreach ($candidate in @($WebArtifactPath, (Join-Path $WebArtifactPath 'wwwroot'))) {
        $entryPoint = Join-Path $candidate 'index.html'
        $frameworkPath = Join-Path $candidate '_framework'
        $blazorBootstrap = if (Test-Path -LiteralPath $frameworkPath -PathType Container) {
            Get-ChildItem -LiteralPath $frameworkPath -Filter 'blazor.webassembly*.js' -File | Select-Object -First 1
        }
        if ((Test-Path -LiteralPath $entryPoint -PathType Leaf) -and $null -ne $blazorBootstrap) {
            return $candidate
        }
    }
    throw 'Web artifact is invalid: the Blazor WASM entry point was not found.'
}

function Test-ReleaseArtifacts {
    param(
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath
    )

    $desktopExecutable = Join-Path $DesktopArtifactPath 'MAP.H.Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktopExecutable -PathType Leaf)) {
        throw "Desktop artifact is invalid: MAP.H.Desktop.exe was not found."
    }

    $null = Get-WebDeployContentPath -WebArtifactPath $WebArtifactPath
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

    $solution = Join-Path $RepositoryRoot 'MAP.slnx'
    Write-Step '[2/6] Build & test'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $solution, '--nologo') -Description 'Restore'

    # Build release targets through their project-reference graphs. Run-App is intentionally not built.
    foreach ($project in @('MAP.H.Desktop\MAP.H.Desktop.csproj', 'MAP.H.Web\MAP.H.Web.csproj')) {
        Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('build', (Join-Path $RepositoryRoot $project), '-c', 'Release', '--no-restore', '--nologo') -Description "Build $project"
    }
    $testProject = Join-Path $RepositoryRoot 'Tests\MAP.C.Runtime.Tests\MAP.C.Runtime.Tests.csproj'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('test', $testProject, '-c', 'Release', '--no-restore', '--nologo') -Description 'Test'
    Write-Success '[2/6] Build & test ................. OK'

    Write-Step '[3/6] Publish'
    $desktopProject = Join-Path $RepositoryRoot 'MAP.H.Desktop\MAP.H.Desktop.csproj'
    # The Desktop project builds discovered modules that are not project references; restore them for this RID.
    foreach ($runtimeProject in @($desktopProject) + @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'Modules') -Recurse -Filter '*.csproj' -File | ForEach-Object FullName)) {
        Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('restore', $runtimeProject, '-r', $DesktopRuntime, '--nologo') -Description 'Desktop runtime restore'
    }
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('publish', $desktopProject, '-c', 'Release', '-r', $DesktopRuntime, '--self-contained', 'true', '--no-restore', '-o', $DesktopArtifactPath, '--nologo') -Description 'Desktop publish'

    $webProject = Join-Path $RepositoryRoot 'MAP.H.Web\MAP.H.Web.csproj'
    Invoke-ExternalCommand -FilePath 'dotnet' -ArgumentList @('publish', $webProject, '-c', 'Release', '--no-restore', '-o', $WebArtifactPath, '--nologo') -Description 'Web publish'

    Test-ReleaseArtifacts -DesktopArtifactPath $DesktopArtifactPath -WebArtifactPath $WebArtifactPath
    Write-Success '  Desktop .......................... OK'
    Write-Success '  Web .............................. OK'
}
