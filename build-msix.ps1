param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ProjectDir = (Get-Location).Path,
    [string]$PackageIdentityName = "PrimeBuild.ThreadPilot",
    [string]$PackagePublisher = "CN=ThreadPilot",
    [string]$ExecutableName = "ThreadPilot.exe",
    [string]$DisplayName = "ThreadPilot",
    [string]$PublisherDisplayName = "Prime Build",
    [string]$Description = "ThreadPilot process and power plan manager",
    [string]$MinWindowsVersion = "10.0.19041.0",
    [string]$MaxWindowsVersionTested = "10.0.26100.0",
    [switch]$KeepStaging
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ProjectDir $Path))
}

function Convert-ToMsixVersion {
    param([Parameter(Mandatory = $true)][string]$RawVersion)

    $parts = $RawVersion.Split('.') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    $normalized = @()

    foreach ($part in $parts) {
        $numericPart = ($part -replace '[^0-9]', '')
        if ([string]::IsNullOrWhiteSpace($numericPart)) {
            $numericPart = '0'
        }

        $value = [int]$numericPart
        if ($value -lt 0) {
            $value = 0
        }
        if ($value -gt 65535) {
            $value = 65535
        }

        $normalized += $value
    }

    while ($normalized.Count -lt 4) {
        $normalized += 0
    }

    return "{0}.{1}.{2}.{3}" -f $normalized[0], $normalized[1], $normalized[2], $normalized[3]
}

function Find-MakeAppxPath {
    if (-not [string]::IsNullOrWhiteSpace($env:MAKEAPPX_EXE) -and (Test-Path -LiteralPath $env:MAKEAPPX_EXE)) {
        return $env:MAKEAPPX_EXE
    }

    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        throw "Windows SDK was not found at '$kitsRoot'. Install Windows 10/11 SDK to enable MSIX packaging."
    }

    $candidate = Get-ChildItem -LiteralPath $kitsRoot -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\makeappx.exe" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1

    if (-not $candidate) {
        throw "makeappx.exe not found under Windows SDK bin folders."
    }

    return $candidate
}

function New-AppLogo {
    param(
        [Parameter(Mandatory = $true)][int]$Size,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 22, 112, 214))
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

            $fontSize = [Math]::Max(12.0, $Size * 0.42)
            $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            try {
                $stringFormat = New-Object System.Drawing.StringFormat
                try {
                    $stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
                    $stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
                    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
                    try {
                        $graphics.DrawString("TP", $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $Size, $Size), $stringFormat)
                        $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
                    }
                    finally {
                        $brush.Dispose()
                    }
                }
                finally {
                    $stringFormat.Dispose()
                }
            }
            finally {
                $font.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }
    }
    finally {
        $bmp.Dispose()
    }
}

$publishPath = Resolve-AbsolutePath -Path $PublishDir
$outputPath = Resolve-AbsolutePath -Path $OutputDir
$projectPath = Resolve-AbsolutePath -Path $ProjectDir

if (-not (Test-Path -LiteralPath $publishPath)) {
    throw "Publish directory not found: $publishPath"
}

$executablePath = Join-Path $publishPath $ExecutableName
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Expected executable '$ExecutableName' was not found in publish directory: $publishPath"
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$stagingPath = Join-Path $outputPath "_msix_stage"
if (Test-Path -LiteralPath $stagingPath) {
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
Copy-Item -Path (Join-Path $publishPath "*") -Destination $stagingPath -Recurse -Force

$assetsPath = Join-Path $stagingPath "Assets"
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null

Add-Type -AssemblyName System.Drawing

$square44 = Join-Path $assetsPath "Square44x44Logo.png"
$square150 = Join-Path $assetsPath "Square150x150Logo.png"
$storeLogo = Join-Path $assetsPath "StoreLogo.png"

New-AppLogo -Size 44 -OutputPath $square44
New-AppLogo -Size 150 -OutputPath $square150
New-AppLogo -Size 50 -OutputPath $storeLogo

$msixVersion = Convert-ToMsixVersion -RawVersion $Version
$manifestPath = Join-Path $stagingPath "AppxManifest.xml"

$manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
    <Identity Name="$PackageIdentityName" Publisher="$PackagePublisher" Version="$msixVersion" ProcessorArchitecture="x64" />
    <Properties>
        <DisplayName>$DisplayName</DisplayName>
        <PublisherDisplayName>$PublisherDisplayName</PublisherDisplayName>
        <Description>$Description</Description>
        <Logo>Assets\StoreLogo.png</Logo>
    </Properties>
    <Dependencies>
        <TargetDeviceFamily Name="Windows.Desktop" MinVersion="$MinWindowsVersion" MaxVersionTested="$MaxWindowsVersionTested" />
    </Dependencies>
    <Resources>
        <Resource Language="en-us" />
    </Resources>
    <Applications>
        <Application Id="ThreadPilot" Executable="$ExecutableName" EntryPoint="Windows.FullTrustApplication">
            <uap:VisualElements DisplayName="$DisplayName" Description="$Description" BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png" Square44x44Logo="Assets\Square44x44Logo.png" />
        </Application>
    </Applications>
    <Capabilities>
        <rescap:Capability Name="runFullTrust" />
    </Capabilities>
</Package>
"@

[System.IO.File]::WriteAllText($manifestPath, $manifestContent, [System.Text.UTF8Encoding]::new($false))

$makeAppx = Find-MakeAppxPath
$msixFileName = "ThreadPilot_{0}_win-x64.msix" -f $msixVersion
$msixPath = Join-Path $outputPath $msixFileName

if (Test-Path -LiteralPath $msixPath) {
    Remove-Item -LiteralPath $msixPath -Force
}

Write-Host "Creating MSIX package at: $msixPath"
& $makeAppx pack /d $stagingPath /p $msixPath /o

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msixPath)) {
    throw "MSIX packaging failed. Expected artifact not found: $msixPath"
}

if (-not $KeepStaging) {
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}

Write-Host "MSIX package created successfully: $msixPath"