param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$NotesFile = "docs/release/RELEASE_NOTES.md",
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"

function Assert-CommandAvailable {
    param([string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$CommandName' was not found in PATH."
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

Assert-CommandAvailable -CommandName "gh"

$tag = if ($Version.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) { $Version } else { "v$Version" }

$notesPath = Join-Path $projectRoot $NotesFile
if (-not (Test-Path -LiteralPath $notesPath)) {
    throw "Release notes file not found: $notesPath"
}

$artifacts = @()
$artifacts += Get-ChildItem "artifacts/release/packages" -File -ErrorAction SilentlyContinue
$artifacts += Get-ChildItem "artifacts/release/installer" -File -Include *.exe -ErrorAction SilentlyContinue
$artifacts += Get-ChildItem "artifacts/release/msix" -Recurse -File -Include *.msix,*.appx,*.msixbundle,*.appxbundle -ErrorAction SilentlyContinue

$checksums = Join-Path $projectRoot "artifacts/release/SHA256SUMS.txt"
if (Test-Path -LiteralPath $checksums) {
    $artifacts += Get-Item -LiteralPath $checksums
}

if (-not $artifacts -or $artifacts.Count -eq 0) {
    throw "No release artifacts found under artifacts/release."
}

$artifactArgs = @()
foreach ($artifact in $artifacts) {
    $artifactArgs += $artifact.FullName
}

$releaseArgs = @("release", "create", $tag)
$releaseArgs += $artifactArgs
$releaseArgs += @("--notes-file", $notesPath)

if ($Draft) {
    $releaseArgs += "--draft"
}

if ($Prerelease) {
    $releaseArgs += "--prerelease"
}

Write-Host "Creating GitHub release $tag with $($artifacts.Count) artifact(s)..."
& gh @releaseArgs
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed with exit code $LASTEXITCODE"
}

Write-Host "Release created successfully: $tag" -ForegroundColor Green
