param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$ManifestSourcePath,

  [Parameter(Mandatory = $true)]
  [string]$ForkOwner,

  [Parameter(Mandatory = $true)]
  [string]$RepositoryOwner,

  [Parameter(Mandatory = $true)]
  [string]$PackageIdentifier,

  [Parameter(Mandatory = $true)]
  [string]$GithubToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-CommandAvailable {
  param([string]$CommandName)

  if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
    throw "Required command '$CommandName' was not found in PATH."
  }
}

Assert-CommandAvailable -CommandName 'git'
Assert-CommandAvailable -CommandName 'gh'

if (-not (Test-Path -LiteralPath $ManifestSourcePath)) {
  throw "Manifest source path not found: $ManifestSourcePath"
}

$manifestFiles = Get-ChildItem -LiteralPath $ManifestSourcePath -Recurse -File -Filter '*.yaml'
if (-not $manifestFiles -or $manifestFiles.Count -eq 0) {
  throw "No winget manifest files found under $ManifestSourcePath"
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
$workDir = Join-Path $env:RUNNER_TEMP 'winget-pkgs-work'

if (Test-Path -LiteralPath $workDir) {
  Remove-Item -LiteralPath $workDir -Recurse -Force
}

$env:GH_TOKEN = $GithubToken

Write-Host 'Cloning winget-pkgs fork...'
& git clone "https://x-access-token:$GithubToken@github.com/$ForkOwner/winget-pkgs.git" $workDir
if ($LASTEXITCODE -ne 0) {
  throw 'Failed to clone winget-pkgs fork.'
}

Push-Location -LiteralPath $workDir
try {
  & git config user.name 'ThreadPilot Release Bot'
  & git config user.email 'threadpilot-release-bot@users.noreply.github.com'

  $branch = "threadpilot-$Version"
  & git checkout -B $branch

  $packageParts = $PackageIdentifier.Split('.')
  if ($packageParts.Count -lt 2) {
    throw "PackageIdentifier must contain at least one dot: $PackageIdentifier"
  }

  $packagePublisher = $packageParts[0]
  $packageName = $packageParts[1]

  if ($RepositoryOwner -ne $packagePublisher) {
    throw "RepositoryOwner '$RepositoryOwner' does not match package publisher '$packagePublisher'."
  }

  $firstLetter = $packagePublisher.Substring(0, 1).ToLowerInvariant()
  $targetDir = Join-Path $workDir (Join-Path 'manifests' (Join-Path $firstLetter (Join-Path $packagePublisher (Join-Path $packageName $Version))))

  if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
  }

  New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

  foreach ($file in $manifestFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetDir $file.Name) -Force
  }

  & git add $targetDir
  & git status --short

  $hasChanges = (& git diff --cached --name-only | Out-String).Trim()
  if ([string]::IsNullOrWhiteSpace($hasChanges)) {
    Write-Host "No manifest changes detected for $Version. Nothing to submit."
    exit 0
  }

  & git commit -m "Add $PackageIdentifier version $Version"
  & git push -u origin $branch --force

  $existingPr = (& gh pr list --repo microsoft/winget-pkgs --state open --head "$ForkOwner`:$branch" --json number | Out-String).Trim()
  if (-not [string]::IsNullOrWhiteSpace($existingPr) -and $existingPr -ne '[]') {
    Write-Host "Winget PR already exists for branch $branch."
    exit 0
  }

  $title = "New version: $PackageIdentifier version $Version"
  $body = @"
Automated submission from ThreadPilot release workflow.

- Package: $PackageIdentifier
- Version: $Version
"@

  & gh pr create --repo microsoft/winget-pkgs --base master --head "$ForkOwner`:$branch" --title $title --body $body
  if ($LASTEXITCODE -ne 0) {
    throw 'Failed to create winget-pkgs PR.'
  }

  Write-Host "Winget PR submitted successfully for $PackageIdentifier $Version"
}
finally {
  Pop-Location
}
