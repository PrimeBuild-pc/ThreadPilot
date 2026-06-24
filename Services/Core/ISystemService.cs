namespace ThreadPilot.Services.Core
{
    using System;

    public interface ISystemService
    {
        bool IsAvailable { get; }

        event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;

        Task InitializeAsync();

        Task DisposeAsync();
    }

    public class ServiceAvailabilityChangedEventArgs : EventArgs
    {
        public bool IsAvailable { get; }

        public string? Reason { get; }

        public ServiceAvailabilityChangedEventArgs(bool isAvailable, string? reason = null)
        {
            this.IsAvailable = isAvailable;
            this.Reason = reason;
        }
    }
}

