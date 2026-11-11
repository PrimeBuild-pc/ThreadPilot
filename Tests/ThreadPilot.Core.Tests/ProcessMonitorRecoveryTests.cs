namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessMonitorRecoveryTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LostMonitoring_RecreatesWatchersAndRefreshesExistingProcesses(bool resume)
        {
            var wake = new FakeWakeEvents();
            var created = new List<FakeWatchers>();
            var processes = new ObservableCollection<ProcessModel>();
            var source = new Mock<IProcessService>();
            source.Setup(x => x.GetProcessesAsync()).ReturnsAsync(() => new ObservableCollection<ProcessModel>(processes));
            var settings = new Mock<IApplicationSettingsService>();
            settings.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel
            {
                EnableWmiMonitoring = true,
                EnableFallbackPolling = false,
            });
            var recreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var monitor = new ProcessMonitorService(source.Object, settings.Object,
                NullLogger<ProcessMonitorService>.Instance, () =>
                {
                    var watchers = new FakeWatchers();
                    created.Add(watchers);
                    return watchers;
                }, wake);
            monitor.MonitoringStatusChanged += async (_, _) =>
            {
                if (created.Count >= 2 && (await monitor.GetRunningProcessesAsync()).Any(x => x.ProcessId == 123))
                {
                    recreated.TrySetResult();
                }
            };

            await monitor.StartMonitoringAsync();
            processes.Add(new ProcessModel { ProcessId = 123, Name = "already-running" });
            if (resume)
            {
                wake.Raise();
            }
            else
            {
                created[0].Fail("subscription cancelled");
            }

            await recreated.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(created[0].Disposed);
            Assert.True(monitor.IsWmiAvailable);
            Assert.Contains(await monitor.GetRunningProcessesAsync(), p => p.ProcessId == 123);
        }

        [Fact]
        public async Task Start_SnapshotsAfterSubscribingSoProcessesStartedDuringWatcherSetupAreIncluded()
        {
            using var fixture = new MonitorFixture();
            fixture.Source.Setup(x => x.GetProcessesAsync()).ReturnsAsync(() =>
                fixture.Created.Count == 0 ? new ObservableCollection<ProcessModel>() :
                new ObservableCollection<ProcessModel> { new() { ProcessId = 123, Name = "started-during-subscription" } });

            await fixture.Monitor.StartMonitoringAsync();

            Assert.Contains(await fixture.Monitor.GetRunningProcessesAsync(), p => p.ProcessId == 123);
        }

        [Fact]
        public async Task Watchdog_RecreatesSilentWatchersAndIgnoresRetiredCallbacks()
        {
            using var fixture = new MonitorFixture();
            await fixture.Monitor.StartMonitoringAsync();
            fixture.Processes.Add(new ProcessModel { ProcessId = 123, Name = "missed" });

            await fixture.Monitor.CheckHealthAsync(DateTime.UtcNow.AddMinutes(3));
            fixture.Created[0].Started(999, "stale.exe");
            fixture.Created[0].Fail("late Stopped callback");

            Assert.Equal(2, fixture.Created.Count);
            Assert.True(fixture.Created[0].Disposed);
            Assert.Contains(await fixture.Monitor.GetRunningProcessesAsync(), p => p.ProcessId == 123);
            Assert.DoesNotContain(await fixture.Monitor.GetRunningProcessesAsync(), p => p.ProcessId == 999);
            Assert.True(fixture.Monitor.IsWmiAvailable);
        }

        [Fact]
        public async Task WmiUnavailable_PollingDetectsNewProcessesAndWatchdogRetriesWithBackoff()
        {
            using var fixture = new MonitorFixture(failWmi: true, fallback: true);
            var detected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Monitor.ProcessStarted += (_, e) =>
            {
                if (e.Process.ProcessId == 123)
                {
                    detected.TrySetResult();
                }
            };
            await fixture.Monitor.StartMonitoringAsync();
            Assert.True(fixture.Monitor.IsFallbackPollingActive);
            Assert.False(fixture.Monitor.IsWmiAvailable);
            fixture.Processes.Add(new ProcessModel { ProcessId = 123, Name = "polling" });
            await detected.Task.WaitAsync(TimeSpan.FromSeconds(4));

            var now = DateTime.UtcNow;
            await fixture.Monitor.CheckHealthAsync(now);
            Assert.Single(fixture.Created);
            await fixture.Monitor.CheckHealthAsync(now.AddSeconds(11));
            Assert.Equal(2, fixture.Created.Count);
            await fixture.Monitor.CheckHealthAsync(now.AddSeconds(12));
            Assert.Equal(2, fixture.Created.Count);

            fixture.FailWmi = false;
            await fixture.Monitor.CheckHealthAsync(now.AddMinutes(1));
            Assert.True(fixture.Monitor.IsWmiAvailable);
            Assert.False(fixture.Monitor.IsFallbackPollingActive);
        }

        [Fact]
        public async Task StopDuringRecovery_LeavesMonitoringStoppedAndWakeUnsubscribed()
        {
            using var fixture = new MonitorFixture();
            await fixture.Monitor.StartMonitoringAsync();
            var enumerating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<ObservableCollection<ProcessModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Source.Setup(x => x.GetProcessesAsync()).Returns(() =>
            {
                enumerating.TrySetResult();
                return release.Task;
            });
            var recovery = fixture.Monitor.CheckHealthAsync(DateTime.UtcNow.AddMinutes(3));
            await enumerating.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var stop = fixture.Monitor.StopMonitoringAsync();
            release.SetResult(new ObservableCollection<ProcessModel>());
            await Task.WhenAll(recovery, stop).WaitAsync(TimeSpan.FromSeconds(3));
            fixture.Wake.Raise();
            await fixture.Monitor.CheckHealthAsync(DateTime.UtcNow.AddHours(1));

            Assert.False(fixture.Monitor.IsMonitoring);
            Assert.False(fixture.Monitor.IsWmiAvailable);
            Assert.All(fixture.Created, w => Assert.True(w.Disposed));
            Assert.Equal(2, fixture.Created.Count);
        }

        private sealed class MonitorFixture : IDisposable
        {
            public MonitorFixture(bool failWmi = false, bool fallback = false)
            {
                this.FailWmi = failWmi;
                this.Source.Setup(x => x.GetProcessesAsync()).ReturnsAsync(() => new ObservableCollection<ProcessModel>(this.Processes));
                var settings = new Mock<IApplicationSettingsService>();
                settings.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel
                {
                    EnableWmiMonitoring = true,
                    EnableFallbackPolling = fallback,
                    FallbackPollingIntervalMs = 1000,
                });
                this.Monitor = new ProcessMonitorService(this.Source.Object, settings.Object,
                    NullLogger<ProcessMonitorService>.Instance, () =>
                    {
                        var watchers = new FakeWatchers { ThrowOnStart = this.FailWmi };
                        this.Created.Add(watchers);
                        return watchers;
                    }, this.Wake);
            }

            public Mock<IProcessService> Source { get; } = new();
            public ObservableCollection<ProcessModel> Processes { get; } = new();

            public FakeWakeEvents Wake { get; } = new();

            public List<FakeWatchers> Created { get; } = new();

            public ProcessMonitorService Monitor { get; }

            public bool FailWmi { get; set; }

            public void Dispose() => this.Monitor.Dispose();
        }

        internal sealed class FakeWatchers : IProcessWatchers
        {
            public Action<string> Fail { get; private set; } = _ => { };

            public Action<int, string> Started { get; private set; } = (_, _) => { };

            public bool Disposed { get; private set; }

            public bool ThrowOnStart { get; init; }

            public void Start(Action<int, string> started, Action<int> stopped, Action<string> failed)
            {
                this.Started = started;
                this.Fail = failed;
                if (this.ThrowOnStart)
                {
                    throw new InvalidOperationException("WMI unavailable");
                }
            }

            public void Dispose() => this.Disposed = true;
        }

        internal sealed class FakeWakeEvents : IMonitoringWakeEvents
        {
            public event EventHandler? Wake;
            public void Start()
            {
            }
            public void Raise() => this.Wake?.Invoke(this, EventArgs.Empty);
            public void Dispose()
            {
            }
        }
    }
}
