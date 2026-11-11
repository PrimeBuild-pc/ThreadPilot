namespace ThreadPilot.Services
{
    using System;
    using System.Management;
    using Microsoft.Win32;

    // Only the OS subscriptions are replaceable; recovery stays in ProcessMonitorService.
    internal interface IProcessWatchers : IDisposable
    {
        void Start(Action<int, string> started, Action<int> stopped, Action<string> failed);
    }

    internal sealed class WmiProcessWatchers : IProcessWatchers
    {
        private readonly ManagementEventWatcher start = new(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
        private readonly ManagementEventWatcher stop = new(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));

        public void Start(Action<int, string> started, Action<int> stopped, Action<string> failed)
        {
            this.start.EventArrived += (_, e) => HandleEvent(
                () => started(Convert.ToInt32(e.NewEvent["ProcessID"]), e.NewEvent["ProcessName"]?.ToString() ?? string.Empty), failed);
            this.stop.EventArrived += (_, e) => HandleEvent(() => stopped(Convert.ToInt32(e.NewEvent["ProcessID"])), failed);
            this.start.Stopped += (_, e) => failed($"start watcher stopped ({e.Status})");
            this.stop.Stopped += (_, e) => failed($"stop watcher stopped ({e.Status})");
            this.start.Start();
            this.stop.Start();
        }

        private static void HandleEvent(Action handle, Action<string> failed)
        {
            try
            {
                handle();
            }
            catch (Exception ex)
            {
                failed($"WMI event could not be handled: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Dispose both even if stopping one failed. The owner ignores retired callbacks.
            try
            {
                try
                {
                    this.start.Stop();
                }
                finally
                {
                    this.start.Dispose();
                }
            }
            finally
            {
                try
                {
                    this.stop.Stop();
                }
                finally
                {
                    this.stop.Dispose();
                }
            }
        }
    }

    internal interface IMonitoringWakeEvents : IDisposable
    {
        event EventHandler? Wake;

        void Start();
    }

    internal sealed class WindowsMonitoringWakeEvents : IMonitoringWakeEvents
    {
        public event EventHandler? Wake;

        public void Start()
        {
            SystemEvents.PowerModeChanged += this.OnPowerModeChanged;
            SystemEvents.SessionSwitch += this.OnSessionSwitch;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                this.Wake?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                this.Wake?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            SystemEvents.PowerModeChanged -= this.OnPowerModeChanged;
            SystemEvents.SessionSwitch -= this.OnSessionSwitch;
        }
    }
}
