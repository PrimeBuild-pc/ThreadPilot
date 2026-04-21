/*
 * ThreadPilot - process monitor manager unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for orchestration logic in <see cref="ProcessMonitorManagerService"/>.
    /// </summary>
    public sealed class ProcessMonitorManagerServiceTests
    {
        [Fact]
        public async Task StartAsync_LoadsConfiguration_AndStartsMonitoring()
        {
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService);

            await manager.StartAsync();

            Assert.True(manager.IsRunning);
            Assert.Equal("Running", manager.Status);
            Assert.Equal(1, processMonitor.StartCalls);
            associationService.Verify(x => x.LoadConfigurationAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_SelectsHighestPriorityAssociation()
        {
            var processMonitor = new FakeProcessMonitorService
            {
                RunningProcesses =
                {
                    new ProcessModel { ProcessId = 1, Name = "game-low" },
                    new ProcessModel { ProcessId = 2, Name = "game-high" },
                },
            };

            var configuration = new ProcessMonitorConfiguration
            {
                Associations =
                {
                    new ProcessPowerPlanAssociation("game-low", "plan-low", "Low") { Priority = 1 },
                    new ProcessPowerPlanAssociation("game-high", "plan-high", "High") { Priority = 10 },
                },
                PowerPlanChangeDelayMs = 0,
            };

            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService);

            await manager.StartAsync();

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-high", true),
                Times.Once);
            notificationService.Verify(
                x => x.ShowPowerPlanChangeNotificationAsync("Balanced", "plan-high-name", "game-high"),
                Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_WithDelay_TriggersSingleReevaluation()
        {
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration
            {
                PowerPlanChangeDelayMs = 25,
                Associations =
                {
                    new ProcessPowerPlanAssociation("game", "plan-game", "Game") { Priority = 5 },
                },
            };

            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 10, Name = "game" });
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 11, Name = "game" });

            await Task.Delay(150);

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-game", true),
                Times.Once);
        }

        [Fact]
        public async Task StopAsync_RestoresDefaultPowerPlan_WhenConfigured()
        {
            var processMonitor = new FakeProcessMonitorService
            {
                RunningProcesses =
                {
                    new ProcessModel { ProcessId = 21, Name = "game" },
                },
            };

            var configuration = new ProcessMonitorConfiguration
            {
                DefaultPowerPlanGuid = "plan-default",
                DefaultPowerPlanName = "Balanced",
                PowerPlanChangeDelayMs = 0,
                Associations =
                {
                    new ProcessPowerPlanAssociation("game", "plan-game", "Game") { Priority = 1 },
                },
            };

            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService);

            await manager.StartAsync();
            await manager.StopAsync();

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-default", true),
                Times.AtLeastOnce);
            processService.Verify(x => x.UntrackProcess(21), Times.Once);
            coreMaskService.Verify(x => x.UnregisterMaskApplication(21), Times.Once);
        }

        [Fact]
        public async Task Dispose_CompletesOnBlockingSynchronizationContext()
        {
            var processMonitor = new FakeProcessMonitorService
            {
                StopMonitoringAsyncImpl = async () =>
                {
                    await Task.Yield();
                },
            };

            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService);

            await manager.StartAsync();

            Exception? disposeException = null;
            using var completed = new ManualResetEventSlim(false);
            var disposeThread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());

                try
                {
                    manager.Dispose();
                }
                catch (Exception ex)
                {
                    disposeException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            disposeThread.Start();

            Assert.True(completed.Wait(TimeSpan.FromSeconds(1)), "Dispose did not complete promptly.");
            Assert.Null(disposeException);
            Assert.Equal(1, processMonitor.StopCalls);
            Assert.Equal(1, processMonitor.DisposeCalls);
        }

        private static ProcessMonitorManagerService CreateService(
            FakeProcessMonitorService processMonitorService,
            Mock<IProcessPowerPlanAssociationService> associationService,
            Mock<IPowerPlanService> powerPlanService,
            Mock<INotificationService> notificationService,
            Mock<IProcessService> processService,
            Mock<ICoreMaskService> coreMaskService)
        {
            var enhancedLogger = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            enhancedLogger
                .Setup(x => x.LogSystemEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Microsoft.Extensions.Logging.LogLevel>()))
                .Returns(Task.CompletedTask);
            enhancedLogger
                .Setup(x => x.LogProcessMonitoringEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            enhancedLogger
                .Setup(x => x.LogErrorAsync(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()))
                .Returns(Task.CompletedTask);

            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settingsService.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel());

            return new ProcessMonitorManagerService(
                processMonitorService,
                associationService.Object,
                powerPlanService.Object,
                notificationService.Object,
                settingsService.Object,
                processService.Object,
                coreMaskService.Object,
                NullLogger<ProcessMonitorManagerService>.Instance,
                enhancedLogger.Object);
        }

        private static Mock<IProcessPowerPlanAssociationService> CreateAssociationService(ProcessMonitorConfiguration configuration)
        {
            var associationService = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Strict);
            associationService.SetupGet(x => x.Configuration).Returns(configuration);
            associationService.Setup(x => x.LoadConfigurationAsync()).ReturnsAsync(true);
            return associationService;
        }

        private static Mock<IPowerPlanService> CreatePowerPlanService()
        {
            var powerPlanService = new Mock<IPowerPlanService>(MockBehavior.Strict);
            powerPlanService.Setup(x => x.GetActivePowerPlan()).ReturnsAsync(new PowerPlanModel { Guid = "balanced", Name = "Balanced" });
            powerPlanService.Setup(x => x.SetActivePowerPlanByGuidAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);
            powerPlanService.Setup(x => x.GetPowerPlanByGuidAsync(It.IsAny<string>()))
                .ReturnsAsync((string guid) => new PowerPlanModel { Guid = guid, Name = $"{guid}-name" });
            return powerPlanService;
        }

        private static Mock<INotificationService> CreateNotificationService()
        {
            var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
            notificationService.SetupGet(x => x.NotificationHistory).Returns(Array.Empty<NotificationModel>());
            notificationService.Setup(x => x.ShowPowerPlanChangeNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            notificationService.Setup(x => x.ShowErrorNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception?>()))
                .Returns(Task.CompletedTask);
            notificationService.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);
            return notificationService;
        }

        private static Mock<IProcessService> CreateProcessService()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Strict);
            processService.Setup(x => x.UntrackProcess(It.IsAny<int>()));
            return processService;
        }

        private static Mock<ICoreMaskService> CreateCoreMaskService()
        {
            var coreMaskService = new Mock<ICoreMaskService>(MockBehavior.Strict);
            coreMaskService.SetupGet(x => x.AvailableMasks).Returns(new ObservableCollection<CoreMask>());
            coreMaskService.Setup(x => x.UnregisterMaskApplication(It.IsAny<int>()));
            return coreMaskService;
        }

        private sealed class FakeProcessMonitorService : IProcessMonitorService
        {
            public event EventHandler<ProcessEventArgs>? ProcessStarted;

            public event EventHandler<ProcessEventArgs>? ProcessStopped;

            public event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

            public List<ProcessModel> RunningProcesses { get; } = new();

            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public bool IsMonitoring { get; private set; }

            public bool IsWmiAvailable => false;

            public bool IsFallbackPollingActive => false;

            public Func<Task>? StopMonitoringAsyncImpl { get; init; }

            public Task StartMonitoringAsync()
            {
                this.StartCalls++;
                this.IsMonitoring = true;
                return Task.CompletedTask;
            }

            public Task StopMonitoringAsync()
            {
                this.StopCalls++;
                this.IsMonitoring = false;
                return this.StopMonitoringAsyncImpl?.Invoke() ?? Task.CompletedTask;
            }

            public Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync() =>
                Task.FromResult<IEnumerable<ProcessModel>>(this.RunningProcesses.ToList());

            public Task<bool> IsProcessRunningAsync(string executableName) =>
                Task.FromResult(this.RunningProcesses.Any(x => string.Equals(x.Name, executableName, StringComparison.OrdinalIgnoreCase)));

            public void UpdateSettings()
            {
            }

            public void RaiseProcessStarted(ProcessModel process) =>
                this.ProcessStarted?.Invoke(this, new ProcessEventArgs(process));

            public void Dispose()
            {
                this.DisposeCalls++;
            }
        }

        private sealed class BlockingSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object? state)
            {
                // Intentionally do not pump posted work to emulate a blocked UI thread.
            }
        }
    }
}
