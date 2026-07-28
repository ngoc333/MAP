$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishRoot = Join-Path $root "publish"
$publishCore = Join-Path $publishRoot "core"
$publishModules = Join-Path $publishRoot "modules"

Write-Host "=== MAP Build ===" -ForegroundColor Cyan
Write-Host ""

# 1. Clean & prepare
Write-Host "[1/3] Preparing output directories..." -ForegroundColor Cyan
@($publishCore, $publishModules) | ForEach-Object {
    if (Test-Path $_) { Remove-Item -Recurse -Force $_ }
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}

# 2. Restore
Write-Host "[2/3] Restoring packages..." -ForegroundColor Cyan
$sln = Join-Path $root "MAP.slnx"
dotnet restore $sln
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Restore failed" -ForegroundColor Red; exit 1 }

# 3. Build
Write-Host "[3/3] Building projects..." -ForegroundColor Cyan

$ok = 0
$fail = 0

function Build-And-Copy {
    param([string]$csproj, [string]$label, [string]$dest)

    $projDir = Split-Path $csproj -Parent
    $name = (Get-Item $csproj).BaseName

    Write-Host "  $label" -ForegroundColor Yellow -NoNewline
    dotnet build $csproj -c Release --nologo -v q 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED" -ForegroundColor Red
        $script:fail++
        return
    }

    # Tìm DLL trong bin/Release/<TFM>/
    $binRelease = Join-Path (Join-Path $projDir "bin") "Release"
    $tfms = Get-ChildItem $binRelease -Directory | Select-Object -ExpandProperty Name
    $found = $false
    foreach ($tfm in $tfms) {
        $dll = Join-Path (Join-Path $binRelease $tfm) "$name.dll"
        if (Test-Path $dll) {
            Copy-Item $dll $dest -Force
            $found = $true
            break
        }
    }

    if ($found) {
        Write-Host "  OK" -ForegroundColor Green
        $script:ok++
    } else {
        Write-Host "  WARN (DLL not found)" -ForegroundColor Yellow
        $script:ok++
    }
}

# Core (theo thứ tự phụ thuộc)
$cores = @(
    @{Csproj="Core/MAP.C.Contract/MAP.C.Contract.csproj";    Label="MAP.C.Contract"},
    @{Csproj="Core/MAP.C.Runtime/MAP.C.Runtime.csproj";      Label="MAP.C.Runtime"},
    @{Csproj="Core/MAP.C.Components/MAP.C.Components.csproj"; Label="MAP.C.Components"},
    @{Csproj="Core/MAP.C.Wpf/MAP.C.Wpf.csproj";             Label="MAP.C.Wpf"}
)

# Modules
$modules = @(
    @{Csproj="Modules/MAP.M.Home/MAP.M.Home.csproj";             Label="MAP.M.Home"},
    @{Csproj="Modules/MAP.M.Customers/MAP.M.Customers.csproj";    Label="MAP.M.Customers"},
    @{Csproj="Modules/MAP.M.Products/MAP.M.Products.csproj";      Label="MAP.M.Products"},
    @{Csproj="Modules/MAP.M.Reports/MAP.M.Reports.csproj";        Label="MAP.M.Reports"}
)

Write-Host "  -- Core --" -ForegroundColor DarkGray
foreach ($c in $cores) {
    Build-And-Copy (Join-Path $root $c.Csproj) $c.Label $publishCore
}

Write-Host "  -- Modules --" -ForegroundColor DarkGray
foreach ($m in $modules) {
    Build-And-Copy (Join-Path $root $m.Csproj) $m.Label $publishModules
}

# Copy modules vào MAP.H.Web/Modules/ để Web publish
$webModulesDir = Join-Path (Join-Path $root "MAP.H.Web") "Modules"
if (-not (Test-Path $webModulesDir)) {
    New-Item -ItemType Directory -Force -Path $webModulesDir | Out-Null
}
Copy-Item (Join-Path $publishModules "*.dll") $webModulesDir -Force

# Summary
Write-Host ""
Write-Host "=== BUILD COMPLETE ===" -ForegroundColor Green
Write-Host "  Core   : $publishCore" -ForegroundColor White
$coreCount = (Get-ChildItem $publishCore -File).Count
Write-Host "    $coreCount files" -ForegroundColor DarkGray
Write-Host "  Modules: $publishModules" -ForegroundColor White
$modCount = (Get-ChildItem $publishModules -File).Count
Write-Host "    $modCount files" -ForegroundColor DarkGray
