namespace ThreadPilot.Services
{
    using System;
    using Microsoft.Extensions.Logging;

    public readonly record struct ForegroundWindowSnapshot(
        IntPtr WindowHandle,
        int ProcessId,
        bool IsVisible,
        bool IsCloaked);

    public interface IForegroundWindowProvider
    {
        bool TryGetForegroundWindow(out ForegroundWindowSnapshot snapshot);
    }

    public interface IForegroundProcessService
    {
        int? TryGetForegroundProcessId();
    }

    public sealed class ForegroundProcessService : IForegroundProcessService
    {
        private readonly IForegroundWindowProvider foregroundWindowProvider;
        private readonly ILogger<ForegroundProcessService> logger;

        public ForegroundProcessService(
            IForegroundWindowProvider foregroundWindowProvider,
            ILogger<ForegroundProcessService> logger)
        {
            this.foregroundWindowProvider = foregroundWindowProvider ?? throw new ArgumentNullException(nameof(foregroundWindowProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int? TryGetForegroundProcessId()
        {
            try
            {
                if (!this.foregroundWindowProvider.TryGetForegroundWindow(out var snapshot))
                {
                    return null;
                }

                if (snapshot.ProcessId <= 0 || !snapshot.IsVisible || snapshot.IsCloaked)
                {
                    return null;
                }

                return snapshot.ProcessId;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Foreground process detection failed");
                return null;
            }
        }
    }
}
