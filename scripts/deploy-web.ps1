Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-IisCredential {
    $userName = Get-OptionalSetting -Name 'MAP_WEB_IIS_USER'
    $password = Get-OptionalSetting -Name 'MAP_WEB_IIS_PASSWORD'
    if ([string]::IsNullOrWhiteSpace($userName) -and [string]::IsNullOrWhiteSpace($password)) { return $null }
    if ([string]::IsNullOrWhiteSpace($userName) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'MAP_WEB_IIS_USER and MAP_WEB_IIS_PASSWORD must be configured together.'
    }
    return [pscredential]::new($userName, (ConvertTo-SecureString $password -AsPlainText -Force))
}

function Invoke-IisCommand {
    param([Parameter(Mandatory)][scriptblock]$ScriptBlock, [Parameter(Mandatory)][string]$Server, [Parameter(Mandatory)][string]$AppPool)
    $parameters = @{ ComputerName = $Server; ScriptBlock = $ScriptBlock; ArgumentList = @($AppPool); ErrorAction = 'Stop' }
    $credential = Get-IisCredential
    if ($null -ne $credential) { $parameters.Credential = $credential }
    Invoke-Command @parameters
}

function Test-ReleaseDeploymentPreflight {
    param(
        [Parameter(Mandatory)][string]$DesktopArtifactPath,
        [Parameter(Mandatory)][string]$WebArtifactPath
    )

    Test-ReleaseArtifacts -DesktopArtifactPath $DesktopArtifactPath -WebArtifactPath $WebArtifactPath
    $desktopRoot = Get-RequiredSetting -Name 'MAP_DESKTOP_DEPLOY_PATH'
    foreach ($setting in @('MAP_WEB_DEPLOY_URL', 'MAP_WEB_DEPLOY_DEST', 'MAP_WEB_DEPLOY_USER', 'MAP_WEB_DEPLOY_PASSWORD', 'MAP_WEB_IIS_SERVER', 'MAP_WEB_IIS_APP_POOL')) {
        $null = Get-RequiredSetting -Name $setting
    }
    $null = Get-IisCredential

    $msdeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
    if (-not (Test-Path -LiteralPath $msdeploy -PathType Leaf)) { throw 'msdeploy.exe was not found in the expected Web Deploy V3 location.' }
    if (-not (Test-Path -LiteralPath $desktopRoot -PathType Container)) { throw 'Desktop deployment root is not reachable.' }
    New-Item -ItemType Directory -Path (Join-Path $desktopRoot 'desktop') -Force | Out-Null

    $server = Get-RequiredSetting -Name 'MAP_WEB_IIS_SERVER'
    $appPool = Get-RequiredSetting -Name 'MAP_WEB_IIS_APP_POOL'
    Invoke-IisCommand -Server $server -AppPool $appPool -ScriptBlock {
        param($poolName)
        Import-Module WebAdministration -ErrorAction Stop
        if ($null -eq (Get-WebAppPoolState -Name $poolName -ErrorAction SilentlyContinue)) {
            throw "Configured IIS application pool '$poolName' was not found."
        }
    }
}

function Invoke-WebDeployment {
    param([Parameter(Mandatory)][string]$WebArtifactPath)

    $server = Get-RequiredSetting -Name 'MAP_WEB_IIS_SERVER'
    $appPool = Get-RequiredSetting -Name 'MAP_WEB_IIS_APP_POOL'
    $deployUrl = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_URL'
    $deployDestination = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_DEST'
    $deployUser = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_USER'
    $deployPassword = Get-RequiredSetting -Name 'MAP_WEB_DEPLOY_PASSWORD'
    $msdeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
    $webContentPath = Get-WebDeployContentPath -WebArtifactPath $WebArtifactPath
    $poolStopped = $false
    $deploymentError = $null
    $restartError = $null

    try {
        Write-Host 'Web AppPool ........ STOPPING'
        Invoke-IisCommand -Server $server -AppPool $appPool -ScriptBlock { param($poolName) Import-Module WebAdministration -ErrorAction Stop; Stop-WebAppPool -Name $poolName -ErrorAction Stop }
        $poolStopped = $true

        $destinationArgument = '-dest:contentPath="{0}",computerName="{1}",userName="{2}",password="{3}",authType=basic' -f $deployDestination, $deployUrl, $deployUser, $deployPassword
        Invoke-ExternalCommand -FilePath $msdeploy -ArgumentList @('-verb:sync', ('-source:contentPath="{0}"' -f $webContentPath), $destinationArgument, '-allowUntrusted') -Description 'Web Deploy' -SensitiveValues @($deployPassword)
    }
    catch { $deploymentError = $_ }
    finally {
        if ($poolStopped) {
            try {
                Invoke-IisCommand -Server $server -AppPool $appPool -ScriptBlock { param($poolName) Import-Module WebAdministration -ErrorAction Stop; Start-WebAppPool -Name $poolName -ErrorAction Stop }
                Write-Host 'Web AppPool ........ RUNNING'
            }
            catch { $restartError = $_ }
        }
    }

    if ($null -ne $deploymentError) { throw $deploymentError }
    if ($null -ne $restartError) { throw "Web deployment succeeded but restarting the IIS application pool failed: $($restartError.Exception.Message)" }
}
