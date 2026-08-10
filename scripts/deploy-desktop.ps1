Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DesktopDeploymentPlan {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $sourceRoot = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\', '/')
    $plan = [System.Collections.Generic.List[object]]::new()

    foreach ($sourceFile in @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse)) {
        $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
        $destinationFile = Join-Path $DestinationPath $relativePath
        $classification = 'NEW'

        if (Test-Path -LiteralPath $destinationFile) {
            if (-not (Test-Path -LiteralPath $destinationFile -PathType Leaf)) {
                throw "Desktop destination path is not a file: $relativePath"
            }

            $destinationInfo = Get-Item -LiteralPath $destinationFile
            if ($sourceFile.Length -ne $destinationInfo.Length) {
                $classification = 'CHANGED'
            }
            else {
                $sourceHash = Get-FileSha256 -Path $sourceFile.FullName
                $destinationHash = Get-FileSha256 -Path $destinationFile
                if ($sourceHash -eq $destinationHash) {
                    $classification = 'UNCHANGED'
                }
                else {
                    $classification = 'CHANGED'
                }
            }
        }

        $plan.Add([pscustomobject]@{
            Source = $sourceFile.FullName
            Destination = $destinationFile
            RelativePath = $relativePath
            Classification = $classification
        })
    }

    return $plan
}

function Copy-DesktopDeploymentFile {
    param(
        [Parameter(Mandatory)]$Item,
        [Parameter(Mandatory)][datetime]$ReleaseTimestampUtc
    )

    $destinationDirectory = Split-Path -Parent $Item.Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    $temporaryPath = '{0}.mapdeploy.tmp' -f $Item.Destination

    try {
        Copy-Item -LiteralPath $Item.Source -Destination $temporaryPath -Force
        $sourceHash = Get-FileSha256 -Path $Item.Source
        $temporaryHash = Get-FileSha256 -Path $temporaryPath
        if ($sourceHash -ne $temporaryHash) {
            throw "Desktop hash verification failed for '$($Item.RelativePath)'."
        }

        Move-Item -LiteralPath $temporaryPath -Destination $Item.Destination -Force
        $destinationInfo = Get-Item -LiteralPath $Item.Destination
        if ((Get-FileSha256 -Path $Item.Source) -ne (Get-FileSha256 -Path $Item.Destination)) {
            throw "Desktop replacement verification failed for '$($Item.RelativePath)'."
        }
        $destinationInfo.LastWriteTimeUtc = $ReleaseTimestampUtc
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Invoke-DesktopDeployment {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DeploymentRoot,
        [Parameter(Mandatory)][datetime]$ReleaseTimestampUtc
    )

    $destinationPath = Join-Path $DeploymentRoot 'desktop'
    if (-not (Test-Path -LiteralPath $destinationPath -PathType Container)) {
        New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    }

    $plan = @(Get-DesktopDeploymentPlan -SourcePath $SourcePath -DestinationPath $destinationPath)
    $newItems = @($plan | Where-Object { $_.Classification -eq 'NEW' })
    $changedItems = @($plan | Where-Object { $_.Classification -eq 'CHANGED' })
    $unchangedItems = @($plan | Where-Object { $_.Classification -eq 'UNCHANGED' })

    foreach ($item in @($newItems + $changedItems)) {
        Write-Host ('{0,-8} {1}' -f $item.Classification, $item.RelativePath)
        Copy-DesktopDeploymentFile -Item $item -ReleaseTimestampUtc $ReleaseTimestampUtc
    }

    Write-Host ''
    Write-Host 'Desktop Deployment'
    Write-Host '--------------------------------'
    Write-Host ('Files scanned : {0}' -f $plan.Count)
    Write-Host ('New           : {0}' -f $newItems.Count)
    Write-Host ('Changed       : {0}' -f $changedItems.Count)
    Write-Host ('Unchanged     : {0}' -f $unchangedItems.Count)
    Write-Host ('Copied        : {0}' -f ($newItems.Count + $changedItems.Count))

    return [pscustomobject]@{
        New = $newItems.Count
        Changed = $changedItems.Count
        Unchanged = $unchangedItems.Count
    }
}
