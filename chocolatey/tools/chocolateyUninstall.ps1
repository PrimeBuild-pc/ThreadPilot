$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$softwareName = 'ThreadPilot*'

$uninstallKey = Get-UninstallRegistryKey -SoftwareName $softwareName | Select-Object -First 1
if ($null -eq $uninstallKey) {
    Write-Host "$packageName has already been uninstalled by other means."
    return
}

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    file           = $uninstallKey.UninstallString
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    validExitCodes = @(0)
}

Uninstall-ChocolateyPackage @packageArgs
