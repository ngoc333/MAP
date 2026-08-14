Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$root = Split-Path -Parent $PSScriptRoot
$requiredSdkFiles = @(
    'MAP.C.Contract.dll', 'MAP.C.Contract.xml',
    'MAP.C.UI.dll', 'MAP.C.UI.xml',
    'MAP.C.Runtime.dll', 'MAP.C.Wasm.dll', 'MAP.C.Wpf.dll'
)

foreach ($file in $requiredSdkFiles) {
    $path = Join-Path $root "Sdk\lib\$file"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required SDK binary is missing: $path"
    }

    Write-Host "$file  $((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)"
}

$moduleSdkPropsPath = Join-Path $root 'Sdk\MAP.ModuleSdk.props'
if (-not (Test-Path -LiteralPath $moduleSdkPropsPath -PathType Leaf)) {
    throw "Required SDK props file is missing: $moduleSdkPropsPath"
}

$modulesPath = Join-Path $root 'Modules'
$moduleProjects = @(
    Get-ChildItem -LiteralPath $modulesPath -Recurse -Filter '*.csproj' -File |
        Sort-Object FullName
)
if ($moduleProjects.Count -eq 0) {
    throw "No module projects were found under: $modulesPath"
}

Push-Location $root
try {
    Invoke-DotNet -Arguments @('restore', 'DevKit.slnx') -Description 'DevKit restore'

    foreach ($moduleProject in $moduleProjects) {
        Invoke-DotNet -Arguments @('build', $moduleProject.FullName, '-c', 'Release', '--no-restore') -Description "Module build: $($moduleProject.BaseName)"
    }

    Invoke-DotNet -Arguments @('build', 'MAP.H.Desktop/MAP.H.Desktop.csproj', '-c', 'Release', '--no-restore') -Description 'Desktop build'
    Invoke-DotNet -Arguments @('build', 'MAP.H.Web/MAP.H.Web.csproj', '-c', 'Release', '--no-restore') -Description 'Web build'
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "Modules discovered : $($moduleProjects.Count)"
Write-Host 'Desktop build       : OK'
Write-Host 'Web build           : OK'
