param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$targets = @(
    ".kilo",
    ".roo",
    ".cursor",
    "__pycache__"
)

$checkpointTargets = Get-ChildItem -Path . -Directory -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "checkpoint*" }
$pathsToRemove = @()
$pathsToRemove += $targets
$pathsToRemove += ($checkpointTargets | ForEach-Object { $_.FullName })

foreach ($target in $pathsToRemove) {
    if (-not (Test-Path -LiteralPath $target)) {
        continue
    }

    if ($DryRun) {
        Write-Host "[DryRun] Would remove: $target"
        continue
    }

    Remove-Item -LiteralPath $target -Recurse -Force
    Write-Host "Removed: $target"
}

Write-Host "Repository cleanup completed." -ForegroundColor Green
