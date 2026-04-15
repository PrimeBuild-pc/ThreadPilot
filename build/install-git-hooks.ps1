$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

$hooksPath = ".githooks"
if (-not (Test-Path -LiteralPath $hooksPath)) {
    throw "Hooks directory not found: $hooksPath"
}

git config core.hooksPath $hooksPath
if ($LASTEXITCODE -ne 0) {
    throw "Failed to configure git hooksPath"
}

Write-Host "Configured git hooks path to '$hooksPath'." -ForegroundColor Green
Write-Host "Pre-commit hook is now active for this repository." -ForegroundColor Green
