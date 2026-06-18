$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Installer URL and checksum for ThreadPilot v1.4.0
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Setup.exe'
$checksum64 = '3280bb39258d6bc9d16537f07f0ed1017f9d67b675c62c8b86d53c9d8a4a1ad5'

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
