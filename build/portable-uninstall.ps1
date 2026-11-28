param([switch]$Silent, [switch]$PurgeData, [switch]$KeepWindowsChanges, [switch]$KeepData)

$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try {
        [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')
    } finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$manifest = Join-Path $root 'uninstall-files.json'
$executable = Join-Path $root 'ThreadPilot.exe'
$files = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$binary = $files | Where-Object Path -eq 'ThreadPilot.exe'
if (!$binary -or (Get-Sha256 $executable) -ne $binary.Sha256) {
    throw 'ThreadPilot.exe does not match this portable package.'
}

$arguments = @('--prepare-uninstall')
if ($Silent) { $arguments += '--silent' }
if ($KeepData -and !$PurgeData) { $arguments += '--keep-data' }
if ($KeepWindowsChanges) { $arguments += '--keep-windows-changes' }
$process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $root -Wait -PassThru
if ($process.ExitCode -notin 0, 1) { exit $process.ExitCode }

$rootPrefix = $root.TrimEnd('\') + '\'
$left = @()
foreach ($file in $files) {
    try {
        $path = [IO.Path]::GetFullPath([IO.Path]::Combine($root, [string]$file.Path))
        if (!$path.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside the package' }
        if (!(Test-Path -LiteralPath $path)) { continue }

        for ($current = $path; $current; $current = [IO.Path]::GetDirectoryName($current)) {
            if ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked path' }
        }

        if ((Get-Sha256 $path) -ne $file.Sha256) { throw 'File changed since extraction' }
        Remove-Item -LiteralPath $path
    } catch {
        $left += "$($file.Path): $($_.Exception.Message)"
    }
}

if ($left.Count) {
    $left | ForEach-Object { Write-Warning $_ }
    if (!$Silent) {
        Add-Type -AssemblyName PresentationFramework
        [void][System.Windows.MessageBox]::Show(($left -join [Environment]::NewLine), 'Portable files retained')
    }
    exit 1
}

Remove-Item -LiteralPath $manifest, $PSCommandPath
exit $process.ExitCode
