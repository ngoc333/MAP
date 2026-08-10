Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-WebDeployServerName {
    param([Parameter(Mandatory)][string]$DeployUrl)

    try {
        return ([System.Uri]$DeployUrl).Host
    }
    catch {
        return $DeployUrl
    }
}

function Test-ReleaseDeploymentPreflight {
    param(
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath
    )

    Test-ReleaseArtifacts -DesktopArtifactPath $DesktopArtifactPath -WebArtifactPath $WebArtifactPath

    $desktopDeploymentRoot = Get-RequiredSetting -Name 'MAP_DESKTOP_DEPLOY_PATH'
    if (-not (Test-Path -LiteralPath $desktopDeploymentRoot -PathType Container)) {
        throw 'Desktop deployment root is not reachable.'
    }

    $msdeployPath = Get-MsDeployPath
    if (-not (Test-Path -LiteralPath $msdeployPath -PathType Leaf)) {
        throw 'msdeploy.exe was not found in the expected Web Deploy V3 location.'
    }

    foreach ($settingName in @(
        'MAP_WEB_DEPLOY_URL',
        'MAP_WEB_DEPLOY_DEST',
        'MAP_WEB_DEPLOY_USER',
        'MAP_WEB_DEPLOY_PASSWORD')) {
        $null = Get-RequiredSetting -Name $settingName
    }
}

function Invoke-WebDeployment {
    param([Parameter(Mandatory)][string]$WebArtifactPath)

    $deployUrl = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_URL'
    $deployDestination = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_DEST'
    $deployUser = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_USER'
    $deployPassword = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_PASSWORD'
    $msdeployPath = Get-MsDeployPath
    $webContentPath = Get-WebDeployContentPath -WebArtifactPath $WebArtifactPath

    # Use the same ProcessStartInfo invocation shape as deploy.ps1. The
    # argument string is passed directly to msdeploy (not through a shell),
    # so characters such as @, #, ! and $ remain part of the password.
    $arguments = '-verb:sync -source:contentPath="{0}" -dest:contentPath={1},computerName={2},userName={3},password={4},authType=Basic -allowUntrusted' -f `
        $webContentPath, $deployDestination, $deployUrl, $deployUser, $deployPassword

    Write-Host 'Web Deployment'
    Write-Host '--------------------------------'
    Write-Host ('Source        : {0}' -f $webContentPath)
    Write-Host ('Destination   : {0}' -f $deployDestination)
    Write-Host ('Server        : {0}' -f (Get-WebDeployServerName -DeployUrl $deployUrl))

    Invoke-ExternalProcess `
        -FilePath $msdeployPath `
        -Arguments $arguments `
        -Description 'Web Deploy' `
        -SensitiveValues @($deployPassword)

    Write-Success 'Result        : SUCCESS'
}
