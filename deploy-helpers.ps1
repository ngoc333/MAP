# deploy-helpers.ps1
# Shared helper functions for deploy scripts.

function Get-DeployPassword {
    <#
    .SYNOPSIS
        Reads MAP_WEB_DEPLOY_PASSWORD from .env file or environment variable.
    .DESCRIPTION
        Priority: environment variable > .env file.
        Never prints the password value.
    #>

    # 1. Check environment variable first
    $envPwd = [Environment]::GetEnvironmentVariable("MAP_WEB_DEPLOY_PASSWORD", "Process")
    if (-not [string]::IsNullOrWhiteSpace($envPwd)) {
        return $envPwd
    }

    # 2. Read from .env file
    $envFile = Join-Path $PSScriptRoot ".env"
    if (-not (Test-Path $envFile)) {
        Write-Host "ERROR: Password not found." -ForegroundColor Red
        Write-Host "  Option 1: Set environment variable `$env:MAP_WEB_DEPLOY_PASSWORD" -ForegroundColor Yellow
        Write-Host "  Option 2: Create .env file from .env.example with your password" -ForegroundColor Yellow
        exit 1
    }

    $lines = Get-Content $envFile -ErrorAction SilentlyContinue
    foreach ($line in $lines) {
        $line = $line.Trim()
        if ($line.StartsWith("#") -or [string]::IsNullOrWhiteSpace($line)) { continue }

        if ($line -match "^MAP_WEB_DEPLOY_PASSWORD\s*=\s*(.+)$") {
            $passwordValue = $Matches[1].Trim().Trim('"').Trim("'")
            if (-not [string]::IsNullOrWhiteSpace($passwordValue) -and $passwordValue -ne "your_password_here") {
                return $passwordValue
            }
        }
    }

    Write-Host "ERROR: MAP_WEB_DEPLOY_PASSWORD not set in .env file." -ForegroundColor Red
    Write-Host "  Edit .env and set MAP_WEB_DEPLOY_PASSWORD=<your_password>" -ForegroundColor Yellow
    exit 1
}

function Invoke-MsDeploy {
    <#
    .SYNOPSIS
        Runs msdeploy.exe and handles exit code. Never logs the password.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$MsDeployExe,

        [Parameter(Mandatory)]
        [string]$Arguments,

        [string]$Description = "msdeploy"
    )

    if (-not (Test-Path $MsDeployExe)) {
        Write-Host "ERROR: msdeploy.exe not found at $MsDeployExe" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Running $Description..." -ForegroundColor DarkGray

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $MsDeployExe
    $psi.Arguments = $Arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    $exitCode = $proc.ExitCode

    if ($stdout) { Write-Host $stdout }
    if ($stderr) { Write-Host $stderr -ForegroundColor Red }

    if ($exitCode -ne 0) {
        Write-Host "  ERROR: $Description failed (exit code $exitCode)" -ForegroundColor Red
        exit 1
    }

    Write-Host "  $Description succeeded" -ForegroundColor Green
}

function Test-RobocopyExitCode {
    <#
    .SYNOPSIS
        Interprets robocopy exit code. Returns $true if acceptable, $false if fatal.
    .DESCRIPTION
        Robocopy exit codes:
          0 = No files copied, no failure
          1 = One or more files copied successfully
          2 = Extra files or directories detected
          4 = Some mismatched files detected
          8 = Some files could not be copied
          16 = Serious error, no files were copied
        Codes 0-7 are acceptable. Codes >= 8 are fatal.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$ExitCode
    )

    if ($ExitCode -ge 8) {
        Write-Host "  ERROR: Robocopy failed with exit code $ExitCode" -ForegroundColor Red
        if ($ExitCode -eq 8) {
            Write-Host "    Some files could not be copied (access denied, file locked, etc.)" -ForegroundColor Yellow
        } elseif ($ExitCode -eq 16) {
            Write-Host "    Serious error - no files were copied" -ForegroundColor Yellow
        }
        return $false
    }

    if ($ExitCode -ge 4) {
        Write-Host "  WARNING: Robocopy exit code $ExitCode (mismatched files detected)" -ForegroundColor Yellow
    }

    return $true
}
