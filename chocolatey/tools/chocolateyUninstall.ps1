$ErrorActionPreference = 'Stop'

$packageName = 'threadpilot'
$softwareName = 'ThreadPilot*'

$uninstallKey = Get-UninstallRegistryKey -SoftwareName $softwareName | Select-Object -First 1
if ($null -eq $uninstallKey) {
    Write-Host "$packageName has already been uninstalled by other means."
    return
}

$uninstallString = $uninstallKey.UninstallString
if ([string]::IsNullOrWhiteSpace($uninstallString)) {
    throw "Uninstall string not found for $softwareName"
}

$exePath = $uninstallString.Trim()
$baseArgs = ''

if ($exePath.StartsWith('"')) {
    $endQuote = $exePath.IndexOf('"', 1)
    if ($endQuote -lt 0) {
        throw "Malformed uninstall string: $uninstallString"
    }

    $baseArgs = $exePath.Substring($endQuote + 1).Trim()
    $exePath = $exePath.Substring(1, $endQuote - 1)
}
else {
    $parts = $exePath.Split(' ', 2)
    $exePath = $parts[0]
    if ($parts.Count -gt 1) {
        $baseArgs = $parts[1].Trim()
    }
}

$silentFlags = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS=0'
$allArgs = @($baseArgs, $silentFlags) -join ' '
$allArgs = $allArgs.Trim()

$packageArgs = @{
    packageName    = $packageName
    fileType       = 'exe'
    file           = $exePath
    silentArgs     = $allArgs
    validExitCodes = @(0, 3010, 1641)
}

Uninstall-ChocolateyPackage @packageArgs
