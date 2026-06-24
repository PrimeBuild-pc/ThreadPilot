namespace ThreadPilot.Helpers
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceProviderExtensions
    {
        public static IServiceProvider Services => ((App)App.Current).ServiceProvider;

        public static T? GetService<T>()
            where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }
    }
}
