Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:EnvironmentFile = Join-Path $script:RepositoryRoot '.env'

function Get-SettingFromEnvironmentFile {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Test-Path -LiteralPath $script:EnvironmentFile -PathType Leaf)) { return $null }

    foreach ($line in Get-Content -LiteralPath $script:EnvironmentFile) {
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) { continue }
        if ($trimmedLine -match ('^{0}\s*=\s*(.*)$' -f [regex]::Escape($Name))) {
            $value = $Matches[1].Trim().Trim('"').Trim("'")
            if ($value -and $value -ne 'your_value_here') { return $value }
        }
    }

    return $null
}

function Get-OptionalSetting {
    param([Parameter(Mandatory)][string]$Name)

    $environmentValue = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) { return $environmentValue }
    return Get-SettingFromEnvironmentFile -Name $Name
}

function Get-RequiredSetting {
    param([Parameter(Mandatory)][string]$Name)

    $value = Get-OptionalSetting -Name $Name
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required configuration setting '$Name' is not set. Set it as an environment variable or in .env."
    }

    return $value
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory)][string]$Description,
        [string[]]$SensitiveValues = @()
    )

    $output = & $FilePath @ArgumentList 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in $output) {
        $safeLine = [string]$line
        foreach ($sensitiveValue in $SensitiveValues) {
            if (-not [string]::IsNullOrEmpty($sensitiveValue)) {
                $safeLine = $safeLine.Replace($sensitiveValue, '***')
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($safeLine)) { Write-Host $safeLine }
    }

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode."
    }
}

function Get-DirectorySize {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-ChildItem -LiteralPath $Path -File -Recurse | Measure-Object -Property Length -Sum).Sum
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Write-Step { param([Parameter(Mandatory)][string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Write-Success { param([Parameter(Mandatory)][string]$Message) Write-Host $Message -ForegroundColor Green }
function Write-Warning { param([Parameter(Mandatory)][string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Write-Failure { param([Parameter(Mandatory)][string]$Message) Write-Host $Message -ForegroundColor Red }
