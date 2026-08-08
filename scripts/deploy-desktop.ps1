Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DesktopDeploymentPlan {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $items = [System.Collections.Generic.List[object]]::new()
    $sourceRoot = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\')
    foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceRoot -File -Recurse) {
        $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
        # The bootstrapper has its own release path and must never be deployed here.
        if ([System.IO.Path]::GetFileName($relativePath) -ieq 'Run-App.exe') { continue }

        $destinationFile = Join-Path $DestinationPath $relativePath
        $classification = 'NEW'
        if (Test-Path -LiteralPath $destinationFile -PathType Leaf) {
            $classification = if ((Get-FileSha256 -Path $sourceFile.FullName) -eq (Get-FileSha256 -Path $destinationFile)) { 'UNCHANGED' } else { 'CHANGED' }
        }
        $items.Add([pscustomobject]@{
            Source = $sourceFile.FullName
            Destination = $destinationFile
            RelativePath = $relativePath
            Classification = $classification
        })
    }
    return $items
}

function Copy-DesktopDeploymentFile {
    param(
        [Parameter(Mandatory)]$Item,
        [Parameter(Mandatory)][datetime]$ReleaseTimestampUtc
    )

    $destinationDirectory = Split-Path -Parent $Item.Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    $temporaryPath = "$($Item.Destination).mapdeploy.tmp"
    try {
        Copy-Item -LiteralPath $Item.Source -Destination $temporaryPath -Force
        if ((Get-FileSha256 -Path $Item.Source) -ne (Get-FileSha256 -Path $temporaryPath)) {
            throw "Hash verification failed for '$($Item.RelativePath)'."
        }
        Move-Item -LiteralPath $temporaryPath -Destination $Item.Destination -Force
        (Get-Item -LiteralPath $Item.Destination).LastWriteTimeUtc = $ReleaseTimestampUtc
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

function Invoke-DesktopDeployment {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DeploymentRoot,
        [Parameter(Mandatory)][datetime]$ReleaseTimestampUtc
    )

    $destinationPath = Join-Path $DeploymentRoot 'desktop'
    New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    $plan = @(Get-DesktopDeploymentPlan -SourcePath $SourcePath -DestinationPath $destinationPath)

    $newCount = @($plan | Where-Object Classification -eq 'NEW').Count
    $changedCount = @($plan | Where-Object Classification -eq 'CHANGED').Count
    $unchangedCount = @($plan | Where-Object Classification -eq 'UNCHANGED').Count
    foreach ($item in $plan | Where-Object { $_.Classification -ne 'UNCHANGED' }) {
        Write-Host ('{0,-8} {1}' -f $item.Classification, $item.RelativePath)
        Copy-DesktopDeploymentFile -Item $item -ReleaseTimestampUtc $ReleaseTimestampUtc
    }

    Write-Host ''
    Write-Host 'Desktop Deployment'
    Write-Host '------------------------------'
    Write-Host ('Files scanned : {0}' -f $plan.Count)
    Write-Host ('New           : {0}' -f $newCount)
    Write-Host ('Changed       : {0}' -f $changedCount)
    Write-Host ('Unchanged     : {0}' -f $unchangedCount)
    Write-Host ('Copied        : {0}' -f ($newCount + $changedCount))

    return [pscustomobject]@{ New = $newCount; Changed = $changedCount; Unchanged = $unchangedCount }
}
