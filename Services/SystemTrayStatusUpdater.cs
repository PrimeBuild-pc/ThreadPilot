namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    public interface ISystemTrayStatusUpdater
    {
        bool ShouldRunPerformanceStatusUpdates { get; }

        Task UpdateContextMenuAsync(ISystemTrayService systemTrayService, Func<Action, Task> dispatchAsync);

        Task<bool> UpdateStatusAsync(ISystemTrayService systemTrayService, Func<Action, Task> dispatchAsync);
    }

    public sealed class SystemTrayStatusUpdater : ISystemTrayStatusUpdater
    {
        private readonly IPowerPlanService powerPlanService;
        private readonly Lazy<IPerformanceMonitoringService> performanceService;
        private readonly ILocalizationService? localizationService;
        private readonly ILogger<SystemTrayStatusUpdater>? logger;

        public SystemTrayStatusUpdater(
            IPowerPlanService powerPlanService,
            Lazy<IPerformanceMonitoringService> performanceService,
            ILocalizationService? localizationService = null,
            ILogger<SystemTrayStatusUpdater>? logger = null)
        {
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
            this.localizationService = localizationService;
            this.logger = logger;
        }

        public bool ShouldRunPerformanceStatusUpdates => AppNavigationOptions.ShowAdvancedDiagnostics;

        public async Task UpdateContextMenuAsync(ISystemTrayService systemTrayService, Func<Action, Task> dispatchAsync)
        {
            ArgumentNullException.ThrowIfNull(systemTrayService);
            ArgumentNullException.ThrowIfNull(dispatchAsync);

            var activePowerPlan = await this.UpdatePowerPlanMenuAsync(systemTrayService, dispatchAsync).ConfigureAwait(false);
            await this.UpdateProfileMenuAsync(systemTrayService, dispatchAsync).ConfigureAwait(false);

            await this.UpdateStatusCoreAsync(
                systemTrayService,
                activePowerPlan,
                dispatchAsync).ConfigureAwait(false);
        }

        public async Task<bool> UpdateStatusAsync(ISystemTrayService systemTrayService, Func<Action, Task> dispatchAsync)
        {
            ArgumentNullException.ThrowIfNull(systemTrayService);
            ArgumentNullException.ThrowIfNull(dispatchAsync);

            try
            {
                var activePowerPlan = await this.powerPlanService.GetActivePowerPlan().ConfigureAwait(false);
                await this.UpdateStatusCoreAsync(systemTrayService, activePowerPlan, dispatchAsync).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                this.logger?.LogDebug(ex, "Failed to update the system tray status");
                return false;
            }
        }

        private async Task<PowerPlanModel?> UpdatePowerPlanMenuAsync(
            ISystemTrayService systemTrayService,
            Func<Action, Task> dispatchAsync)
        {
            var powerPlans = await this.powerPlanService.GetPowerPlansAsync().ConfigureAwait(false);
            var activePowerPlan = powerPlans.FirstOrDefault(plan => plan.IsActive);

            await dispatchAsync(() => systemTrayService.UpdatePowerPlans(powerPlans, activePowerPlan)).ConfigureAwait(false);
            return activePowerPlan;
        }

        private async Task UpdateProfileMenuAsync(
            ISystemTrayService systemTrayService,
            Func<Action, Task> dispatchAsync)
        {
            var profilesDirectory = StoragePaths.ProfilesDirectory;
            var profileNames = new List<string>();

            try
            {
                if (Directory.Exists(profilesDirectory))
                {
                    profileNames = Directory.GetFiles(profilesDirectory, "*.json")
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList()!;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.logger?.LogDebug(ex, "Could not enumerate saved profiles for the tray menu");
                profileNames = new List<string>();
            }

            await dispatchAsync(() => systemTrayService.UpdateProfiles(profileNames)).ConfigureAwait(false);
        }

        private async Task UpdateStatusCoreAsync(
            ISystemTrayService systemTrayService,
            PowerPlanModel? activePowerPlan,
            Func<Action, Task> dispatchAsync)
        {
            var planName = activePowerPlan?.Name ?? this.Localize("SystemTray_Unknown", "Unknown");

            if (!this.ShouldRunPerformanceStatusUpdates)
            {
                await dispatchAsync(() => systemTrayService.UpdateSystemStatus(planName)).ConfigureAwait(false);
                return;
            }

            try
            {
                var metricsTask = this.performanceService.Value.GetSystemMetricsAsync(lightweight: true);
                var metricsResult = await Task.WhenAny(metricsTask, Task.Delay(2000)).ConfigureAwait(false);

                if (metricsResult == metricsTask)
                {
                    var currentMetrics = await metricsTask.ConfigureAwait(false);
                    await dispatchAsync(() => systemTrayService.UpdateSystemStatus(
                        planName,
                        currentMetrics?.TotalCpuUsage ?? 0.0,
                        currentMetrics?.MemoryUsagePercentage ?? 0.0)).ConfigureAwait(false);
                    return;
                }
            }
            catch
            {
                // Fall back to non-performance status below.
            }

            await dispatchAsync(() => systemTrayService.UpdateSystemStatus(planName)).ConfigureAwait(false);
        }

        private string Localize(string key, string fallback)
        {
            if (this.localizationService == null)
            {
                return fallback;
            }

            var localized = this.localizationService.GetString(key);
            return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback
                : localized;
        }
    }
}
