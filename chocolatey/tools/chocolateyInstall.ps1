$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Installer URL and checksum for ThreadPilot v1.1.2
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.1.2/ThreadPilot_v1.1.2_Setup.exe'
$checksum64 = '326864ead100edd134f3a41f5d4631fcb1fdf6a689101bb0c2d4b7330b9cf032'

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    url64bit       = $url64
    checksum64     = $checksum64
    checksumType64 = 'sha256'
    softwareName   = 'ThreadPilot*'
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS'
    validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
