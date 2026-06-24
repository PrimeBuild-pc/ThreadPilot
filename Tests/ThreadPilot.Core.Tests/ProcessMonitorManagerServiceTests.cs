/*
 * ThreadPilot - process monitor manager unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 10, Name = "game" });
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 11, Name = "game" });

            await Task.Delay(150);

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-game", true),
                Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_SamePlanRequest_IsSuppressedWithinDuplicateWindow()
        {
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration
            {
                PowerPlanChangeDelayMs = 0,
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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 10, Name = "game" });
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 11, Name = "game" });

            await Task.Delay(100);

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-game", true),
                Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_WhenPowerPlanChangeFails_DoesNotRetrySamePlanImmediately()
        {
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration
            {
                PowerPlanChangeDelayMs = 0,
                Associations =
                {
                    new ProcessPowerPlanAssociation("game", "plan-game", "Game") { Priority = 5 },
                },
            };

            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            powerPlanService
                .Setup(x => x.SetActivePowerPlanByGuidAsync("plan-game", true))
                .ReturnsAsync(false);
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 10, Name = "game" });
            processMonitor.RaiseProcessStarted(new ProcessModel { ProcessId = 11, Name = "game" });

            await Task.Delay(100);

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-game", true),
                Times.Once);
            notificationService.Verify(
                x => x.ShowPowerPlanChangeNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

            await manager.StartAsync();
            await manager.StopAsync();

            powerPlanService.Verify(
                x => x.SetActivePowerPlanByGuidAsync("plan-default", true),
                Times.AtLeastOnce);
            processService.Verify(x => x.UntrackProcess(21), Times.Once);
            coreMaskService.Verify(x => x.UnregisterMaskApplication(21), Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_AppliesConfiguredCoreMaskForMatchingProcess()
        {
            var process = new ProcessModel { ProcessId = 31, Name = "game" };
            var processMonitor = new FakeProcessMonitorService();

            var configuration = new ProcessMonitorConfiguration
            {
                PowerPlanChangeDelayMs = 0,
                Associations =
                {
                    new ProcessPowerPlanAssociation("game", "plan-game", "Game")
                    {
                        CoreMaskId = "mask-game",
                        Priority = 5,
                    },
                },
            };

            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            processService.Setup(x => x.TrackAppliedMask(31, "mask-game"));
            var coreMaskService = CreateCoreMaskService();
            coreMaskService.SetupGet(x => x.AvailableMasks).Returns(new ObservableCollection<CoreMask>
            {
                new()
                {
                    Id = "mask-game",
                    Name = "Game Mask",
                    BoolMask = new ObservableCollection<bool> { true, false },
                },
            });
            coreMaskService.Setup(x => x.RegisterMaskApplication(31, "mask-game"));
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            affinityApplyService.Verify(x => x.ApplyAsync(process, 1), Times.Once);
            processService.Verify(x => x.TrackAppliedMask(31, "mask-game"), Times.Once);
            coreMaskService.Verify(x => x.RegisterMaskApplication(31, "mask-game"), Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_AppliesPersistentRulesThroughCoordinator()
        {
            var process = new ProcessModel { ProcessId = 41, Name = "game.exe" };
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            autoApplyService.Verify(
                x => x.ApplyForDiscoveredProcessesAsync(
                    It.IsAny<IEnumerable<ProcessModel>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            autoApplyService.Verify(
                x => x.ApplyForProcessStartAsync(process, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EvaluateCurrentProcessesAsync_WhenPersistentRuleSnapshotApplyCancels_DoesNotLogWarning()
        {
            var processMonitor = new FakeProcessMonitorService
            {
                RunningProcesses =
                {
                    new ProcessModel { ProcessId = 42, Name = "game.exe" },
                },
            };
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForDiscoveredProcessesAsync(
                    It.IsAny<IEnumerable<ProcessModel>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var logger = new CapturingLogger<ProcessMonitorManagerService>();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService,
                logger);

            await manager.StartAsync();
            await manager.EvaluateCurrentProcessesAsync();

            Assert.Empty(logger.WarningMessages);
        }

        [Fact]
        public async Task ProcessStarted_WhenPersistentRuleAutoApplyCancels_DoesNotLogWarning()
        {
            var process = new ProcessModel { ProcessId = 43, Name = "game.exe" };
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForProcessStartAsync(
                    process,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var logger = new CapturingLogger<ProcessMonitorManagerService>();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService,
                logger);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            Assert.Empty(logger.WarningMessages);
        }

        [Fact]
        public async Task EvaluateCurrentProcessesAsync_WhenPersistentRuleAutoApplyThrows_LogsWarningWithoutBreakingRefresh()
        {
            var processMonitor = new FakeProcessMonitorService
            {
                RunningProcesses =
                {
                    new ProcessModel { ProcessId = 44, Name = "game.exe" },
                },
            };
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForDiscoveredProcessesAsync(
                    It.IsAny<IEnumerable<ProcessModel>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("auto apply failed"));
            var logger = new CapturingLogger<ProcessMonitorManagerService>();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService,
                logger);

            await manager.StartAsync();
            await manager.EvaluateCurrentProcessesAsync();

            Assert.Contains(
                logger.WarningMessages,
                message => message.Contains("snapshot refresh", StringComparison.OrdinalIgnoreCase));
            Assert.True(manager.IsRunning);
        }

        [Fact]
        public async Task ProcessStarted_WhenPersistentRuleAutoApplyThrows_LogsWarningWithoutBreakingStartHandling()
        {
            var process = new ProcessModel { ProcessId = 45, Name = "game.exe" };
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForProcessStartAsync(
                    process,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("auto apply failed"));
            var logger = new CapturingLogger<ProcessMonitorManagerService>();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService,
                logger);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            Assert.Contains(
                logger.WarningMessages,
                message => message.Contains("Persistent rule auto-apply failed", StringComparison.OrdinalIgnoreCase));
            Assert.True(manager.IsRunning);
        }

        [Fact]
        public async Task ProcessStarted_WhenPersistentRuleAutoApplySucceeds_LogsEnhancedMonitoringEvent()
        {
            var process = new ProcessModel { ProcessId = 46, Name = "game.exe" };
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForProcessStartAsync(
                    process,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistentRuleAutoApplyResult
                    {
                        Success = true,
                        RuleId = "rule-game",
                        ProcessId = process.ProcessId,
                        ProcessName = process.Name,
                        UserMessage = "Persistent rule applied.",
                    },
                });
            var enhancedLogger = CreateEnhancedLogger();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService,
                enhancedLogger: enhancedLogger);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            enhancedLogger.Verify(
                x => x.LogProcessMonitoringEventAsync(
                    LogEventTypes.ProcessMonitoring.AssociationTriggered,
                    process.Name,
                    process.ProcessId,
                    It.Is<string>(message => message.Contains("Persistent rule 'rule-game' applied automatically", StringComparison.Ordinal))),
                Times.Once);
        }

        [Fact]
        public async Task ProcessStarted_WhenPersistentRuleAutoApplyReturnsFailure_DoesNotNotifyOrThrow()
        {
            var process = new ProcessModel { ProcessId = 47, Name = "game.exe" };
            var processMonitor = new FakeProcessMonitorService();
            var configuration = new ProcessMonitorConfiguration();
            var associationService = CreateAssociationService(configuration);
            var powerPlanService = CreatePowerPlanService();
            var notificationService = CreateNotificationService();
            var processService = CreateProcessService();
            var coreMaskService = CreateCoreMaskService();
            var affinityApplyService = CreateAffinityApplyService();
            var autoApplyService = CreateAutoApplyService();
            autoApplyService
                .Setup(x => x.ApplyForProcessStartAsync(
                    process,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistentRuleAutoApplyResult
                    {
                        Success = false,
                        RuleId = "rule-game",
                        ProcessId = process.ProcessId,
                        ProcessName = process.Name,
                        UserMessage = ProcessOperationUserMessages.AccessDenied,
                        IsAccessDenied = true,
                    },
                });
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService,
                autoApplyService);

            await manager.StartAsync();
            processMonitor.RaiseProcessStarted(process);
            await Task.Delay(100);

            Assert.True(manager.IsRunning);
            notificationService.Verify(
                x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
            notificationService.Verify(
                x => x.ShowErrorNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception?>()),
                Times.Never);
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
            var affinityApplyService = CreateAffinityApplyService();
            var manager = CreateService(
                processMonitor,
                associationService,
                powerPlanService,
                notificationService,
                processService,
                coreMaskService,
                affinityApplyService);

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
            Mock<ICoreMaskService> coreMaskService,
            Mock<IAffinityApplyService> affinityApplyService,
            Mock<IPersistentRuleAutoApplyService>? autoApplyService = null,
            ILogger<ProcessMonitorManagerService>? logger = null,
            Mock<IEnhancedLoggingService>? enhancedLogger = null)
        {
            var resolvedEnhancedLogger = enhancedLogger ?? CreateEnhancedLogger();

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
                affinityApplyService.Object,
                (autoApplyService ?? CreateAutoApplyService()).Object,
                new PowerPlanTransitionGate(TimeSpan.FromSeconds(2), () => DateTimeOffset.UtcNow),
                logger ?? NullLogger<ProcessMonitorManagerService>.Instance,
                resolvedEnhancedLogger.Object);
        }

        private static Mock<IEnhancedLoggingService> CreateEnhancedLogger()
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
            return enhancedLogger;
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

        private static Mock<IAffinityApplyService> CreateAffinityApplyService()
        {
            var affinityApplyService = new Mock<IAffinityApplyService>(MockBehavior.Strict);
            affinityApplyService
                .Setup(x => x.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<long>()))
                .ReturnsAsync((ProcessModel process, long affinity) =>
                    AffinityApplyResult.Succeeded(affinity, affinity));
            return affinityApplyService;
        }

        private static Mock<IPersistentRuleAutoApplyService> CreateAutoApplyService()
        {
            var autoApplyService = new Mock<IPersistentRuleAutoApplyService>(MockBehavior.Strict);
            autoApplyService
                .Setup(x => x.ApplyForDiscoveredProcessesAsync(
                    It.IsAny<IEnumerable<ProcessModel>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PersistentRuleAutoApplyResult>());
            autoApplyService
                .Setup(x => x.ApplyForProcessStartAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PersistentRuleAutoApplyResult>());
            autoApplyService.Setup(x => x.MarkProcessExited(It.IsAny<int>()));
            return autoApplyService;
        }

        private sealed class FakeProcessMonitorService : IProcessMonitorService
        {
            public event EventHandler<ProcessEventArgs>? ProcessStarted;

            public event EventHandler<ProcessEventArgs>? ProcessStopped
            {
                add { }
                remove { }
            }

            public event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged
            {
                add { }
                remove { }
            }

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

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> WarningMessages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    this.WarningMessages.Add(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
