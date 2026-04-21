$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# These placeholders are replaced during pack/publish automation.
$url64 = '__INSTALLER_URL__'
$checksum64 = '__INSTALLER_SHA256__'

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    url64bit       = $url64
    checksum64     = $checksum64
    checksumType64 = 'sha256'
    softwareName   = 'ThreadPilot*'
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS=0'
    validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
