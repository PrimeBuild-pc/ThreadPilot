param(
  [Parameter(Mandatory = $true)]
  [string]$Repository,

  [ValidateSet('false_positive', 'used_in_tests', 'wont_fix', 'revoked', 'pattern_deleted')]
  [string]$Resolution = 'used_in_tests',

  [string]$ResolutionComment = 'Scanner vendor artifacts were removed from repository; resolving historical false positives in fix-forward mode.',

  [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-GhApiJson {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Endpoint,

    [string]$Method = 'GET',

    [hashtable]$Fields
  )

  $args = @('api', '-X', $Method, '-H', 'Accept: application/vnd.github+json', $Endpoint)
  if ($Fields) {
    foreach ($entry in $Fields.GetEnumerator()) {
      $args += '-f'
      $args += ('{0}={1}' -f $entry.Key, $entry.Value)
    }
  }

  $raw = (& gh @args 2>&1 | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "gh api call failed for '$Endpoint'. Output: $raw"
  }

  if ([string]::IsNullOrWhiteSpace($raw)) {
    return $null
  }

  try {
    return $raw | ConvertFrom-Json
  }
  catch {
    throw "Unable to parse gh api JSON response for '$Endpoint'. Raw output: $raw"
  }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  throw 'GitHub CLI (gh) is required. Install gh and authenticate with a token that has repo + security_events scope.'
}

$repoPattern = '^[^/]+/[^/]+$'
if ($Repository -notmatch $repoPattern) {
  throw "Repository must be in 'owner/name' format. Received: $Repository"
}

Write-Host "Repository: $Repository"
Write-Host "Resolution: $Resolution"
Write-Host "Mode: $([bool]$Apply ? 'APPLY' : 'DRY-RUN')"

$openAlerts = @()
$page = 1
$perPage = 100

while ($true) {
  $endpoint = "/repos/$Repository/secret-scanning/alerts?state=open&per_page=$perPage&page=$page"
  $batch = Invoke-GhApiJson -Endpoint $endpoint

  if (-not $batch -or $batch.Count -eq 0) {
    break
  }

  $openAlerts += $batch
  if ($batch.Count -lt $perPage) {
    break
  }

  $page += 1
}

if ($openAlerts.Count -eq 0) {
  Write-Host 'No open secret scanning alerts found.'
  exit 0
}

Write-Host "Open alerts found: $($openAlerts.Count)"

$summary = $openAlerts |
  Select-Object number, secret_type, state, created_at, html_url |
  Sort-Object number

$summary | Format-Table -AutoSize

if (-not $Apply) {
  Write-Host ''
  Write-Host 'Dry-run only. Re-run with -Apply to resolve all open alerts listed above.'
  exit 0
}

$resolved = 0
foreach ($alert in $openAlerts) {
  $alertNumber = $alert.number
  $patchEndpoint = "/repos/$Repository/secret-scanning/alerts/$alertNumber"

  Invoke-GhApiJson -Endpoint $patchEndpoint -Method 'PATCH' -Fields @{
    state = 'resolved'
    resolution = $Resolution
    resolution_comment = $ResolutionComment
  } | Out-Null

  $resolved += 1
  Write-Host "Resolved alert #$alertNumber"
}

Write-Host ''
Write-Host "Completed. Resolved alerts: $resolved"
