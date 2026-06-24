$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Installer URL and checksum for ThreadPilot v1.4.1
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.1/ThreadPilot_v1.4.1_Setup.exe'
$checksum64 = '0f635e1d5eb1447f618edd749bc0d31d733e60f09f669a5122b2f37d8a9e3f62'

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
