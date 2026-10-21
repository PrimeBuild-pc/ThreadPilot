$ErrorActionPreference = "Stop"

# Verify administrator permissions for machine-wide installation changes.
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Please run this command in a PowerShell window opened as Administrator."
    exit 1
}

Write-Host "Starting ThreadPilot installation..." -ForegroundColor Cyan

$releaseInfoUrl = "https://api.github.com/repos/PrimeBuild-pc/ThreadPilot/releases/latest"
$destPath = "$env:ProgramFiles\ThreadPilot"

if (-not (Test-Path -LiteralPath $destPath)) {
    New-Item -ItemType Directory -Force -Path $destPath | Out-Null
}

Write-Host "Resolving latest ThreadPilot release..."
$release = Invoke-RestMethod -Uri $releaseInfoUrl
$asset = $release.assets | Where-Object { $_.name -like "*Setup.exe" } | Select-Object -First 1

if (-not $asset) {
    throw "Could not find Setup.exe asset in the latest GitHub release."
}

$exePath = Join-Path $destPath $asset.name

$checksumAsset = $release.assets | Where-Object { $_.name -eq "SHA256SUMS.txt" } | Select-Object -First 1
if (-not $checksumAsset) {
    throw "The latest release has no SHA256SUMS.txt, so the installer cannot be verified."
}

Write-Host "Downloading installer $($asset.name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $exePath -UseBasicParsing

$checksums = (Invoke-WebRequest -Uri $checksumAsset.browser_download_url -UseBasicParsing).Content
$expected = ($checksums -split "`n" | Where-Object { $_ -match "^([0-9a-fA-F]{64})\s+\*?$([regex]::Escape($asset.name))\s*$" } | ForEach-Object { $Matches[1] } | Select-Object -First 1)
$actual = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash
if (-not $expected -or $actual -ne $expected) {
    Remove-Item -LiteralPath $exePath -Force
    throw "Installer checksum does not match SHA256SUMS.txt; the download was deleted."
}

# Optional machine PATH update for command-line convenience.
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notmatch [regex]::Escape($destPath)) {
    [Environment]::SetEnvironmentVariable("Path", $currentPath + ";" + $destPath, "Machine")
    Write-Host "Added install folder to machine PATH."
}

Write-Host "Running installer silently..."
Start-Process -FilePath $exePath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-" -Wait

Write-Host "ThreadPilot installation completed successfully." -ForegroundColor Green
