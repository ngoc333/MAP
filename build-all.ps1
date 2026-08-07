param(
    [switch]$RemoveSymbols
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishRoot = Join-Path $root "publish"
$publishDesktop = Join-Path $publishRoot "desktop"
$publishWeb = Join-Path $publishRoot "web"
$publishWebExe = Join-Path $publishRoot "web-exe"

Write-Host "=== MAP Deploy ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build
Write-Host "[1/4] Running build..." -ForegroundColor Cyan
$buildScript = Join-Path $root "build.ps1"
& $buildScript
if ($LASTEXITCODE -ne 0) { exit 1 }

# 2. Publish Desktop
Write-Host ""
Write-Host "[2/4] Publishing Desktop (win-x64, self-contained)..." -ForegroundColor Cyan

if (Test-Path $publishDesktop) { Remove-Item -Recurse -Force $publishDesktop }

$moduleProjects = @(Get-ChildItem (Join-Path $root "Modules") -Filter "*.csproj" -Recurse -File | Sort-Object FullName)
foreach ($moduleProject in $moduleProjects) {
    $proj = $moduleProject.FullName
    dotnet restore $proj -r win-x64 --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Restore $($moduleProject.Name) with win-x64 failed" -ForegroundColor Red
        exit 1
    }
}

$desktopCsproj = Join-Path (Join-Path $root "MAP.H.Desktop") "MAP.H.Desktop.csproj"
dotnet publish $desktopCsproj -c Release -r win-x64 --self-contained true -o $publishDesktop --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Desktop publish failed" -ForegroundColor Red
    exit 1
}

$tailwindCss = Join-Path (Join-Path (Join-Path (Join-Path $root "MAP.H.Web") "wwwroot") "css") "tailwind.css"
if (Test-Path $tailwindCss) {
    $destCss = Join-Path $publishDesktop "css"
    if (-not (Test-Path $destCss)) { New-Item -ItemType Directory -Force -Path $destCss | Out-Null }
    Copy-Item $tailwindCss $destCss -Force
}

if ($RemoveSymbols) {
    Get-ChildItem $publishDesktop -Filter "*.pdb" -Recurse | Remove-Item -Force
}

Write-Host "  OK" -ForegroundColor Green
$desktopSize = [math]::Round(((Get-ChildItem $publishDesktop -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "  Size: ${desktopSize} MB" -ForegroundColor DarkGray

# 3. Publish Web
Write-Host ""
Write-Host "[3/4] Publishing Web (Blazor WASM)..." -ForegroundColor Cyan

if (Test-Path $publishWeb) { Remove-Item -Recurse -Force $publishWeb }

$webCsproj = Join-Path (Join-Path $root "MAP.H.Web") "MAP.H.Web.csproj"
dotnet publish $webCsproj -c Release -o $publishWeb --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Web publish failed" -ForegroundColor Red
    exit 1
}

if ($RemoveSymbols) {
    Get-ChildItem $publishWeb -Filter "*.pdb" -Recurse | Remove-Item -Force
}

Write-Host "  OK" -ForegroundColor Green
$webSize = [math]::Round(((Get-ChildItem $publishWeb -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "  Size: ${webSize} MB" -ForegroundColor DarkGray

# 4. Publish Web .exe (self-contained, like CORE.Web)
Write-Host ""
Write-Host "[4/4] Publishing Web as self-contained .exe..." -ForegroundColor Cyan

if (Test-Path $publishWebExe) { Remove-Item -Recurse -Force $publishWebExe }

$hostCsproj = Join-Path (Join-Path $root "MAP.H.Web.Host") "MAP.H.Web.Host.csproj"

# Restore Host for win-x64
dotnet restore $hostCsproj -r win-x64 --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Host restore failed" -ForegroundColor Red
    exit 1
}

# Publish Host self-contained
dotnet publish $hostCsproj -c Release -r win-x64 --self-contained true -o $publishWebExe --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Web .exe publish failed" -ForegroundColor Red
    exit 1
}

# Copy Blazor WASM assets into wwwroot
$wwwrootDest = Join-Path $publishWebExe "wwwroot"
if (Test-Path $wwwrootDest) { Remove-Item -Recurse -Force $wwwrootDest }
New-Item -ItemType Directory -Force -Path $wwwrootDest | Out-Null

# Blazor WASM publish output has static assets in wwwroot subfolder
$webWwwroot = Join-Path $publishWeb "wwwroot"
$webSource = if (Test-Path $webWwwroot) { $webWwwroot } else { $publishWeb }
Copy-Item (Join-Path $webSource "*") $wwwrootDest -Recurse -Force

# Verify index.html exists
$indexHtml = Join-Path $wwwrootDest "index.html"
if (-not (Test-Path $indexHtml)) {
    Write-Host "ERROR: index.html not found in wwwroot after copy" -ForegroundColor Red
    exit 1
}

if ($RemoveSymbols) {
    Get-ChildItem $publishWebExe -Filter "*.pdb" -Recurse | Remove-Item -Force
}

$webExeName = (Get-Item $hostCsproj).BaseName + ".exe"
$webExeSize = [math]::Round(((Get-Item (Join-Path $publishWebExe $webExeName)).Length / 1MB), 1)
Write-Host "  OK" -ForegroundColor Green
Write-Host "  $webExeName (${webExeSize} MB)" -ForegroundColor DarkGray

# Summary
Write-Host ""
Write-Host "=== DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host ""

Write-Host "  Desktop  : $publishDesktop" -ForegroundColor White
Write-Host "    MAP.H.Desktop.exe  (${desktopSize} MB, self-contained)" -ForegroundColor DarkGray
Write-Host ""

Write-Host "  Web      : $publishWeb" -ForegroundColor White
Write-Host "    index.html  (${webSize} MB, Blazor WASM)" -ForegroundColor DarkGray
Write-Host ""

Write-Host "  Web .exe : $publishWebExe" -ForegroundColor White
Write-Host "    $webExeName  (${webExeSize} MB, click-to-run)" -ForegroundColor DarkGray
