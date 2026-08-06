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

$publishCore = Join-Path $root "publish\core"
$publishModules = Join-Path $root "publish\modules"
$publishDesktop = Join-Path $root "publish\desktop"
$publishWeb = Join-Path $root "publish\web"

Write-Host "=== MAP Deploy ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build (incremental)
Write-Host "[1/4] Running build..." -ForegroundColor Cyan
$buildScript = Join-Path $root "build.ps1"
& $buildScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
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

# 3. Deploy desktop to server
Write-Host ""
Write-Host "[3/4] Deploying desktop to server..." -ForegroundColor Cyan
Write-Host "  Target: $serverPath" -ForegroundColor DarkGray

# Validate required outputs
if (-not (Test-Path $runAppExe)) {
    Write-Host "ERROR: Run-App.exe not found at $runAppExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishCore) -or -not (Get-ChildItem $publishCore -Filter "*.dll" -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: publish\core not found or contains no DLLs" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishModules) -or -not (Get-ChildItem $publishModules -Filter "*.dll" -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: publish\modules not found or contains no DLLs" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishDesktop)) {
    Write-Host "ERROR: publish\desktop not found" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishWeb)) {
    Write-Host "ERROR: publish\web not found" -ForegroundColor Red
    exit 1
}

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

# Copy core DLLs
$serverCore = Join-Path $serverPath "core"
if (-not (Test-Path $serverCore)) {
    New-Item -ItemType Directory -Force -Path $serverCore | Out-Null
}
$fileCount = (Get-ChildItem $publishCore -Filter "*.dll" -File).Count
Write-Host "  Copying $fileCount core DLLs..." -ForegroundColor DarkGray
Copy-Item (Join-Path $publishCore "*.dll") $serverCore -Force
Write-Host "  Core deployed" -ForegroundColor Green

# Copy module DLLs
$serverModules = Join-Path $serverPath "modules"
if (-not (Test-Path $serverModules)) {
    New-Item -ItemType Directory -Force -Path $serverModules | Out-Null
}
$fileCount = (Get-ChildItem $publishModules -Filter "*.dll" -File).Count
Write-Host "  Copying $fileCount module DLLs..." -ForegroundColor DarkGray
Copy-Item (Join-Path $publishModules "*.dll") $serverModules -Force
Write-Host "  Modules deployed" -ForegroundColor Green

# Publish Desktop app (index.html, css, js, fonts, _content, _framework...)
Write-Host "  Publishing Desktop static files..." -ForegroundColor DarkGray
$desktopCsproj = Join-Path (Join-Path $root "MAP.H.Desktop") "MAP.H.Desktop.csproj"
dotnet publish $desktopCsproj -c Release -o $publishDesktop --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Desktop publish failed" -ForegroundColor Red
    exit 1
}

# Copy static files to server (index.html, css, js, fonts, _content, _framework)
$staticItems = @("index.html", "css", "js", "fonts", "_content", "_framework")
foreach ($item in $staticItems) {
    $src = Join-Path $publishDesktop $item
    if (Test-Path $src) {
        $dest = Join-Path $serverPath $item
        if (Test-Path $src -PathType Container) {
            if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
            Copy-Item $src $dest -Recurse -Force
        } else {
            Copy-Item $src $dest -Force
        }
        Write-Host "  $item deployed" -ForegroundColor Green
    }
}
Write-Host "  Desktop static files deployed" -ForegroundColor Green

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

$msdeployArgs = "-verb:sync -source:contentPath=$publishWeb -dest:contentPath=$webDest,computerName=$webComputerName,userName=$webUserName,password=$webPassword,authType=basic -allowUntrusted"

Invoke-MsDeploy -MsDeployExe $msdeployExe -Arguments $msdeployArgs -Description "Web deploy"

# Summary
Write-Host ""
Write-Host "=== DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host "  Server: $serverPath" -ForegroundColor White
Write-Host "  Run-App.exe: $serverPath\Run-App.exe" -ForegroundColor DarkGray
Write-Host "  Core: $serverCore" -ForegroundColor DarkGray
Write-Host "  Modules: $serverModules" -ForegroundColor DarkGray
Write-Host "  Web: $webDest @ (remote server)" -ForegroundColor DarkGray
