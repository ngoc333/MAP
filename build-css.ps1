# build-css.ps1
# Regenerates Tailwind CSS from source styles.
# Run this only when you change styles in Shared/Styles/.
# Normal builds use the pre-committed tailwind.css.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$tailwindExe = Join-Path $root "tools\tailwindcss.exe"
$inputCss = Join-Path $root "Shared\Styles\tailwind-input.css"
$configJs = Join-Path $root "Shared\Styles\tailwind.config.js"
$outputCss = Join-Path $root "MAP.H.Web\wwwroot\css\tailwind.css"

Write-Host "=== Build CSS ===" -ForegroundColor Cyan

# Check input files exist
if (-not (Test-Path $inputCss)) {
    Write-Host "ERROR: Input CSS not found: $inputCss" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $configJs)) {
    Write-Host "ERROR: Tailwind config not found: $configJs" -ForegroundColor Red
    exit 1
}

# Check if Tailwind CLI exists
if (-not (Test-Path $tailwindExe)) {
    Write-Host "ERROR: Tailwind CLI not found at: $tailwindExe" -ForegroundColor Red
    Write-Host "" -ForegroundColor Yellow
    Write-Host "Download it manually from:" -ForegroundColor Yellow
    Write-Host "  https://github.com/tailwindlabs/tailwindcss/releases/download/v3.4.17/tailwindcss-windows-x64.exe" -ForegroundColor Yellow
    Write-Host "" -ForegroundColor Yellow
    Write-Host "Save it to: tools\tailwindcss.exe" -ForegroundColor Yellow
    exit 1
}

# Build CSS
Write-Host "Building Tailwind CSS..." -ForegroundColor DarkGray
Write-Host "  Input:  $inputCss" -ForegroundColor DarkGray
Write-Host "  Output: $outputCss" -ForegroundColor DarkGray

& $tailwindExe -i $inputCss -c $configJs -o $outputCss --minify
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Tailwind build failed" -ForegroundColor Red
    exit 1
}

$fileSize = [math]::Round(((Get-Item $outputCss).Length / 1KB), 1)
Write-Host "" -ForegroundColor Green
Write-Host "CSS built successfully: ${fileSize} KB" -ForegroundColor Green
Write-Host "" -ForegroundColor Yellow
Write-Host "Remember to commit the updated tailwind.css!" -ForegroundColor Yellow
