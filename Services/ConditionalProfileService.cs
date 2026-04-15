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
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Implementation of conditional process profile service.
    /// </summary>
    public class ConditionalProfileService : IConditionalProfileService, IDisposable
    {
        private readonly ILogger<ConditionalProfileService> logger;
        private readonly IProcessService processService;
        private readonly IRetryPolicyService retryPolicy;
        private readonly List<ConditionalProcessProfile> profiles = new();
        private readonly System.Threading.Timer monitoringTimer;
        private readonly SemaphoreSlim profileLock = new(1, 1);

        private SystemState lastSystemState = new();
        private bool isMonitoring;
        private bool disposed;

        public bool IsMonitoring => this.isMonitoring;

        public event EventHandler<ProfileApplicationEventArgs>? ProfileApplied;

        public event EventHandler<ProfileConflictEventArgs>? ProfileConflictResolved;

        public event EventHandler<SystemState>? SystemStateChanged;

        public ConditionalProfileService(
            ILogger<ConditionalProfileService> logger,
            IProcessService processService,
            IRetryPolicyService retryPolicy)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

            // Set up monitoring timer (check every 10 seconds)
            this.monitoringTimer = new System.Threading.Timer(this.MonitoringCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing ConditionalProfileService");

            // Load initial system state
            this.lastSystemState = await this.GetSystemStateAsync().ConfigureAwait(false);

            // Create some default profiles for demonstration
            await this.CreateDefaultProfilesAsync().ConfigureAwait(false);
        }

        public async Task AddProfileAsync(ConditionalProcessProfile profile)
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var (isValid, errors) = await this.ValidateProfileAsync(profile).ConfigureAwait(false);
                if (!isValid)
                {
                    throw new ArgumentException($"Invalid profile: {string.Join(", ", errors)}");
                }

                this.profiles.Add(profile);
                this.logger.LogInformation(
                    "Added conditional profile: {ProfileName} for process {ProcessName}",
                    profile.Name, profile.ProcessName);
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task RemoveProfileAsync(string profileId)
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var profile = this.profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile != null)
                {
                    this.profiles.Remove(profile);
                    this.logger.LogInformation("Removed conditional profile: {ProfileName}", profile.Name);
                }
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task UpdateProfileAsync(ConditionalProcessProfile profile)
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var existingProfile = this.profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existingProfile != null)
                {
                    var index = this.profiles.IndexOf(existingProfile);
                    this.profiles[index] = profile;
                    this.logger.LogInformation("Updated conditional profile: {ProfileName}", profile.Name);
                }
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> GetAllProfilesAsync()
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return this.profiles.ToList();
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> GetProfilesForProcessAsync(string processName)
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return this.profiles
                    .Where(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task<List<ConditionalProcessProfile>> EvaluateProfilesAsync(ProcessModel process)
        {
            var systemState = await this.GetSystemStateAsync().ConfigureAwait(false);
            var applicableProfiles = new List<ConditionalProcessProfile>();

            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var processProfiles = this.profiles
                    .Where(p => p.ProcessName.Equals(process.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var profile in processProfiles)
                {
                    if (profile.ShouldApply(process, systemState) && profile.CanApplyNow())
                    {
                        applicableProfiles.Add(profile);
                    }
                }

                // Sort by priority (higher priority first)
                applicableProfiles.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            finally
            {
                this.profileLock.Release();
            }

            return applicableProfiles;
        }

        public async Task<bool> ApplyBestProfileAsync(ProcessModel process)
        {
            try
            {
                var applicableProfiles = await this.EvaluateProfilesAsync(process).ConfigureAwait(false);

                if (!applicableProfiles.Any())
                {
                    return false;
                }

                ConditionalProcessProfile selectedProfile;

                if (applicableProfiles.Count == 1)
                {
                    selectedProfile = applicableProfiles[0];
                }
                else
                {
                    // Handle conflicts
                    selectedProfile = this.ResolveProfileConflict(applicableProfiles, process);

                    this.ProfileConflictResolved?.Invoke(this, new ProfileConflictEventArgs
                    {
                        ConflictingProfiles = applicableProfiles,
                        Process = process,
                        SelectedProfile = selectedProfile,
                        Resolution = "Priority-based selection",
                    });
                }

                // Apply the profile (simplified - would use actual process service)
                var success = await this.ApplyProfileToProcessAsync(process, selectedProfile).ConfigureAwait(false);

                if (success)
                {
                    selectedProfile.MarkAsApplied();

                    this.ProfileApplied?.Invoke(this, new ProfileApplicationEventArgs
                    {
                        Profile = selectedProfile,
                        Process = process,
                        SystemState = await this.GetSystemStateAsync().ConfigureAwait(false),
                        WasApplied = true,
                        Reason = "Conditions satisfied",
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error applying profile for process {ProcessName}", process.Name);
                return false;
            }
        }

        public async Task<SystemState> GetSystemStateAsync()
        {
            return await this.retryPolicy.ExecuteAsync(
                async () =>
            {
                var systemState = new SystemState
                {
                    CurrentTime = DateTime.Now,
                    CpuUsage = await this.GetCpuUsageAsync().ConfigureAwait(false),
                    MemoryUsage = await this.GetMemoryUsageAsync().ConfigureAwait(false),
                    ProcessCount = await this.GetProcessCountAsync().ConfigureAwait(false),
                    IsOnBattery = this.GetBatteryStatus(),
                    BatteryLevel = this.GetBatteryLevel(),
                    IsUserIdle = this.GetUserIdleStatus(),
                    UserIdleTime = this.GetUserIdleTime(),
                    NetworkActivity = await this.GetNetworkActivityAsync().ConfigureAwait(false),
                };

                // Check if system state changed significantly
                if (this.HasSystemStateChangedSignificantly(systemState, this.lastSystemState))
                {
                    this.SystemStateChanged?.Invoke(this, systemState);
                    this.lastSystemState = systemState;
                }

                return systemState;
            }, this.retryPolicy.CreateProcessOperationPolicy()).ConfigureAwait(false);
        }

        public async Task StartMonitoringAsync()
        {
            if (this.isMonitoring)
            {
                return;
            }

            this.logger.LogInformation("Starting conditional profile monitoring");
            this.isMonitoring = true;

            // Start monitoring timer (check every 10 seconds)
            this.monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public async Task StopMonitoringAsync()
        {
            if (!this.isMonitoring)
            {
                return;
            }

            this.logger.LogInformation("Stopping conditional profile monitoring");
            this.isMonitoring = false;

            this.monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public ConditionalProcessProfile ResolveProfileConflict(List<ConditionalProcessProfile> conflictingProfiles, ProcessModel process)
        {
            // Simple resolution: highest priority wins
            return conflictingProfiles.OrderByDescending(p => p.Priority).First();
        }

        public ConditionalProcessProfile CreateDefaultProfile(string processName)
        {
            return new ConditionalProcessProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Default Profile for {processName}",
                ProcessName = processName,
                Priority = 0,
                AutoApplyDelay = TimeSpan.FromSeconds(5),
                IsAutoApplyEnabled = true,
                ConditionGroups = new List<ConditionGroup>
                {
                    new ConditionGroup
                    {
                        Name = "Default Conditions",
                        LogicalOperator = LogicalOperator.And,
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition
                            {
                                Name = "High CPU Usage",
                                ConditionType = ProfileConditionType.SystemLoad,
                                ComparisonOperator = ComparisonOperator.GreaterThan,
                                Value = 50.0,
                                Description = "Apply when system CPU usage is above 50%"
                            }
                        }
                    }
                },
            };
        }

        public async Task<(bool IsValid, List<string> Errors)> ValidateProfileAsync(ConditionalProcessProfile profile)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                errors.Add("Profile name is required");
            }

            if (string.IsNullOrWhiteSpace(profile.ProcessName))
            {
                errors.Add("Process name is required");
            }

            if (profile.AutoApplyDelay < TimeSpan.Zero)
            {
                errors.Add("Auto-apply delay cannot be negative");
            }

            // Validate condition groups
            foreach (var group in profile.ConditionGroups)
            {
                if (!group.Conditions.Any() && !group.SubGroups.Any())
                {
                    errors.Add($"Condition group '{group.Name}' must have at least one condition or sub-group");
                }
            }

            return (errors.Count == 0, errors);
        }

        public async Task<string> ExportProfilesToJsonAsync()
        {
            await this.profileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return JsonSerializer.Serialize(this.profiles, new JsonSerializerOptions { WriteIndented = true });
            }
            finally
            {
                this.profileLock.Release();
            }
        }

        public async Task<int> ImportProfilesFromJsonAsync(string json)
        {
            try
            {
                var importedProfiles = JsonSerializer.Deserialize<List<ConditionalProcessProfile>>(json);
                if (importedProfiles == null)
                {
                    return 0;
                }

                await this.profileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var validProfiles = 0;
                    foreach (var profile in importedProfiles)
                    {
                        var (isValid, _) = await this.ValidateProfileAsync(profile).ConfigureAwait(false);
                        if (isValid)
                        {
                            this.profiles.Add(profile);
                            validProfiles++;
                        }
                    }

                    this.logger.LogInformation(
                        "Imported {ValidProfiles} valid profiles out of {TotalProfiles}",
                        validProfiles, importedProfiles.Count);

                    return validProfiles;
                }
                finally
                {
                    this.profileLock.Release();
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error importing profiles from JSON");
                return 0;
            }
        }

        private void MonitoringCallback(object? state)
        {
            TaskSafety.FireAndForget(this.MonitoringCallbackAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Error during profile monitoring cycle");
            });
        }

        private async Task MonitoringCallbackAsync()
        {
            if (!this.isMonitoring)
            {
                return;
            }

            try
            {
                var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
                foreach (var process in processes)
                {
                    await this.ApplyBestProfileAsync(process).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Error during profile monitoring cycle");
            }
        }

        private async Task<bool> ApplyProfileToProcessAsync(ProcessModel process, ConditionalProcessProfile profile)
        {
            try
            {
                // Simplified profile application - would use actual process service methods
                this.logger.LogInformation(
                    "Applying profile {ProfileName} to process {ProcessName}",
                    profile.Name, process.Name);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error applying profile {ProfileName} to process {ProcessName}",
                    profile.Name, process.Name);
                return false;
            }
        }

        private async Task CreateDefaultProfilesAsync()
        {
            // Create some example conditional profiles
            var gameProfile = new ConditionalProcessProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "High Performance Gaming",
                ProcessName = "*", // Wildcard for any process
                Priority = 10,
                AutoApplyDelay = TimeSpan.FromSeconds(3),
                ConditionGroups = new List<ConditionGroup>
                {
                    new ConditionGroup
                    {
                        Name = "Gaming Conditions",
                        LogicalOperator = LogicalOperator.And,
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition
                            {
                                Name = "High CPU Usage",
                                ConditionType = ProfileConditionType.SystemLoad,
                                ComparisonOperator = ComparisonOperator.GreaterThan,
                                Value = 70.0
                            },
                            new ProfileCondition
                            {
                                Name = "Evening Hours",
                                ConditionType = ProfileConditionType.TimeOfDay,
                                ComparisonOperator = ComparisonOperator.Between,
                                Value = 18.0, // 6 PM
                                SecondaryValue = 23.0 // 11 PM
                            }
                        }
                    }
                },
            };

            await this.AddProfileAsync(gameProfile).ConfigureAwait(false);
        }

        private Task<double> GetCpuUsageAsync()
        {
            // Simplified CPU usage calculation
            return Task.FromResult(Environment.ProcessorCount * 10.0); // Placeholder
        }

        private Task<double> GetMemoryUsageAsync()
        {
            var totalMemory = GC.GetTotalMemory(false);
            return Task.FromResult(totalMemory / (1024.0 * 1024.0)); // MB
        }

        private async Task<int> GetProcessCountAsync()
        {
            var processes = await this.processService.GetProcessesAsync().ConfigureAwait(false);
            return processes.Count;
        }

        private bool GetBatteryStatus()
        {
            return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        }

        private int GetBatteryLevel()
        {
            return (int)(SystemInformation.PowerStatus.BatteryLifePercent * 100);
        }

        private bool GetUserIdleStatus()
        {
            return this.GetUserIdleTime() > TimeSpan.FromMinutes(5);
        }

        private TimeSpan GetUserIdleTime()
        {
            // Simplified - would use Windows API to get actual idle time
            return TimeSpan.FromMinutes(1);
        }

        private async Task<double> GetNetworkActivityAsync()
        {
            // Simplified network activity measurement
            return 0.0; // Placeholder
        }

        private bool HasSystemStateChangedSignificantly(SystemState current, SystemState previous)
        {
            const double cpuThreshold = 10.0;
            const double memoryThreshold = 100.0; // MB

            return Math.Abs(current.CpuUsage - previous.CpuUsage) > cpuThreshold ||
                   Math.Abs(current.MemoryUsage - previous.MemoryUsage) > memoryThreshold ||
                   current.IsOnBattery != previous.IsOnBattery;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.monitoringTimer?.Dispose();
                    this.profileLock?.Dispose();
                    this.logger.LogInformation("ConditionalProfileService disposed");
                }
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

