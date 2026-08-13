[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DestinationModulesPath,
    [string]$Configuration = 'Release'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "Modules\MAP.M.Template\bin\$Configuration\net10.0\MAP.M.Template.dll"
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Build the template module first: $source" }
New-Item -ItemType Directory -Path $DestinationModulesPath -Force | Out-Null
$destination = Join-Path $DestinationModulesPath 'MAP.M.Template.dll'
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if (Test-Path -LiteralPath $destination -PathType Leaf) {
    $destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceHash -eq $destinationHash) {
        Write-Host "UNCHANGED MAP.M.Template.dll (destination: $((Get-Item $destination).LastWriteTimeUtc.ToString('O')))"
        return
    }
}
Copy-Item -LiteralPath $source -Destination $destination -Force
(Get-Item -LiteralPath $destination).LastWriteTimeUtc = [datetime]::UtcNow
Write-Host "DEPLOYED MAP.M.Template.dll SHA256=$sourceHash"
