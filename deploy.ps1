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

# Required paths — declared before any validation
$runAppExe     = Join-Path $root "Run-App\bin\Release\net48\Run-App.exe"
$publishCore    = Join-Path $root "publish\core"
$publishModules = Join-Path $root "publish\modules"
$publishDesktop = Join-Path $root "publish\desktop"
$publishWeb     = Join-Path $root "publish\web"

Write-Host "=== MAP Deploy ===" -ForegroundColor Cyan
Write-Host ""

# ──────────────────────────────────────────────
# 1. build.ps1
# ──────────────────────────────────────────────
Write-Host "[1/6] Running build..." -ForegroundColor Cyan
$buildScript = Join-Path $root "build.ps1"
& $buildScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

# ──────────────────────────────────────────────
# 2. Build Run-App
# ──────────────────────────────────────────────
Write-Host ""
Write-Host "[2/6] Building Run-App..." -ForegroundColor Cyan
$runAppCsproj = Join-Path $root "Run-App\Run-App.csproj"
dotnet build $runAppCsproj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Run-App build failed" -ForegroundColor Red
    exit 1
}
Write-Host "  OK" -ForegroundColor Green

# ──────────────────────────────────────────────
# 3. Validate build outputs (core + modules)
# ──────────────────────────────────────────────
Write-Host ""
Write-Host "[3/6] Validating build outputs..." -ForegroundColor Cyan

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
Write-Host "  OK" -ForegroundColor Green

# ──────────────────────────────────────────────
# 4. Publish + validate + deploy Desktop
# ──────────────────────────────────────────────
Write-Host ""
Write-Host "[4/6] Publishing and deploying Desktop..." -ForegroundColor Cyan

$desktopCsproj = Join-Path (Join-Path $root "MAP.H.Desktop") "MAP.H.Desktop.csproj"
dotnet publish $desktopCsproj -c Release -o $publishDesktop --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Desktop publish failed" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishDesktop) -or -not (Get-ChildItem $publishDesktop -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Desktop publish output is missing or empty at $publishDesktop" -ForegroundColor Red
    exit 1
}

Write-Host "  Target: $serverPath" -ForegroundColor DarkGray
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

# Copy Desktop static files to server
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
Write-Host "  Desktop deployed" -ForegroundColor Green

# ──────────────────────────────────────────────
# 5. Publish + validate + deploy Web
# ──────────────────────────────────────────────
Write-Host ""
Write-Host "[5/6] Publishing and deploying Web to IIS..." -ForegroundColor Cyan

$webCsproj = Join-Path (Join-Path $root "MAP.H.Web") "MAP.H.Web.csproj"
dotnet publish $webCsproj -c Release -o $publishWeb --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Web publish failed" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishWeb) -or -not (Get-ChildItem $publishWeb -File -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Web publish output is missing or empty at $publishWeb" -ForegroundColor Red
    exit 1
}

$msdeployExe = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
if (-not (Test-Path $msdeployExe)) {
    Write-Host "ERROR: msdeploy.exe not found at $msdeployExe" -ForegroundColor Red
    exit 1
}

Write-Host "  Source: $publishWeb" -ForegroundColor DarkGray
Write-Host "  Dest: $webDest @ (remote server)" -ForegroundColor DarkGray

$msdeployArgs = "-verb:sync -source:contentPath=$publishWeb -dest:contentPath=$webDest,computerName=$webComputerName,userName=$webUserName,password=$webPassword,authType=basic -allowUntrusted"

Invoke-MsDeploy -MsDeployExe $msdeployExe -Arguments $msdeployArgs -Description "Web deploy"

# ──────────────────────────────────────────────
# 6. Summary
# ──────────────────────────────────────────────
Write-Host ""
Write-Host "[6/6] === DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host "  Server: $serverPath" -ForegroundColor White
Write-Host "  Run-App.exe: $serverPath\Run-App.exe" -ForegroundColor DarkGray
Write-Host "  Core: $serverCore" -ForegroundColor DarkGray
Write-Host "  Modules: $serverModules" -ForegroundColor DarkGray
Write-Host "  Web: $webDest @ (remote server)" -ForegroundColor DarkGray
