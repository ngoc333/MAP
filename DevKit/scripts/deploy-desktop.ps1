[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DestinationModulesPath,

    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Get-DotNetProperty {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$PropertyName,

        [Parameter(Mandatory)]
        [string]$Configuration
    )

    $output = & dotnet msbuild $ProjectPath '-nologo' "-property:Configuration=$Configuration" "-getProperty:$PropertyName"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve $PropertyName for module project '$ProjectPath'. dotnet msbuild failed with exit code $LASTEXITCODE."
    }

    $value = @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1)
    if ($value.Count -ne 1) {
        throw "Could not resolve a single $PropertyName value for module project '$ProjectPath'."
    }

    return $value[0]
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Test-IsFrameworkAssembly {
    param([Parameter(Mandatory)][string]$FileName)

    return $FileName -eq 'mscorlib.dll' -or
        $FileName -eq 'netstandard.dll' -or
        $FileName -eq 'System.dll' -or
        $FileName -like 'System.*' -or
        $FileName -like 'Microsoft.*'
}

function Get-ModuleProjects {
    param([Parameter(Mandatory)][string]$ModulesPath)

    $projects = @(
        Get-ChildItem -LiteralPath $ModulesPath -Recurse -Filter '*.csproj' -File |
            Sort-Object FullName
    )
    if ($projects.Count -eq 0) {
        throw "No module projects were found under: $ModulesPath"
    }

    return $projects
}

function Get-ModuleMetadata {
    param(
        [Parameter(Mandatory)]$Project,
        [Parameter(Mandatory)][string]$Configuration,
        [Parameter(Mandatory)][string]$StagingRoot
    )

    $targetPath = Get-DotNetProperty -ProjectPath $Project.FullName -PropertyName 'TargetPath' -Configuration $Configuration
    if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        throw "Module output was not found. Run build.ps1 first: $targetPath"
    }

    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($targetPath)
    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        throw "Could not determine assembly name for module project '$($Project.FullName)'."
    }

    return [pscustomobject]@{
        Name = $assemblyName
        ProjectPath = $Project.FullName
        TargetPath = $targetPath
        StagingPath = Join-Path $StagingRoot $assemblyName
    }
}

