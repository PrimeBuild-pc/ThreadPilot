param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$Tag,

  [Parameter(Mandatory = $true)]
  [string]$InstallerUrl,

  [Parameter(Mandatory = $true)]
  [string]$InstallerSha256,

  [Parameter(Mandatory = $true)]
  [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
  throw "Version must be in semantic version format X.Y.Z. Actual: $Version"
}

if ($Tag -notmatch '^v\d+\.\d+\.\d+$') {
  throw "Tag must be in format vX.Y.Z. Actual: $Tag"
}

if (-not [Uri]::TryCreate($InstallerUrl, [UriKind]::Absolute, [ref]$null)) {
  throw "InstallerUrl must be an absolute URI. Actual: $InstallerUrl"
}

if ($InstallerSha256 -notmatch '^[A-Fa-f0-9]{64}$') {
  throw 'InstallerSha256 must be a 64-character hexadecimal SHA256 value.'
}

$normalizedSha256 = $InstallerSha256.ToUpperInvariant()
$outputPath = [System.IO.Path]::GetFullPath($OutputRoot)

if (Test-Path -LiteralPath $outputPath) {
  Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$versionManifestPath = Join-Path $outputPath 'PrimeBuild.ThreadPilot.yaml'
$defaultLocaleManifestPath = Join-Path $outputPath 'PrimeBuild.ThreadPilot.locale.en-US.yaml'
$installerManifestPath = Join-Path $outputPath 'PrimeBuild.ThreadPilot.installer.yaml'

$versionManifest = @"
PackageIdentifier: PrimeBuild.ThreadPilot
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.4.0
"@

$defaultLocaleManifest = @"
PackageIdentifier: PrimeBuild.ThreadPilot
PackageVersion: $Version
PackageLocale: en-US
Publisher: Prime Build
PublisherUrl: https://github.com/PrimeBuild-pc
PublisherSupportUrl: https://github.com/PrimeBuild-pc/ThreadPilot/issues
PackageName: ThreadPilot
PackageUrl: https://github.com/PrimeBuild-pc/ThreadPilot
License: AGPL-3.0-only
LicenseUrl: https://github.com/PrimeBuild-pc/ThreadPilot/blob/main/LICENSE
ShortDescription: Advanced Windows process and power plan manager.
Description: ThreadPilot is a Process Lasso style utility for process affinity, priority, and power plan automation on Windows.
ReleaseNotesUrl: https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/$Tag
Tags:
  - windows
  - process
  - power-plan
  - performance
ManifestType: defaultLocale
ManifestVersion: 1.4.0
"@

$installerManifest = @"
PackageIdentifier: PrimeBuild.ThreadPilot
PackageVersion: $Version
InstallerType: inno
Installers:
  - Architecture: x64
    InstallerUrl: $InstallerUrl
    InstallerSha256: $normalizedSha256
ManifestType: installer
ManifestVersion: 1.4.0
"@

Set-Content -LiteralPath $versionManifestPath -Value $versionManifest -Encoding utf8
Set-Content -LiteralPath $defaultLocaleManifestPath -Value $defaultLocaleManifest -Encoding utf8
Set-Content -LiteralPath $installerManifestPath -Value $installerManifest -Encoding utf8

$expectedFiles = @(
  $versionManifestPath,
  $defaultLocaleManifestPath,
  $installerManifestPath
)

foreach ($file in $expectedFiles) {
  if (-not (Test-Path -LiteralPath $file)) {
    throw "Expected generated manifest was not created: $file"
  }
}

Write-Host "Generated winget manifests for PrimeBuild.ThreadPilot $Version at $outputPath"
