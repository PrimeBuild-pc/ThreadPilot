namespace ThreadPilot.Services
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.Services.Abstractions;
    using ThreadPilot.ViewModels;

    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureApplicationServices(this IServiceCollection services)
        {
            // Configure service infrastructure
            services.ConfigureServiceInfrastructure();

            // Configure core system services
            services.ConfigureCoreSystemServices();

            // Configure process management services
            services.ConfigureProcessManagementServices();

            // Configure application services
            services.ConfigureApplicationLevelServices();

            // Configure presentation layer
            services.ConfigurePresentationLayer();

            return services;
        }

        private static IServiceCollection ConfigureServiceInfrastructure(this IServiceCollection services)
        {
            // Logging infrastructure
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));

            // Enhanced logging service
            services.AddSingleton<IEnhancedLoggingService, EnhancedLoggingService>();
            services.AddSingleton<IActivityAuditService, ActivityAuditService>();
            services.AddSingleton<IProcessRunner, SystemProcessRunner>();
            services.AddSingleton<ISettingsStorage, FileSettingsStorage>();
            services.AddSingleton(sp =>
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThreadPilot", "1.0"));
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                return httpClient;
            });
            services.AddSingleton<IGitHubReleaseClient, GitHubReleaseClient>();
            services.AddSingleton<GitHubUpdateChecker>();
            services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
            services.AddSingleton<IUpdateClock, SystemUpdateClock>();
            services.AddSingleton<IUpdateDownloadClient, HttpUpdateDownloadClient>();
            services.AddSingleton<IUpdateTempDirectoryProvider, UpdateTempDirectoryProvider>();
            services.AddSingleton<IUpdateSignatureVerifier, AuthenticodeSignatureVerifier>();
            services.AddSingleton<IUpdateDownloadService, UpdateDownloadService>();
            services.AddSingleton<IUpdateProcessLauncher, ShellUpdateProcessLauncher>();
            services.AddSingleton<IUpdateInstallerService, UpdateInstallerService>();
            services.AddSingleton<IApplicationShutdownService, WpfApplicationShutdownService>();
            services.AddSingleton<IUpdateService, UpdateService>();

            return services;
        }

        private static IServiceCollection ConfigureCoreSystemServices(this IServiceCollection services)
        {
            // Core system interaction services
            services.AddSingleton<IForegroundWindowProvider, WindowsForegroundWindowProvider>();
            services.AddSingleton<IForegroundProcessService, ForegroundProcessService>();
            services.AddSingleton<IPassiveProcessErrorThrottle, PassiveProcessErrorThrottle>();
            services.AddSingleton<IProcessClassifier, ProcessClassifier>();
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IAffinityApplyService, AffinityApplyService>();
            services.AddSingleton<IProcessAffinityApplyCoordinator, ProcessAffinityApplyCoordinator>();
            services.AddSingleton<IProcessMemoryPriorityNativeApi>(ProcessMemoryPriorityNativeApi.Instance);
            services.AddSingleton<IProcessMemoryPriorityService, ProcessMemoryPriorityService>();
            services.AddSingleton<IPersistentProcessRuleStore, PersistentProcessRuleJsonStore>();
            services.AddSingleton<IPersistentProcessRuleMatcher, PersistentProcessRuleMatcher>();
            services.AddSingleton<IPersistentRulesEngine, PersistentRulesEngine>();
            services.AddSingleton<IPersistentRuleAutoApplyService, PersistentRuleAutoApplyService>();
            services.AddSingleton<IProcessRuleCreationService, ProcessRuleCreationService>();
            services.AddSingleton<ProcessFilterService>();
            services.AddSingleton<IVirtualizedProcessService, VirtualizedProcessService>();
            services.AddSingleton<IConditionalProfileService, ConditionalProfileService>();
            services.AddSingleton<IPowerPlanService, PowerPlanService>();
            services.AddSingleton<PowerPlanTransitionGate>();
            services.AddSingleton<ICpuTopologyService, CpuTopologyService>();
            services.AddSingleton<ICpuTopologyProvider, WindowsCpuTopologyProvider>();
            services.AddSingleton<CpuSelectionMigrationService>();
            services.AddSingleton<ICpuPresetGenerator, CpuPresetGenerator>();

            // CoreMaskService needs IServiceProvider for checking profile references
            services.AddSingleton<ICoreMaskService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<CoreMaskService>>();
                var cpuTopologyService = sp.GetRequiredService<ICpuTopologyService>();
                var cpuTopologyProvider = sp.GetRequiredService<ICpuTopologyProvider>();
                var migrationService = sp.GetRequiredService<CpuSelectionMigrationService>();
                return new CoreMaskService(logger, cpuTopologyService, sp, cpuTopologyProvider, migrationService);
            });

            return services;
        }

        private static IServiceCollection ConfigureProcessManagementServices(this IServiceCollection services)
        {
            // Process monitoring services
            services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
            services.AddSingleton<IProcessPowerPlanAssociationService, ProcessPowerPlanAssociationService>();
            services.AddSingleton<IProcessMonitorManagerService, ProcessMonitorManagerService>();

            // Performance monitoring services
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
            services.AddSingleton(sp => new Lazy<IPerformanceMonitoringService>(
                () => sp.GetRequiredService<IPerformanceMonitoringService>()));
            services.AddSingleton<ISystemTrayStatusUpdater, SystemTrayStatusUpdater>();

            return services;
        }

        private static IServiceCollection ConfigureApplicationLevelServices(this IServiceCollection services)
        {
            // Application configuration and settings
            services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<ISelfResourceManagementService, SelfResourceManagementService>();

            // User interface services
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<ISmartNotificationService, SmartNotificationService>();
            services.AddSingleton<ISystemTrayService, SystemTrayService>();

            // System integration services
            services.AddSingleton<IAutostartService, AutostartService>();

            // System optimization services
            services.AddSingleton<IGameModeService, GameModeService>();

            // Security and elevation services
            services.AddSingleton<ISecurityService, SecurityService>();
            services.AddSingleton<IElevationService, ElevationService>();
            services.AddSingleton<IElevatedTaskService, ElevatedTaskService>();

            // System tweaks service
            services.AddSingleton<ISystemTweaksService, SystemTweaksService>();

            // Keyboard shortcut service
            services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();

            return services;
        }

        private static IServiceCollection ConfigurePresentationLayer(this IServiceCollection services)
        {
            // ViewModels - ProcessViewModel as Singleton to share state across views, others as Transient
            services.AddSingleton<ProcessViewModel>();
            services.AddSingleton<MasksViewModel>();
            services.AddTransient<PowerPlanViewModel>();
            services.AddTransient<ProcessPowerPlanAssociationViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<PerformanceViewModel>();
            services.AddTransient(sp => new Lazy<PerformanceViewModel>(
                () => sp.GetRequiredService<PerformanceViewModel>()));
            services.AddTransient<LogViewerViewModel>();
            services.AddTransient<SystemTweaksViewModel>();

            // Views - Transient for proper lifecycle management
            services.AddTransient<MainWindow>();

            return services;
        }

        public static void ValidateServiceConfiguration(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("ServiceConfiguration");

            try
            {
                // Validate core services can be resolved
                var coreServices = new[]
                {
                    typeof(IProcessService),
                    typeof(IPowerPlanService),
                    typeof(ICpuTopologyService),
                    typeof(IEnhancedLoggingService),
                    typeof(IActivityAuditService),
                    typeof(IApplicationSettingsService),
                };

                foreach (var serviceType in coreServices)
                {
                    var service = serviceProvider.GetRequiredService(serviceType);
                    if (service == null)
                    {
                        throw new InvalidOperationException($"Failed to resolve required service: {serviceType.Name}");
                    }
                }

                logger.LogInformation("Service configuration validation completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Service configuration validation failed");
                throw;
            }
        }
    }
}
