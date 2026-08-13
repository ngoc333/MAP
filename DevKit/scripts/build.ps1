Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$requiredSdkFiles = @(
    'MAP.C.Contract.dll', 'MAP.C.Contract.xml',
    'MAP.C.UI.dll', 'MAP.C.UI.xml',
    'MAP.C.Runtime.dll', 'MAP.C.Wasm.dll', 'MAP.C.Wpf.dll'
)
foreach ($file in $requiredSdkFiles) {
    $path = Join-Path $root "Sdk\lib\$file"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required SDK binary is missing: $path" }
    Write-Host "$file  $((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)"
}

Push-Location $root
try {
    dotnet restore DevKit.slnx
    dotnet build Modules/MAP.M.Template/MAP.M.Template.csproj -c Release --no-restore
    dotnet build MAP.H.Desktop/MAP.H.Desktop.csproj -c Release --no-restore
    dotnet build MAP.H.Web/MAP.H.Web.csproj -c Release --no-restore
}
finally { Pop-Location }
