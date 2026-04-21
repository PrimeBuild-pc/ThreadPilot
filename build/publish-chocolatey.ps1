param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$Tag,

  [Parameter(Mandatory = $true)]
  [string]$InstallerPath,

  [string]$ApiKey,

  [string]$PackageOutputDirectory,

  [string]$MetadataOutputPath,

  [switch]$DryRun,

  [switch]$SkipPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-CommandAvailable {
  param([string]$CommandName)

  if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
    throw "Required command '$CommandName' was not found in PATH."
  }
}

Assert-CommandAvailable -CommandName 'choco'

if (-not (Test-Path -LiteralPath $InstallerPath)) {
  throw "Installer executable not found: $InstallerPath"
}

if (-not $DryRun -and -not $SkipPush -and [string]::IsNullOrWhiteSpace($ApiKey)) {
  throw "ApiKey is required unless -DryRun or -SkipPush is used."
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
$sourceChocoRoot = Join-Path $projectRoot 'chocolatey'

if (-not (Test-Path -LiteralPath $sourceChocoRoot)) {
  throw "Chocolatey source folder not found: $sourceChocoRoot"
}

$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
  [System.IO.Path]::GetTempPath()
}
else {
  $env:RUNNER_TEMP
}

$workRoot = Join-Path $tempRoot 'threadpilot-choco-publish'
if (Test-Path -LiteralPath $workRoot) {
  Remove-Item -LiteralPath $workRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
Copy-Item -Path (Join-Path $sourceChocoRoot '*') -Destination $workRoot -Recurse -Force

$nuspecPath = Join-Path $workRoot 'threadpilot.nuspec'
$installScriptPath = Join-Path $workRoot 'tools\chocolateyInstall.ps1'

if (-not (Test-Path -LiteralPath $nuspecPath)) {
  throw "Nuspec file not found: $nuspecPath"
}

if (-not (Test-Path -LiteralPath $installScriptPath)) {
  throw "chocolateyInstall.ps1 not found: $installScriptPath"
}

$hash = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$installerFileName = Split-Path -Leaf $InstallerPath
$installerUrl = "https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/$Tag/$installerFileName"
$releaseNotesUrl = "https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/$Tag"
$resolvedPackageOutputDirectory = if ([string]::IsNullOrWhiteSpace($PackageOutputDirectory)) {
  Join-Path $workRoot 'out'
}
else {
  [System.IO.Path]::GetFullPath($PackageOutputDirectory)
}

$resolvedMetadataOutputPath = if ([string]::IsNullOrWhiteSpace($MetadataOutputPath)) {
  $null
}
else {
  [System.IO.Path]::GetFullPath($MetadataOutputPath)
}

New-Item -ItemType Directory -Force -Path $resolvedPackageOutputDirectory | Out-Null

[xml]$nuspec = Get-Content -LiteralPath $nuspecPath
$nuspec.package.metadata.version = $Version
$nuspec.package.metadata.releaseNotes = $releaseNotesUrl
$nuspec.Save($nuspecPath)

$installScript = Get-Content -LiteralPath $installScriptPath -Raw
$installScript = [regex]::Replace($installScript, '\$url64\s*=\s*''.*?''', ('$url64 = ''{0}''' -f $installerUrl))
$installScript = [regex]::Replace($installScript, '\$checksum64\s*=\s*''.*?''', ('$checksum64 = ''{0}''' -f $hash))
Set-Content -LiteralPath $installScriptPath -Value $installScript -Encoding Ascii

Push-Location -LiteralPath $workRoot
try {
  & choco pack threadpilot.nuspec --outputdirectory $resolvedPackageOutputDirectory
  if ($LASTEXITCODE -ne 0) {
    throw 'choco pack failed.'
  }

  $nupkg = Get-ChildItem -LiteralPath $resolvedPackageOutputDirectory -Filter 'threadpilot.*.nupkg' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
  if (-not $nupkg) {
    throw 'Chocolatey package was not created.'
  }

  $publishAttempted = $false
  $publishResult = 'packed'

  if (-not $DryRun -and -not $SkipPush) {
    $publishAttempted = $true
    $pushOutput = (& choco push $nupkg.FullName --source 'https://push.chocolatey.org/' --api-key $ApiKey --timeout 2700 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
      if ($pushOutput -match 'already exists') {
        Write-Host 'Chocolatey package already exists. Treating as successful idempotent publish.'
        $publishResult = 'already-exists'
      }
      else {
        throw "choco push failed. Output: $pushOutput"
      }
    }
    else {
      Write-Host 'Chocolatey package published successfully.'
      $publishResult = 'published'
    }
  }
  else {
    Write-Host 'Chocolatey package validation completed without push.'
    $publishResult = if ($DryRun) { 'dry-run' } else { 'packed-no-push' }
  }

  if (-not [string]::IsNullOrWhiteSpace($resolvedMetadataOutputPath)) {
    $metadataDirectory = Split-Path -Parent $resolvedMetadataOutputPath
    if (-not [string]::IsNullOrWhiteSpace($metadataDirectory)) {
      New-Item -ItemType Directory -Force -Path $metadataDirectory | Out-Null
    }

    @{
      version = $Version
      tag = $Tag
      installerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
      installerUrl = $installerUrl
      installerSha256 = $hash
      packagePath = $nupkg.FullName
      releaseNotesUrl = $releaseNotesUrl
      publishAttempted = $publishAttempted
      publishResult = $publishResult
      dryRun = [bool]$DryRun
      skipPush = [bool]$SkipPush
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resolvedMetadataOutputPath -Encoding utf8
  }
}
finally {
  Pop-Location
}