function Stage-ModuleDependencyClosure {
    param(
        [Parameter(Mandatory)]$Module,
        [Parameter(Mandatory)][string]$Configuration
    )

    if (Test-Path -LiteralPath $Module.StagingPath) {
        Remove-Item -LiteralPath $Module.StagingPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Module.StagingPath -Force | Out-Null

    Invoke-DotNet -Arguments @(
        'publish', $Module.ProjectPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '-o', $Module.StagingPath
    ) -Description "Module staging: $($Module.Name)"
}

function Get-ModuleDeploymentFiles {
    param(
        [Parameter(Mandatory)]$Module,
        [Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$PlatformAssemblyNames,
        [Parameter(Mandatory)][hashtable]$SdkPlatformHashes
    )

    $stageRoot = (Resolve-Path -LiteralPath $Module.StagingPath).Path.TrimEnd('\', '/')
    $files = [System.Collections.Generic.List[object]]::new()

    foreach ($file in @(Get-ChildItem -LiteralPath $stageRoot -File -Recurse -Filter '*.dll' | Sort-Object FullName)) {
        $fileName = $file.Name
        if ($PlatformAssemblyNames.Contains($fileName)) {
            if ($SdkPlatformHashes.ContainsKey($fileName)) {
                $moduleHash = Get-FileSha256 -Path $file.FullName
                if ($moduleHash -ne $SdkPlatformHashes[$fileName]) {
                    throw "Module/platform mismatch: $($Module.Name) contains $fileName with SHA256=$moduleHash, but the SDK SHA256=$($SdkPlatformHashes[$fileName])."
                }
            }
            continue
        }

        if (Test-IsFrameworkAssembly -FileName $fileName) {
            continue
        }

        $relativePath = $file.FullName.Substring($stageRoot.Length).TrimStart('\', '/')
        $files.Add([pscustomobject]@{
            Module = $Module.Name
            Source = $file.FullName
            RelativePath = $relativePath
            Hash = Get-FileSha256 -Path $file.FullName
        })
    }

    $moduleAssembly = @($files | Where-Object { $_.RelativePath -eq "$($Module.Name).dll" })
    if ($moduleAssembly.Count -ne 1) {
        throw "Module staging for '$($Module.Name)' does not contain exactly one module assembly."
    }

    return $files
}

function Merge-DeploymentFiles {
    param([Parameter(Mandatory)][object[]]$ModuleFiles)

    $mergedFiles = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $ModuleFiles) {
        if ($mergedFiles.ContainsKey($file.RelativePath)) {
            $existing = $mergedFiles[$file.RelativePath]
            if ($existing.Hash -ne $file.Hash) {
                throw "Dependency conflict:`n$($file.RelativePath)`n$($existing.Module) SHA256=$($existing.Hash)`n$($file.Module) SHA256=$($file.Hash)"
            }
            continue
        }

        $mergedFiles.Add($file.RelativePath, $file)
    }

    return @($mergedFiles.Values | Sort-Object RelativePath)
}

function Get-DeploymentPlan {
    param(
        [Parameter(Mandatory)][object[]]$Files,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $plan = [System.Collections.Generic.List[object]]::new()
    foreach ($file in $Files) {
        $destination = Join-Path $DestinationPath $file.RelativePath
        $classification = 'NEW'
        if (Test-Path -LiteralPath $destination) {
            if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
                throw "Desktop destination path is not a file: $($file.RelativePath)"
            }

            if ((Get-FileSha256 -Path $destination) -eq $file.Hash) {
                $classification = 'UNCHANGED'
            }
            else {
                $classification = 'CHANGED'
            }
        }

        $plan.Add([pscustomobject]@{
            Module = $file.Module
            Source = $file.Source
            SourceHash = $file.Hash
            RelativePath = $file.RelativePath
            Destination = $destination
            Classification = $classification
        })
    }

    return $plan
}

function Copy-DeploymentFileAtomically {
    param(
        [Parameter(Mandatory)]$Item,
        [Parameter(Mandatory)][datetime]$ReleaseTimestampUtc
    )

    $destinationDirectory = Split-Path -Parent $Item.Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    $temporaryPath = '{0}.mapdeploy.tmp' -f $Item.Destination

    try {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }

        [System.IO.File]::Copy($Item.Source, $temporaryPath, $true)
        if ((Get-FileSha256 -Path $temporaryPath) -ne $Item.SourceHash) {
            throw "Temporary-file hash verification failed for '$($Item.RelativePath)'."
        }

        [System.IO.File]::Move($temporaryPath, $Item.Destination, $true)
        if ((Get-FileSha256 -Path $Item.Destination) -ne $Item.SourceHash) {
            throw "Destination hash verification failed for '$($Item.RelativePath)'."
        }

        (Get-Item -LiteralPath $Item.Destination).LastWriteTimeUtc = $ReleaseTimestampUtc
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

$root = Split-Path -Parent $PSScriptRoot
$modulesPath = Join-Path $root 'Modules'
$stagingRoot = Join-Path $root 'artifacts\modules'
$sdkPath = Join-Path $root 'Sdk\lib'
$platformAssemblyNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$sdkPlatformHashes = @{}

foreach ($platformAssembly in @('MAP.C.Contract.dll', 'MAP.C.UI.dll', 'MAP.C.Runtime.dll', 'MAP.C.Wpf.dll', 'MAP.C.Wasm.dll')) {
    $null = $platformAssemblyNames.Add($platformAssembly)
    $sdkAssemblyPath = Join-Path $sdkPath $platformAssembly
    if (Test-Path -LiteralPath $sdkAssemblyPath -PathType Leaf) {
        $sdkPlatformHashes[$platformAssembly] = Get-FileSha256 -Path $sdkAssemblyPath
    }
}

$moduleProjects = @(Get-ModuleProjects -ModulesPath $modulesPath)
$modules = @($moduleProjects | ForEach-Object {
    Get-ModuleMetadata -Project $_ -Configuration $Configuration -StagingRoot $stagingRoot
})
$duplicateAssemblyNames = @($modules | Group-Object Name | Where-Object Count -gt 1)
if ($duplicateAssemblyNames.Count -gt 0) {
    throw "Module projects must produce unique assembly names: $($duplicateAssemblyNames.Name -join ', ')."
}

foreach ($module in $modules) {
    Stage-ModuleDependencyClosure -Module $module -Configuration $Configuration
}

$moduleFiles = [System.Collections.Generic.List[object]]::new()
foreach ($module in $modules) {
    foreach ($file in @(Get-ModuleDeploymentFiles -Module $module -PlatformAssemblyNames $platformAssemblyNames -SdkPlatformHashes $sdkPlatformHashes)) {
        $moduleFiles.Add($file)
    }
}

$mergedFiles = @(Merge-DeploymentFiles -ModuleFiles $moduleFiles.ToArray())
New-Item -ItemType Directory -Path $DestinationModulesPath -Force | Out-Null
$destinationModulesPath = (Resolve-Path -LiteralPath $DestinationModulesPath).Path
$plan = @(Get-DeploymentPlan -Files $mergedFiles -DestinationPath $destinationModulesPath)
$releaseTimestampUtc = [datetime]::UtcNow

Write-Host 'Desktop Module Deployment'
Write-Host '----------------------------------------'
$currentModule = $null
foreach ($item in @($plan | Sort-Object Module, RelativePath)) {
    if ($currentModule -ne $item.Module) {
        $currentModule = $item.Module
        Write-Host "`nModule: $currentModule"
    }

    if ($item.Classification -eq 'UNCHANGED') {
        $timestamp = (Get-Item -LiteralPath $item.Destination).LastWriteTimeUtc.ToString('O')
        Write-Host ('{0,-10} {1} (destination: {2})' -f $item.Classification, $item.RelativePath, $timestamp)
        continue
    }

    Copy-DeploymentFileAtomically -Item $item -ReleaseTimestampUtc $releaseTimestampUtc
    $timestamp = (Get-Item -LiteralPath $item.Destination).LastWriteTimeUtc.ToString('O')
    Write-Host ('{0,-10} {1} (destination: {2})' -f $item.Classification, $item.RelativePath, $timestamp)
}

$newItems = @($plan | Where-Object Classification -eq 'NEW')
$changedItems = @($plan | Where-Object Classification -eq 'CHANGED')
$unchangedItems = @($plan | Where-Object Classification -eq 'UNCHANGED')

Write-Host ''
Write-Host 'Summary'
Write-Host '----------------------------------------'
Write-Host "Modules      : $($modules.Count)"
Write-Host "Files scanned: $($plan.Count)"
Write-Host "New          : $($newItems.Count)"
Write-Host "Changed      : $($changedItems.Count)"
Write-Host "Unchanged    : $($unchangedItems.Count)"
Write-Host "Copied       : $($newItems.Count + $changedItems.Count)"
