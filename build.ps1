$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishRoot = Join-Path $root "publish"
$publishCore = Join-Path $publishRoot "core"
$publishModules = Join-Path $publishRoot "modules"
$webModulesDir = Join-Path (Join-Path $root "MAP.H.Web") "Modules"

Write-Host "=== MAP Build ===" -ForegroundColor Cyan
Write-Host ""

# 1. Clean & prepare
Write-Host "[1/3] Preparing output directories..." -ForegroundColor Cyan
@($publishCore, $publishModules) | ForEach-Object {
    if (Test-Path $_) {
        # Retry up to 3 times in case files are temporarily locked (e.g. by IIS or a previous PS session)
        $retries = 0
        $deleted = $false
        while (-not $deleted -and $retries -lt 3) {
            try {
                Remove-Item -Recurse -Force $_ -ErrorAction Stop
                $deleted = $true
            } catch {
                $retries++
                if ($retries -lt 3) {
                    Write-Host "  Retry $retries/3: cleanup failed ($($_.Exception.Message)), waiting..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                } else {
                    Write-Host "  WARNING: Could not clean $_ - files may be locked. Continuing anyway." -ForegroundColor Yellow
                }
            }
        }
    }
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}

# 2. Restore
Write-Host "[2/3] Restoring packages..." -ForegroundColor Cyan
$sln = Join-Path $root "MAP.slnx"
dotnet restore $sln
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Restore failed" -ForegroundColor Red; exit 1 }

# 3. Build
Write-Host "[3/3] Building projects..." -ForegroundColor Cyan

function Build-And-Copy {
    param([string]$csproj, [string]$label, [string]$dest)

    $projDir = Split-Path $csproj -Parent
    $name = (Get-Item $csproj).BaseName

    Write-Host "  $label" -ForegroundColor Yellow -NoNewline
    dotnet build $csproj -c Release --nologo -v q

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED" -ForegroundColor Red
        throw "Build failed: $label"
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
    } else {
        throw "Build succeeded but '$name.dll' was not found for $label."
    }
}

function Test-ModuleLocalization {
    param([string]$dll)

    try {
        # Read the DLL as raw bytes and load from memory to avoid locking the file.
        # Assembly.Load(byte[]) reads the file once and releases the handle immediately.
        $dllPath = Resolve-Path $dll
        $bytes = [System.IO.File]::ReadAllBytes($dllPath)
        $assembly = [System.Reflection.Assembly]::Load($bytes)
        $prefix = "$($assembly.GetName().Name).Localization"
        $resources = $assembly.GetManifestResourceNames()
        $missing = @("$prefix.vi.json", "$prefix.en.json") | Where-Object { $_ -notin $resources }

        if ($missing.Count -gt 0) {
            throw "Missing embedded localization resource(s): $($missing -join ', ')"
        }
    }
    catch {
        Write-Host "ERROR: Module '$dll' is invalid: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Core (theo thứ tự phụ thuộc)
$cores = @(
    @{Csproj="Core/MAP.C.Contract/MAP.C.Contract.csproj";    Label="MAP.C.Contract"},
    @{Csproj="Core/MAP.C.Runtime/MAP.C.Runtime.csproj";      Label="MAP.C.Runtime"},
    @{Csproj="Core/MAP.C.UI/MAP.C.UI.csproj";                Label="MAP.C.UI"},
    @{Csproj="Core/MAP.C.Wasm/MAP.C.Wasm.csproj";            Label="MAP.C.Wasm"},
    @{Csproj="Core/MAP.C.Wpf/MAP.C.Wpf.csproj";             Label="MAP.C.Wpf"}
)

# Modules are discovered so new module projects require no host configuration.
$modules = @(Get-ChildItem (Join-Path $root "Modules") -Filter "*.csproj" -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        @{Csproj=$_.FullName; Label=$_.BaseName}
    })

Write-Host "  -- Core --" -ForegroundColor DarkGray
foreach ($c in $cores) {
    Build-And-Copy (Join-Path $root $c.Csproj) $c.Label $publishCore
}

Write-Host "  -- Modules --" -ForegroundColor DarkGray
foreach ($m in $modules) {
    Build-And-Copy $m.Csproj $m.Label $publishModules
}

# Copy fresh modules into MAP.H.Web/Modules/ for Web lazy loading.
if (-not (Test-Path $webModulesDir)) {
    New-Item -ItemType Directory -Force -Path $webModulesDir | Out-Null
}
Remove-Item (Join-Path $webModulesDir "*.dll") -Force -ErrorAction SilentlyContinue

Get-ChildItem $publishModules -Filter "*.dll" -File | ForEach-Object {
    Test-ModuleLocalization $_.FullName
}
Copy-Item (Join-Path $publishModules "*.dll") $webModulesDir -Force

$expectedCoreDlls = $cores | ForEach-Object { "$($_.Label).dll" }
$missingCoreDlls = $expectedCoreDlls | Where-Object { -not (Test-Path (Join-Path $publishCore $_)) }
if ($missingCoreDlls) {
    throw "Missing core DLL(s): $($missingCoreDlls -join ', ')"
}

# Summary
Write-Host ""
Write-Host "=== BUILD COMPLETE ===" -ForegroundColor Green
Write-Host "  Core   : $publishCore" -ForegroundColor White
$coreCount = (Get-ChildItem $publishCore -File).Count
Write-Host "    $coreCount files" -ForegroundColor DarkGray
Write-Host "  Modules: $publishModules" -ForegroundColor White
$modCount = (Get-ChildItem $publishModules -File).Count
Write-Host "    $modCount files" -ForegroundColor DarkGray
