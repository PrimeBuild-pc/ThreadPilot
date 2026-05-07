$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Installer URL and checksum for ThreadPilot v1.1.2
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.1.2/ThreadPilot_v1.1.2_Setup.exe'
$checksum64 = '5ee64d2268cc8b8c6378d403c098ba4c22e5017b65a0ca58c45c46341b220f01'

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    url64bit       = $url64
    checksum64     = $checksum64
    checksumType64 = 'sha256'
    softwareName   = 'ThreadPilot*'
    silentArgs     = '/VERYSILENT /SP- /NORESTART'
    validExitCodes = @(0, 1, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
