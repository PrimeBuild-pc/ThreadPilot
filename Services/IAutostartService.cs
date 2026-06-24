namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;

    public interface IAutostartService
    {
        event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        bool IsAutostartEnabled { get; }

        string? AutostartPath { get; }

        Task<bool> EnableAutostartAsync(bool startMinimized = true);

        Task<bool> DisableAutostartAsync();

        Task<bool> CheckAutostartStatusAsync();

        Task<bool> UpdateAutostartAsync(bool startMinimized = true);

        string GetAutostartArguments(bool startMinimized = true);
    }

    public class AutostartStatusChangedEventArgs : EventArgs
    {
        public bool IsEnabled { get; }

        public bool StartMinimized { get; }

        public string? RegistryPath { get; }

        public Exception? Error { get; }

        public AutostartStatusChangedEventArgs(bool isEnabled, bool startMinimized = false, string? registryPath = null, Exception? error = null)
        {
            this.IsEnabled = isEnabled;
            this.StartMinimized = startMinimized;
            this.RegistryPath = registryPath;
            this.Error = error;
        }
    }
}

