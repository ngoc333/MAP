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

# Copy core DLLs
$serverCore = Join-Path $serverPath "core"
if (Test-Path $publishCore) {
    if (-not (Test-Path $serverCore)) {
        New-Item -ItemType Directory -Force -Path $serverCore | Out-Null
    }
    $fileCount = (Get-ChildItem $publishCore -Filter "*.dll" -File).Count
    Write-Host "  Copying $fileCount core DLLs..." -ForegroundColor DarkGray
    Copy-Item (Join-Path $publishCore "*.dll") $serverCore -Force
    Write-Host "  Core deployed" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Core publish folder not found" -ForegroundColor Yellow
}

# Copy module DLLs
$serverModules = Join-Path $serverPath "modules"
if (Test-Path $publishModules) {
    if (-not (Test-Path $serverModules)) {
        New-Item -ItemType Directory -Force -Path $serverModules | Out-Null
    }
    $fileCount = (Get-ChildItem $publishModules -Filter "*.dll" -File).Count
    Write-Host "  Copying $fileCount module DLLs..." -ForegroundColor DarkGray
    Copy-Item (Join-Path $publishModules "*.dll") $serverModules -Force
    Write-Host "  Modules deployed" -ForegroundColor Green
} else {
    Write-Host "  WARNING: Modules publish folder not found" -ForegroundColor Yellow
}

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

$webCsproj = Join-Path (Join-Path $root "MAP.H.Web") "MAP.H.Web.csproj"
dotnet publish $webCsproj -c Release -o $publishWeb --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Web publish failed" -ForegroundColor Red
    exit 1
}

Get-ChildItem $publishWeb -Filter "*.pdb" -Recurse | Remove-Item -Force

Write-Host "  Source: $publishWeb" -ForegroundColor DarkGray
Write-Host "  Dest: $webDest @ (remote server)" -ForegroundColor DarkGray

$msdeployExe = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
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
