$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Installer URL and checksum for ThreadPilot v1.1.2
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.1.2/ThreadPilot_v1.1.2_Setup.exe'
$checksum64 = 'FAC3935BA126B01987B15CB323502EE8B6E91C957F22006BD9E62B8D57169790'

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
