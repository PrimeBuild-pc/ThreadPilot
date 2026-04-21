param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$Tag,

  [Parameter(Mandatory = $true)]
  [string]$InstallerPath,

  [Parameter(Mandatory = $true)]
  [string]$ApiKey
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

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
$sourceChocoRoot = Join-Path $projectRoot 'chocolatey'

if (-not (Test-Path -LiteralPath $sourceChocoRoot)) {
  throw "Chocolatey source folder not found: $sourceChocoRoot"
}

$workRoot = Join-Path $env:RUNNER_TEMP 'threadpilot-choco-publish'
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

[xml]$nuspec = Get-Content -LiteralPath $nuspecPath
$nuspec.package.metadata.version = $Version
$nuspec.package.metadata.releaseNotes = $releaseNotesUrl
$nuspec.Save($nuspecPath)

$installScript = Get-Content -LiteralPath $installScriptPath -Raw
$installScript = [regex]::Replace($installScript, "\$url64\s*=\s*'.*?'", "`$url64 = '$installerUrl'")
$installScript = [regex]::Replace($installScript, "\$checksum64\s*=\s*'.*?'", "`$checksum64 = '$hash'")
Set-Content -LiteralPath $installScriptPath -Value $installScript -Encoding Ascii

Push-Location -LiteralPath $workRoot
try {
  & choco pack threadpilot.nuspec --outputdirectory .
  if ($LASTEXITCODE -ne 0) {
    throw 'choco pack failed.'
  }

  $nupkg = Get-ChildItem -LiteralPath $workRoot -Filter 'threadpilot.*.nupkg' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
  if (-not $nupkg) {
    throw 'Chocolatey package was not created.'
  }

  $pushOutput = (& choco push $nupkg.FullName --source 'https://push.chocolatey.org/' --api-key $ApiKey --timeout 2700 2>&1 | Out-String)
  if ($LASTEXITCODE -ne 0) {
    if ($pushOutput -match 'already exists') {
      Write-Host 'Chocolatey package already exists. Treating as successful idempotent publish.'
      exit 0
    }

    throw "choco push failed. Output: $pushOutput"
  }

  Write-Host 'Chocolatey package published successfully.'
}
finally {
  Pop-Location
}
