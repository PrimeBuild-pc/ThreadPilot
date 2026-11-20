param(
    [ValidatePattern('^v?[0-9][0-9A-Za-z.+-]*$')]
    [string]$Version = "1.7.9",
    [string]$PublishDirectory = "artifacts/release/singlefile",
    [string]$OutputDirectory = "artifacts/release/packages"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot

try {
    $publish = (Resolve-Path -LiteralPath $PublishDirectory).ProviderPath
    if (!(Test-Path -LiteralPath (Join-Path $publish "ThreadPilot.exe") -PathType Leaf)) {
        throw "Publish directory does not contain ThreadPilot.exe: $publish"
    }

    $null = New-Item -ItemType Directory -Path $OutputDirectory -Force
    $output = (Resolve-Path -LiteralPath $OutputDirectory).ProviderPath
    if ($output.Equals($publish, [StringComparison]::OrdinalIgnoreCase) -or
        $output.StartsWith($publish.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "ZIP output must be outside the publish directory."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $tag = "v" + $Version.TrimStart('v')
    $portableZip = Join-Path $output ("ThreadPilot_{0}_Portable.zip" -f $tag)
    $temporaryZip = Join-Path $output ("portable-" + [Guid]::NewGuid().ToString("N") + ".zip")

    try {
        [IO.Compression.ZipFile]::CreateFromDirectory($publish, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
        $archive = [IO.Compression.ZipFile]::Open($temporaryZip, [IO.Compression.ZipArchiveMode]::Update)
        try {
            $extras = [ordered]@{
                "uninstall.bat" = Join-Path $PSScriptRoot "portable-uninstall.bat"
                "LICENSE.md" = Join-Path $projectRoot "LICENSE"
            }
            foreach ($extra in $extras.GetEnumerator()) {
                $existing = $archive.GetEntry($extra.Key)
                if ($existing) { $existing.Delete() }
                $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $extra.Value, $extra.Key)
            }
        } finally {
            $archive.Dispose()
        }

        Move-Item -LiteralPath $temporaryZip -Destination $portableZip -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryZip) {
            Remove-Item -LiteralPath $temporaryZip -Force
        }
    }

    Write-Host "PORTABLE_ZIP=$portableZip"
    Write-Host "PORTABLE_SHA256=$((Get-FileHash -LiteralPath $portableZip -Algorithm SHA256).Hash)"
} finally {
    Pop-Location
}
