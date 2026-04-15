$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$url64 = 'https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.1.2/ThreadPilot_v1.1.2_Setup.exe'
$checksum64 = 'fac3935ba126b01987b15cb323502ee8b6e91c957f22006bd9e62b8d57169790'

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    url64bit       = $url64
    checksum64     = $checksum64
    checksumType64 = 'sha256'
    softwareName   = 'ThreadPilot*'
    silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    validExitCodes = @(0)
}

Install-ChocolateyPackage @packageArgs
