/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public class PowerPlanService : IPowerPlanService
    {
        private static readonly Lazy<string> powerPlansPath = new(GetPowerPlansPath);
        private static readonly string powerCfgExecutablePath = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
        private static readonly Regex powerSchemeRegex = new(@"Power Scheme GUID: (.*?)  \((.*?)\)", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex pathTraversalRegex = new(@"(^|[\\/])\.\.([\\/]|$)", RegexOptions.Compiled);

        private static string PowerPlansPath => powerPlansPath.Value;

        private readonly object lockObject = new();
        private readonly ILogger<PowerPlanService> logger;
        private readonly IEnhancedLoggingService enhancedLogger;
        private string? lastActivePowerPlanGuid;

        public event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;

        public PowerPlanService(ILogger<PowerPlanService> logger, IEnhancedLoggingService enhancedLogger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.enhancedLogger = enhancedLogger ?? throw new ArgumentNullException(nameof(enhancedLogger));
        }

        /// <summary>
        /// Gets the power plans path using smart detection:
        /// - Portable mode: Powerplans folder next to EXE
        /// - Installed mode: %AppData%\ThreadPilot\Powerplans.
        /// </summary>
        private static string GetPowerPlansPath()
        {
            StoragePaths.EnsureAppDataDirectories();

            // Check portable mode first (Powerplans folder next to EXE)
            var exeDir = AppContext.BaseDirectory;
            var portablePath = Path.Combine(exeDir, "Powerplans");
            if (Directory.Exists(portablePath) && Directory.GetFiles(portablePath, "*.pow").Length > 0)
            {
                return portablePath;
            }

            // Installed mode: use AppData
            var appDataPath = StoragePaths.PowerPlansDirectory;

            // Ensure directory exists
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);

                // If portable path exists but was empty, or we have bundled plans to copy
                // Copy any .pow files from portable location to AppData
                if (Directory.Exists(portablePath))
                {
                    foreach (var file in Directory.GetFiles(portablePath, "*.pow"))
                    {
                        var destFile = Path.Combine(appDataPath, Path.GetFileName(file));
                        if (!File.Exists(destFile))
                        {
                            try
                            {
                                File.Copy(file, destFile);
                            }
                            catch
                            {
                                // Ignore copy errors, plans may not be available
                            }
                        }
                    }
                }
            }

            return appDataPath;
        }

        public async Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync()
        {
            var powerPlans = new ObservableCollection<PowerPlanModel>();
            var activePlan = await this.GetActivePowerPlan();

            using var process = CreatePowerCfgProcess("/list");
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var matches = powerSchemeRegex.Matches(output);

            foreach (Match match in matches)
            {
                var guid = match.Groups[1].Value.Trim();
                var name = match.Groups[2].Value.Trim();

                var plan = new PowerPlanModel
                {
                    Guid = guid,
                    Name = name,
                    IsActive = guid == activePlan?.Guid,
                    IsCustomPlan = false,
                };

                powerPlans.Add(plan);
            }

            return powerPlans;
        }

        public async Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync()
        {
            var customPlans = new ObservableCollection<PowerPlanModel>();
            if (!Directory.Exists(PowerPlansPath))
            {
                return customPlans;
            }

            foreach (var file in Directory.GetFiles(PowerPlansPath, "*.pow"))
            {
                customPlans.Add(new PowerPlanModel
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    IsCustomPlan = true,
                });
            }

            return await Task.FromResult(customPlans);
        }

        public async Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan)
        {
            return await this.SetActivePowerPlanByGuidAsync(powerPlan.Guid, false);
        }

        public async Task<bool> SetActivePowerPlanByGuidAsync(string powerPlanGuid, bool preventDuplicateChanges = true)
        {
            if (!Guid.TryParse(powerPlanGuid, out _))
            {
                this.logger.LogWarning("Rejected invalid power plan GUID: {PowerPlanGuid}", powerPlanGuid);
                return false;
            }

            try
            {
                // Check if change is needed when duplicate prevention is enabled
                if (preventDuplicateChanges)
                {
                    var isChangeNeeded = await this.IsPowerPlanChangeNeededAsync(powerPlanGuid);
                    if (!isChangeNeeded)
                    {
                        this.logger.LogDebug("Power plan change skipped - already active: {PowerPlanGuid}", powerPlanGuid);
                        return true; // No change needed, consider it successful
                    }
                }

                var previousPowerPlan = await this.GetActivePowerPlan();
                var targetPowerPlan = await this.GetPowerPlanByGuidAsync(powerPlanGuid);

                this.logger.LogInformation(
                    "Attempting to change power plan from '{FromPlan}' to '{ToPlan}'",
                    previousPowerPlan?.Name ?? "Unknown", targetPowerPlan?.Name ?? "Unknown");

                await this.enhancedLogger.LogPowerPlanChangeAsync(
                    previousPowerPlan?.Name ?? "Unknown",
                    targetPowerPlan?.Name ?? "Unknown",
                    "Manual power plan change requested");

                using var process = CreatePowerCfgProcess($"/setactive {powerPlanGuid}");
                process.Start();
                var stdError = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var success = process.ExitCode == 0;

                if (success)
                {
                    lock (this.lockObject)
                    {
                        this.lastActivePowerPlanGuid = powerPlanGuid;
                    }

                    var newPowerPlan = await this.GetPowerPlanByGuidAsync(powerPlanGuid);

                    this.logger.LogInformation("Power plan successfully changed to '{PowerPlan}'", newPowerPlan?.Name ?? "Unknown");

                    await this.enhancedLogger.LogPowerPlanChangeAsync(
                        previousPowerPlan?.Name ?? "Unknown",
                        newPowerPlan?.Name ?? "Unknown",
                        "Manual power plan change completed");

                    this.PowerPlanChanged?.Invoke(this, new PowerPlanChangedEventArgs(
                        previousPowerPlan, newPowerPlan, "Manual power plan change"));
                }
                else
                {
                    this.logger.LogWarning(
                        "Failed to change power plan to '{PowerPlanGuid}' - powercfg exit code: {ExitCode}, stderr: {StdErr}",
                        powerPlanGuid,
                        process.ExitCode,
                        stdError);

                    await this.enhancedLogger.LogSystemEventAsync(
                        LogEventTypes.PowerPlan.ChangeFailed,
                        $"Failed to change power plan to '{targetPowerPlan?.Name ?? powerPlanGuid}' - Exit code: {process.ExitCode}",
                        Microsoft.Extensions.Logging.LogLevel.Warning);
                }

                return success;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Exception occurred while changing power plan to '{PowerPlanGuid}'", powerPlanGuid);

                await this.enhancedLogger.LogErrorAsync(ex, "PowerPlanService.SetActivePowerPlanByGuidAsync",
                    new Dictionary<string, object>
                    {
                        ["PowerPlanGuid"] = powerPlanGuid,
                        ["PreventDuplicateChanges"] = preventDuplicateChanges,
                    });

                return false;
            }
        }

        public async Task<PowerPlanModel?> GetActivePowerPlan()
        {
            try
            {
                using var process = CreatePowerCfgProcess("/getactivescheme");
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var match = powerSchemeRegex.Match(output);

                if (match.Success)
                {
                    return new PowerPlanModel
                    {
                        Guid = match.Groups[1].Value.Trim(),
                        Name = match.Groups[2].Value.Trim(),
                        IsActive = true,
                    };
                }

                this.logger.LogWarning("Could not parse active power plan from powercfg output.");
                return null;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to read active power plan.");
                return null;
            }
        }

        public async Task<bool> ImportCustomPowerPlan(string filePath)
        {
            if (!this.TryNormalizePowerPlanPath(filePath, out var normalizedPath, out var validationError))
            {
                this.logger.LogWarning("Rejected power plan import path '{FilePath}': {ValidationError}", filePath, validationError);
                return false;
            }

            try
            {
                using var process = CreatePowerCfgProcess($"/import {QuoteArgument(normalizedPath)}");
                process.Start();
                var stdError = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    this.logger.LogWarning(
                        "Power plan import failed for '{Path}' with exit code {ExitCode}. stderr: {StdErr}",
                        normalizedPath,
                        process.ExitCode,
                        stdError);

                    return false;
                }

                this.logger.LogInformation("Imported custom power plan from '{Path}'", normalizedPath);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Exception occurred while importing custom power plan from '{Path}'", normalizedPath);
                await this.enhancedLogger.LogErrorAsync(ex, "PowerPlanService.ImportCustomPowerPlan",
                    new Dictionary<string, object> { ["Path"] = normalizedPath });
                return false;
            }
        }

        public async Task<string?> GetActivePowerPlanGuidAsync()
        {
            var activePlan = await this.GetActivePowerPlan();
            return activePlan?.Guid;
        }

        public async Task<bool> PowerPlanExistsAsync(string powerPlanGuid)
        {
            if (!Guid.TryParse(powerPlanGuid, out _))
            {
                return false;
            }

            var powerPlans = await this.GetPowerPlansAsync();
            return powerPlans.Any(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string powerPlanGuid)
        {
            if (!Guid.TryParse(powerPlanGuid, out _))
            {
                return null;
            }

            var powerPlans = await this.GetPowerPlansAsync();
            return powerPlans.FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsPowerPlanChangeNeededAsync(string targetPowerPlanGuid)
        {
            try
            {
                var currentGuid = await this.GetActivePowerPlanGuidAsync();

                // Check if the target power plan is already active
                if (string.Equals(currentGuid, targetPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // No change needed
                }

                // Check if we recently set this power plan (to prevent rapid switching)
                lock (this.lockObject)
                {
                    if (string.Equals(this.lastActivePowerPlanGuid, targetPowerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // We recently set this plan
                    }
                }

                return true; // Change is needed
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not determine if power plan change is needed for '{PowerPlanGuid}'", targetPowerPlanGuid);
                return true; // If we can't determine, assume change is needed
            }
        }

        private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

        private bool TryNormalizePowerPlanPath(string filePath, out string normalizedPath, out string error)
        {
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "Path is empty.";
                return false;
            }

            if (pathTraversalRegex.IsMatch(filePath))
            {
                error = "Path traversal segments are not allowed.";
                return false;
            }

            if (!Path.IsPathFullyQualified(filePath))
            {
                error = "Path must be absolute.";
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                error = $"Invalid file path: {ex.Message}";
                return false;
            }

            if (!string.Equals(Path.GetExtension(normalizedPath), ".pow", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only .pow files are supported.";
                return false;
            }

            if (!File.Exists(normalizedPath))
            {
                error = "File does not exist.";
                return false;
            }

            var fileInfo = new FileInfo(normalizedPath);
            if (fileInfo.Length > 10 * 1024 * 1024)
            {
                error = "Power plan file size exceeds 10 MB limit.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static Process CreatePowerCfgProcess(string arguments)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = powerCfgExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
            };
        }
    }
}
