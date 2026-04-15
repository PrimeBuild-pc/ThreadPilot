$ErrorActionPreference = "Stop"

$forbiddenPatterns = @(
    "*.log",
    "*.pdb",
    "*.tmp",
    "*.temp"
)

$forbiddenDirectories = @(
    ".kilo/",
    ".roo/",
    ".cursor/",
    ".aider",
    "__pycache__/",
    "checkpoint"
)

$maxFileSizeBytes = 100MB
$stagedFiles = git diff --cached --name-only --diff-filter=ACM

if (-not $stagedFiles) {
    exit 0
}

$errors = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $stagedFiles) {
    $normalizedPath = $relativePath.Replace('\\', '/').ToLowerInvariant()

    foreach ($forbiddenDirectory in $forbiddenDirectories) {
        if ($normalizedPath.Contains($forbiddenDirectory.TrimEnd('/').ToLowerInvariant())) {
            $errors.Add("Forbidden artifact path staged: $relativePath")
            break
        }
    }

    foreach ($pattern in $forbiddenPatterns) {
        if ($relativePath -like $pattern) {
            $errors.Add("Forbidden file pattern staged: $relativePath")
            break
        }
    }

    if (-not (Test-Path -LiteralPath $relativePath)) {
        continue
    }

    $fileInfo = Get-Item -LiteralPath $relativePath -ErrorAction SilentlyContinue
    if ($null -ne $fileInfo -and -not $fileInfo.PSIsContainer -and $fileInfo.Length -gt $maxFileSizeBytes) {
        $sizeMb = [Math]::Round($fileInfo.Length / 1MB, 2)
        $errors.Add("File exceeds 100MB limit: $relativePath ($sizeMb MB)")
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Pre-commit checks failed:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host " - $error" -ForegroundColor Red
    }

    Write-Host "Commit aborted. Clean staged artifacts and retry." -ForegroundColor Yellow
    exit 1
}

exit 0
