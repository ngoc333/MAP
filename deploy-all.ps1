$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Load helpers
. (Join-Path $root "deploy-helpers.ps1")

# Hardcoded deploy configuration (non-sensitive)
$serverPath = "\\172.30.10.8\WebService\LGMES_LIVE_6_Service\DeployAssembly\FormAssembly\MAP-App"
$webComputerName = "https://172.30.10.124:8172/msdeploy.axd"
$webDest = "MAP"
$webUserName = "administrator"

# Read password from .env or environment variable
$webPassword = Get-DeployPassword

$publishDesktop = Join-Path $root "publish\desktop"

Write-Host "=== MAP Deploy All ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build all (desktop + web)
Write-Host "[1/4] Running build-all..." -ForegroundColor Cyan
$buildAllScript = Join-Path $root "build-all.ps1"
& $buildAllScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build-all failed" -ForegroundColor Red
    exit 1
}

# 2. Build Run-App (AutoDownload)
Write-Host ""
Write-Host "[2/4] Building Run-App (AutoDownload)..." -ForegroundColor Cyan

$runAppCsproj = Join-Path $root "Run-App\Run-App.csproj"
dotnet build $runAppCsproj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Run-App build failed" -ForegroundColor Red
    exit 1
}

Write-Host "  OK" -ForegroundColor Green

# 3. Deploy to server
Write-Host ""
Write-Host "[3/4] Deploying desktop to server..." -ForegroundColor Cyan
Write-Host "  Target: $serverPath" -ForegroundColor DarkGray

# Ensure server path exists
if (-not (Test-Path $serverPath)) {
    New-Item -ItemType Directory -Force -Path $serverPath | Out-Null
}

# Copy Run-App.exe to server root
$runAppExe = Join-Path $root "Run-App\bin\Release\net48\Run-App.exe"
$runAppConfig = Join-Path $root "Run-App\bin\Release\net48\Run-App.exe.config"
if (Test-Path $runAppExe) {
    Copy-Item $runAppExe $serverPath -Force
    if (Test-Path $runAppConfig) {
        Copy-Item $runAppConfig $serverPath -Force
    }
    Write-Host "  Run-App.exe deployed" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Run-App.exe not found at $runAppExe" -ForegroundColor Yellow
}

# Copy desktop files to server\desktop
$serverDesktop = Join-Path $serverPath "desktop"
if (Test-Path $publishDesktop) {
    if (-not (Test-Path $serverDesktop)) {
        New-Item -ItemType Directory -Force -Path $serverDesktop | Out-Null
    }

    $fileCount = (Get-ChildItem $publishDesktop -Recurse -File).Count
    Write-Host "  Copying $fileCount files to desktop..." -ForegroundColor DarkGray

    $robocopyArgs = @($publishDesktop, $serverDesktop, "/MIR", "/NJH", "/NJS", "/NDL", "/NP", "/NFL", "/NC", "/NS")
    & robocopy @robocopyArgs | Out-Null
    $exitCode = $LASTEXITCODE

    if (-not (Test-RobocopyExitCode -ExitCode $exitCode)) {
        Write-Host "ERROR: Desktop deploy failed" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Desktop files deployed" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Desktop publish folder not found" -ForegroundColor Yellow
}

# 4. Deploy Web to IIS
Write-Host ""
Write-Host "[4/4] Deploying Web to IIS..." -ForegroundColor Cyan

$publishWeb = Join-Path $root "publish\web"

if (Test-Path $publishWeb) {
    Write-Host "  Source: $publishWeb" -ForegroundColor DarkGray
    Write-Host "  Dest: $webDest @ (remote server)" -ForegroundColor DarkGray

    $msdeployExe = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
    $arguments = "-verb:sync -source:contentPath=`"$publishWeb`" -dest:contentPath=`"$webDest`",computerName=`"$webComputerName`",userName=`"$webUserName`",password=`"$webPassword`",authType=Basic -allowUntrusted"

    Invoke-MsDeploy -MsDeployExe $msdeployExe -Arguments $arguments -Description "Web deploy"
} else {
    Write-Host "  WARNING: Web publish folder not found at $publishWeb" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "=== DEPLOY ALL COMPLETE ===" -ForegroundColor Green
Write-Host "  Server: $serverPath" -ForegroundColor White
Write-Host "  Run-App.exe: $serverPath\Run-App.exe" -ForegroundColor DarkGray
Write-Host "  Desktop: $serverDesktop" -ForegroundColor DarkGray
Write-Host "  Web: $webDest @ (remote server)" -ForegroundColor DarkGray
