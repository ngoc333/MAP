$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Load helpers
. (Join-Path $root "deploy-helpers.ps1")

# Read deploy configuration from environment variables or .env
$serverPath = Get-RequiredDeploySetting "MAP_DESKTOP_DEPLOY_PATH"
$webComputerName = Get-RequiredDeploySetting "MAP_WEB_DEPLOY_URL"
$webDest = Get-RequiredDeploySetting "MAP_WEB_DEPLOY_DEST"
$webUserName = Get-RequiredDeploySetting "MAP_WEB_DEPLOY_USER"
$webPassword = Get-RequiredDeploySetting "MAP_WEB_DEPLOY_PASSWORD"

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

# Validate required outputs
$runAppExe = Join-Path $root "Run-App\bin\Release\net48\Run-App.exe"
$publishWeb = Join-Path $root "publish\web"

if (-not (Test-Path $runAppExe)) {
    Write-Host "ERROR: Run-App.exe not found at $runAppExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishDesktop) -or -not (Get-ChildItem $publishDesktop -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: publish\desktop not found or is empty" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishWeb) -or -not (Get-ChildItem $publishWeb -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: publish\web not found or is empty" -ForegroundColor Red
    exit 1
}

# 3. Deploy to server
Write-Host ""
Write-Host "[3/4] Deploying desktop to server..." -ForegroundColor Cyan
Write-Host "  Target: $serverPath" -ForegroundColor DarkGray

# Ensure server path exists
if (-not (Test-Path $serverPath)) {
    New-Item -ItemType Directory -Force -Path $serverPath | Out-Null
}

# Copy Run-App.exe to server root
$runAppConfig = Join-Path $root "Run-App\bin\Release\net48\Run-App.exe.config"
Copy-Item $runAppExe $serverPath -Force
if (Test-Path $runAppConfig) {
    Copy-Item $runAppConfig $serverPath -Force
}
Write-Host "  Run-App.exe deployed" -ForegroundColor Green

# Copy desktop files to server\desktop
$serverDesktop = Join-Path $serverPath "desktop"
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

# 4. Deploy Web to IIS
Write-Host ""
Write-Host "[4/4] Deploying Web to IIS..." -ForegroundColor Cyan

Write-Host "  Source: $publishWeb" -ForegroundColor DarkGray
Write-Host "  Dest: $webDest @ (remote server)" -ForegroundColor DarkGray

$msdeployExe = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
if (-not (Test-Path $msdeployExe)) {
    Write-Host "ERROR: msdeploy.exe not found at $msdeployExe" -ForegroundColor Red
    exit 1
}

$arguments = "-verb:sync -source:contentPath=`"$publishWeb`" -dest:contentPath=`"$webDest`",computerName=`"$webComputerName`",userName=`"$webUserName`",password=`"$webPassword`",authType=Basic -allowUntrusted"

Invoke-MsDeploy -MsDeployExe $msdeployExe -Arguments $arguments -Description "Web deploy"

# Summary
Write-Host ""
Write-Host "=== DEPLOY ALL COMPLETE ===" -ForegroundColor Green
Write-Host "  Server: $serverPath" -ForegroundColor White
Write-Host "  Run-App.exe: $serverPath\Run-App.exe" -ForegroundColor DarkGray
Write-Host "  Desktop: $serverDesktop" -ForegroundColor DarkGray
Write-Host "  Web: $webDest @ (remote server)" -ForegroundColor DarkGray
