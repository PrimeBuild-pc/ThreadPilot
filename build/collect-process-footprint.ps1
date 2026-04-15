param(
    [string]$ProcessName = "ThreadPilot",
    [int]$DurationMinutes = 30,
    [int]$SampleIntervalSeconds = 5,
    [string]$OutputDirectory = "artifacts/perf",
    [switch]$WaitForProcess,
    [int]$WaitTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"

if ($DurationMinutes -lt 1)
{
    throw "DurationMinutes must be >= 1"
}

if ($SampleIntervalSeconds -lt 1)
{
    throw "SampleIntervalSeconds must be >= 1"
}

if ($WaitTimeoutSeconds -lt 1)
{
    throw "WaitTimeoutSeconds must be >= 1"
}

function Get-LatestProcessInstance {
    param(
        [string]$Name
    )

    return Get-Process -Name $Name -ErrorAction SilentlyContinue |
        Sort-Object -Property StartTime -Descending |
        Select-Object -First 1
}

function Wait-ForProcessStart {
    param(
        [string]$Name,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline)
    {
        $candidate = Get-LatestProcessInstance -Name $Name
        if ($candidate)
        {
            return $candidate
        }

        Start-Sleep -Seconds 2
    }

    return $null
}

function Get-MeasureValueOrZero {
    param(
        [double[]]$Values,
        [string]$Metric
    )

    if (-not $Values -or $Values.Count -eq 0)
    {
        return 0
    }

    switch ($Metric)
    {
        "Average"
        {
            return ($Values | Measure-Object -Average).Average
        }

        "Maximum"
        {
            return ($Values | Measure-Object -Maximum).Maximum
        }

        "Minimum"
        {
            return ($Values | Measure-Object -Minimum).Minimum
        }

        default
        {
            throw "Unsupported metric '$Metric'. Use Average, Maximum, or Minimum."
        }
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

$outputRoot = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputPath = Join-Path $outputRoot ("{0}-footprint-{1}.csv" -f $ProcessName.ToLowerInvariant(), $timestamp)

$targetProcess = Get-LatestProcessInstance -Name $ProcessName
if (-not $targetProcess -and $WaitForProcess)
{
    $targetProcess = Wait-ForProcessStart -Name $ProcessName -TimeoutSeconds $WaitTimeoutSeconds
}

if (-not $targetProcess)
{
    throw "Process '$ProcessName' not found. Start the app first, or rerun with -WaitForProcess."
}

$targetProcessId = $targetProcess.Id
$logicalProcessorCount = [Environment]::ProcessorCount
$sessionStartUtc = [DateTime]::UtcNow
$sessionEndUtc = $sessionStartUtc.AddMinutes($DurationMinutes)

Write-Host "Sampling process '$ProcessName' (PID: $targetProcessId)"
Write-Host "Duration: $DurationMinutes minute(s), Interval: $SampleIntervalSeconds second(s)"
Write-Host "Output: $outputPath"

$csvInitialized = $false
$sampleCount = 0
$previousSampleUtc = [DateTime]::UtcNow
$previousCpuTotalSeconds = $targetProcess.TotalProcessorTime.TotalSeconds
$previousWorkingSetMb = [Math]::Round($targetProcess.WorkingSet64 / 1MB, 2)

while ([DateTime]::UtcNow -lt $sessionEndUtc)
{
    Start-Sleep -Seconds $SampleIntervalSeconds

    $process = Get-Process -Id $targetProcessId -ErrorAction SilentlyContinue
    if (-not $process)
    {
        Write-Warning "Process PID $targetProcessId exited before session end."
        break
    }

    $sampleUtc = [DateTime]::UtcNow
    $elapsedSeconds = [Math]::Max(0.001, ($sampleUtc - $previousSampleUtc).TotalSeconds)
    $cpuTotalSeconds = $process.TotalProcessorTime.TotalSeconds
    $deltaCpuSeconds = [Math]::Max(0.0, $cpuTotalSeconds - $previousCpuTotalSeconds)

    $cpuPercentSingleCore = [Math]::Round(($deltaCpuSeconds / $elapsedSeconds) * 100.0, 2)
    $cpuPercentOverall = [Math]::Round($cpuPercentSingleCore / $logicalProcessorCount, 2)
    $workingSetMb = [Math]::Round($process.WorkingSet64 / 1MB, 2)
    $privateMemoryMb = [Math]::Round($process.PrivateMemorySize64 / 1MB, 2)
    $workingSetDeltaMb = [Math]::Round($workingSetMb - $previousWorkingSetMb, 2)

    $row = [PSCustomObject]@{
        TimestampUtc = $sampleUtc.ToString("o")
        ProcessId = $process.Id
        ProcessName = $process.ProcessName
        WorkingSetMB = $workingSetMb
        WorkingSetDeltaMB = $workingSetDeltaMb
        PrivateMemoryMB = $privateMemoryMb
        HandleCount = $process.HandleCount
        ThreadCount = $process.Threads.Count
        CpuPercentSingleCore = $cpuPercentSingleCore
        CpuPercentOverall = $cpuPercentOverall
        CpuTotalSeconds = [Math]::Round($cpuTotalSeconds, 2)
        LogicalProcessorCount = $logicalProcessorCount
    }

    if (-not $csvInitialized)
    {
        $row | Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding Ascii
        $csvInitialized = $true
    }
    else
    {
        $row | Export-Csv -LiteralPath $outputPath -NoTypeInformation -Append -Encoding Ascii
    }

    $sampleCount++
    $previousSampleUtc = $sampleUtc
    $previousCpuTotalSeconds = $cpuTotalSeconds
    $previousWorkingSetMb = $workingSetMb
}

if (-not (Test-Path -LiteralPath $outputPath))
{
    throw "No output file produced."
}

$samples = Import-Csv -LiteralPath $outputPath
if (-not $samples -or $samples.Count -eq 0)
{
    throw "No samples were collected."
}

$workingSetSeries = $samples | ForEach-Object { [double]$_.WorkingSetMB }
$privateMemorySeries = $samples | ForEach-Object { [double]$_.PrivateMemoryMB }
$cpuSingleCoreSeries = $samples | ForEach-Object { [double]$_.CpuPercentSingleCore }
$handleCountSeries = $samples | ForEach-Object { [double]$_.HandleCount }

$actualDurationMinutes = [Math]::Round((([DateTime]::UtcNow) - $sessionStartUtc).TotalMinutes, 2)

$summary = [PSCustomObject]@{
    ProcessName = $ProcessName
    ProcessId = $targetProcessId
    Samples = $samples.Count
    DurationMinutes = $actualDurationMinutes
    AvgWorkingSetMB = [Math]::Round((Get-MeasureValueOrZero -Values $workingSetSeries -Metric "Average"), 2)
    PeakWorkingSetMB = [Math]::Round((Get-MeasureValueOrZero -Values $workingSetSeries -Metric "Maximum"), 2)
    AvgPrivateMemoryMB = [Math]::Round((Get-MeasureValueOrZero -Values $privateMemorySeries -Metric "Average"), 2)
    PeakPrivateMemoryMB = [Math]::Round((Get-MeasureValueOrZero -Values $privateMemorySeries -Metric "Maximum"), 2)
    AvgCpuPercentSingleCore = [Math]::Round((Get-MeasureValueOrZero -Values $cpuSingleCoreSeries -Metric "Average"), 2)
    PeakCpuPercentSingleCore = [Math]::Round((Get-MeasureValueOrZero -Values $cpuSingleCoreSeries -Metric "Maximum"), 2)
    AvgHandleCount = [Math]::Round((Get-MeasureValueOrZero -Values $handleCountSeries -Metric "Average"), 2)
    PeakHandleCount = [Math]::Round((Get-MeasureValueOrZero -Values $handleCountSeries -Metric "Maximum"), 2)
}

$summaryPath = Join-Path $outputRoot ("{0}-footprint-{1}.summary.json" -f $ProcessName.ToLowerInvariant(), $timestamp)
$summary | ConvertTo-Json -Depth 4 | Out-File -LiteralPath $summaryPath -Encoding Ascii

Write-Host ""
Write-Host "Collection complete."
Write-Host "CSV:     $outputPath"
Write-Host "Summary: $summaryPath"
Write-Host ""
Write-Host "Session summary:"
$summary | Format-List | Out-String | Write-Host
Write-Host ""
Write-Host "Optional GC telemetry command:"
Write-Host "dotnet-counters monitor --process-id $targetProcessId --refresh-interval $SampleIntervalSeconds --counters System.Runtime"
