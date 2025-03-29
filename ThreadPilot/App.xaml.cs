using System;
using System.Windows;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    public partial class App : Application
    {
        private Mutex _appMutex;
        private static IServiceProvider _serviceProvider;

        public App()
        {
            // Check if the app is already running
            _appMutex = new Mutex(true, "ThreadPilotSingleInstanceMutex", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("ThreadPilot is already running.", "ThreadPilot", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Register services
            services.AddSingleton<ProcessService>();
            services.AddSingleton<AffinityService>();
            services.AddSingleton<SystemOptimizationService>();
            services.AddSingleton<IPowerProfileService, PowerProfileService>();
            services.AddSingleton<PowerProfileService>();
            services.AddSingleton<IBundledPowerProfilesService, BundledPowerProfilesService>();
            services.AddSingleton<BundledPowerProfilesService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<NotificationService>();

            // Register view models
            services.AddSingleton<MainViewModel>();
            services.AddTransient<ProcessListViewModel>();
            services.AddTransient<AffinityViewModel>();
            services.AddTransient<SystemOptimizationViewModel>();
            services.AddTransient<PowerProfilesViewModel>();
            services.AddTransient<SettingsViewModel>();
        }

        public static T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set the theme based on user preferences or system settings
            var settingsService = GetService<SettingsService>();
            if (settingsService != null)
            {
                settingsService.ApplyTheme();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _appMutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
