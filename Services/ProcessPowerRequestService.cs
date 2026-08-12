namespace ThreadPilot.Services
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;

    public sealed partial class ProcessPowerRequestService : IDisposable
    {
        private readonly Dictionary<int, RequestEntry> requests = [];
        private readonly object sync = new();
        private readonly ILogger<ProcessPowerRequestService> logger;
        private bool disposed;

        public ProcessPowerRequestService(ILogger<ProcessPowerRequestService> logger)
        {
            this.logger = logger;
        }

        public Task<bool> SetPreventSleepAsync(ProcessModel process, bool enabled)
        {
            ArgumentNullException.ThrowIfNull(process);
            if (!enabled)
            {
                this.Release(process.ProcessId);
                return Task.FromResult(true);
            }

            lock (this.sync)
            {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                if (this.requests.ContainsKey(process.ProcessId))
                {
                    return Task.FromResult(true);
                }
            }

            try
            {
                var target = Process.GetProcessById(process.ProcessId);
                var reasonPointer = Marshal.StringToHGlobalUni($"ThreadPilot: {process.Name} is running");
                SafeFileHandle request;
                try
                {
                    var context = new ReasonContext { Version = 0, Flags = 1, SimpleReasonString = reasonPointer };
                    request = NativeMethods.PowerCreateRequest(ref context);
                }
                finally
                {
                    Marshal.FreeHGlobal(reasonPointer);
                }

                if (request.IsInvalid || !NativeMethods.PowerSetRequest(request, PowerRequestType.SystemRequired))
                {
                    request.Dispose();
                    target.Dispose();
                    return Task.FromResult(false);
                }

                EventHandler exited = (_, _) => this.Release(process.ProcessId);
                target.EnableRaisingEvents = true;
                target.Exited += exited;
                lock (this.sync)
                {
                    if (this.disposed || this.requests.ContainsKey(process.ProcessId))
                    {
                        target.Exited -= exited;
                        _ = NativeMethods.PowerClearRequest(request, PowerRequestType.SystemRequired);
                        request.Dispose();
                        target.Dispose();
                        return Task.FromResult(!this.disposed);
                    }

                    this.requests[process.ProcessId] = new RequestEntry(target, request, exited);
                }

                if (target.HasExited)
                {
                    this.Release(process.ProcessId);
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Could not create power request for PID {ProcessId}", process.ProcessId);
                return Task.FromResult(false);
            }
        }

        public void Release(int processId)
        {
            RequestEntry? entry;
            lock (this.sync)
            {
                if (!this.requests.Remove(processId, out entry))
                {
                    return;
                }
            }

            entry.Process.Exited -= entry.ExitedHandler;
            _ = NativeMethods.PowerClearRequest(entry.Request, PowerRequestType.SystemRequired);
            entry.Request.Dispose();
            entry.Process.Dispose();
        }

        public void Dispose()
        {
            int[] processIds;
            lock (this.sync)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                processIds = [.. this.requests.Keys];
            }

            foreach (var processId in processIds)
            {
                this.Release(processId);
            }
        }

        private sealed record RequestEntry(Process Process, SafeFileHandle Request, EventHandler ExitedHandler);

        private enum PowerRequestType
        {
            DisplayRequired = 0,
            SystemRequired = 1,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ReasonContext
        {
            public uint Version;
            public uint Flags;
            public IntPtr SimpleReasonString;
        }

        private static partial class NativeMethods
        {
            [LibraryImport("kernel32.dll", SetLastError = true)]
            internal static partial SafeFileHandle PowerCreateRequest(ref ReasonContext context);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool PowerSetRequest(SafeFileHandle request, PowerRequestType requestType);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool PowerClearRequest(SafeFileHandle request, PowerRequestType requestType);
        }
    }
}
