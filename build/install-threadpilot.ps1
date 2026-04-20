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

Write-Host "Downloading installer $($asset.name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $exePath -UseBasicParsing

# Optional machine PATH update for command-line convenience.
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notmatch [regex]::Escape($destPath)) {
    [Environment]::SetEnvironmentVariable("Path", $currentPath + ";" + $destPath, "Machine")
    Write-Host "Added install folder to machine PATH."
}

Write-Host "Running installer silently..."
Start-Process -FilePath $exePath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-" -Wait

Write-Host "ThreadPilot installation completed successfully." -ForegroundColor Green
