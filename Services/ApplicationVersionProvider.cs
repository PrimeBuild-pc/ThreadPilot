/*
 * ThreadPilot - application version provider for update checks.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Linq;
    using System.Reflection;

    public sealed class ApplicationVersionProvider : IApplicationVersionProvider
    {
        public SemanticVersion CurrentVersion
        {
            get
            {
                var rawVersion = GetRawVersion();
                return SemanticVersion.TryParse(rawVersion, out var version)
                    ? version
                    : new SemanticVersion(0, 0, 0);
            }
        }

        public string DisplayVersion => $"v{this.CurrentVersion}";

        private static string GetRawVersion()
        {
            return typeof(App).Assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?
                .InformationalVersion
                ?? typeof(App).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";
        }
    }
}
