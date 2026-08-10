Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:EnvironmentFile = Join-Path $script:RepositoryRoot '.env'

function Get-SettingFromEnvironmentFile {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Test-Path -LiteralPath $script:EnvironmentFile -PathType Leaf)) {
        return $null
    }

    foreach ($line in Get-Content -LiteralPath $script:EnvironmentFile) {
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
            continue
        }

        if ($trimmedLine -match ('^{0}\s*=\s*(.*)$' -f [regex]::Escape($Name))) {
            $value = $Matches[1].Trim()
            if (($value.Length -ge 2) -and
                (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            if (-not [string]::IsNullOrWhiteSpace($value) -and $value -ne 'your_value_here') {
                return $value
            }
        }
    }

    return $null
}

function Get-OptionalSetting {
    param([Parameter(Mandatory)][string]$Name)

    $environmentValue = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
        return $environmentValue
    }

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

        if (-not [string]::IsNullOrWhiteSpace($safeLine)) {
            Write-Host $safeLine
        }
    }

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode."
    }
}

function Invoke-ExternalProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string]$Arguments,
        [Parameter(Mandatory)][string]$Description,
        [string[]]$SensitiveValues = @()
    )

    $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processStartInfo.FileName = $FilePath
    $processStartInfo.Arguments = $Arguments
    $processStartInfo.UseShellExecute = $false
    $processStartInfo.CreateNoWindow = $true
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $processStartInfo
    $standardOutput = $null
    $standardError = $null
    $exitCode = $null
    try {
        $process.Start() | Out-Null
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    foreach ($output in @($standardOutput, $standardError)) {
        if ([string]::IsNullOrEmpty($output)) {
            continue
        }

        $safeOutput = $output
        foreach ($sensitiveValue in $SensitiveValues) {
            if (-not [string]::IsNullOrEmpty($sensitiveValue)) {
                $safeOutput = $safeOutput.Replace($sensitiveValue, '***')
            }
        }
        Write-Host $safeOutput.TrimEnd()
    }

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode."
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-DirectorySize {
    param([Parameter(Mandatory)][string]$Path)

    $files = @(Get-ChildItem -LiteralPath $Path -File -Recurse)
    if ($files.Count -eq 0) {
        return [int64]0
    }

    return [int64](($files | Measure-Object -Property Length -Sum).Sum)
}

function Get-GitReleaseInfo {
    param([switch]$AllowDirtySource)

    $commit = (& git rev-parse --short HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw 'Unable to determine the current Git commit.'
    }

    $changes = @(& git status --porcelain 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to validate the Git working tree.'
    }

    $isDirty = $changes.Count -gt 0
    if ($isDirty -and -not $AllowDirtySource) {
        throw 'Working tree contains uncommitted changes. Commit the changes or use -AllowDirty.'
    }

    return [pscustomobject]@{
        Commit = $commit
        IsDirty = $isDirty
    }
}

function Write-ReleaseMetadata {
    param(
        [Parameter(Mandatory)][hashtable]$Metadata,
        [Parameter(Mandatory)][string]$Path
    )

    $parentDirectory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
    $Metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Enter-ReleaseLock {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Content
    )

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes(($Content | ConvertTo-Json -Compress))
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
    catch [System.IO.IOException] {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            throw 'Another MAP release is already in progress.'
        }
        throw
    }
}

function Get-SafeErrorMessage {
    param(
        [Parameter(Mandatory)]$ErrorRecord,
        [string[]]$SensitiveValues = @()
    )

    $message = $ErrorRecord.Exception.Message
    foreach ($sensitiveValue in $SensitiveValues) {
        if (-not [string]::IsNullOrEmpty($sensitiveValue)) {
            $message = $message.Replace($sensitiveValue, '***')
        }
    }

    return $message
}

function Get-MsDeployPath {
    return 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
}

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Success {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-ReleaseWarning {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Write-Failure {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host $Message -ForegroundColor Red
}
