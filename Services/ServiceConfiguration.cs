/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Centralized service configuration for dependency injection
    /// </summary>
    public static class ServiceConfiguration
    {
        /// <summary>
        /// Configure all application services
        /// </summary>
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

        /// <summary>
        /// Configure service infrastructure (logging, factories, etc.)
        /// </summary>
        private static IServiceCollection ConfigureServiceInfrastructure(this IServiceCollection services)
        {
            // Logging infrastructure
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Enhanced logging service
            services.AddSingleton<IEnhancedLoggingService, EnhancedLoggingService>();

            // Memory caching for performance - PERFORMANCE IMPROVEMENT
            services.AddMemoryCache();

            // Service lifecycle management - PERFORMANCE IMPROVEMENT
            services.AddSingleton<IServiceHealthMonitor, ServiceHealthMonitor>();
            services.AddSingleton<IServiceDisposalCoordinator, ServiceDisposalCoordinator>();

            // Error recovery and retry policies - RELIABILITY IMPROVEMENT
            services.AddSingleton<IRetryPolicyService, RetryPolicyService>();

            // Service factory for advanced service management
            services.AddSingleton<IServiceFactory, ServiceFactory>();

            return services;
        }

        /// <summary>
        /// Configure core system services that interact directly with the OS
        /// </summary>
        private static IServiceCollection ConfigureCoreSystemServices(this IServiceCollection services)
        {
            // Core system interaction services
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IVirtualizedProcessService, VirtualizedProcessService>();
            services.AddSingleton<IConditionalProfileService, ConditionalProfileService>();
            services.AddSingleton<IPowerPlanService, PowerPlanService>();
            services.AddSingleton<ICpuTopologyService, CpuTopologyService>();
            
            // CoreMaskService needs IServiceProvider for checking profile references
            services.AddSingleton<ICoreMaskService>(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<CoreMaskService>>();
                var cpuTopologyService = sp.GetRequiredService<ICpuTopologyService>();
                return new CoreMaskService(logger, cpuTopologyService, sp);
            });

            return services;
        }

        /// <summary>
        /// Configure process monitoring and management services
        /// </summary>
        private static IServiceCollection ConfigureProcessManagementServices(this IServiceCollection services)
        {
            // Process monitoring services
            services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
            services.AddSingleton<IProcessPowerPlanAssociationService, ProcessPowerPlanAssociationService>();
            services.AddSingleton<IProcessMonitorManagerService, ProcessMonitorManagerService>();

            // Performance monitoring services
            services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();

            return services;
        }

        /// <summary>
        /// Configure application-level services (settings, notifications, etc.)
        /// </summary>
        private static IServiceCollection ConfigureApplicationLevelServices(this IServiceCollection services)
        {
            // Application configuration and settings
            services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
            
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

            // System tweaks service
            services.AddSingleton<ISystemTweaksService, SystemTweaksService>();

            // Keyboard shortcut service
            services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();

            return services;
        }

        /// <summary>
        /// Configure presentation layer (ViewModels and Views)
        /// </summary>
        private static IServiceCollection ConfigurePresentationLayer(this IServiceCollection services)
        {
            // ViewModel factory for centralized ViewModel management
            services.AddViewModelFactory();

            // ViewModels - ProcessViewModel as Singleton to share state across views, others as Transient
            services.AddSingleton<ProcessViewModel>();
            services.AddSingleton<MasksViewModel>();
            services.AddTransient<PowerPlanViewModel>();
            services.AddTransient<ProcessPowerPlanAssociationViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<PerformanceViewModel>();
            services.AddTransient<SystemTweaksViewModel>();

            // Views - Transient for proper lifecycle management
            services.AddTransient<MainWindow>();

            return services;
        }

        /// <summary>
        /// Validate service configuration
        /// </summary>
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
                    typeof(IApplicationSettingsService)
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

