namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class ProcessViewModelAffinityTests
    {
        [Fact]
        public async Task SelectingCoreMask_DoesNotApplyProcessorAffinity()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var viewModel = CreateViewModel(processService.Object, gameModeService.Object);

            viewModel.SelectedProcess = new ProcessModel
            {
                ProcessId = 1234,
                Name = "Game",
                ProcessorAffinity = 3,
            };

            viewModel.SelectedCoreMask = CoreMask.FromProcessorAffinity(1, 2, "First Core");

            await Task.Delay(100);

            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<long>()),
                Times.Never);
        }

        [Fact]
        public async Task SelectingCoreMask_ReportsPendingAffinityWithoutChangingCurrentAffinity()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var viewModel = CreateViewModel(processService.Object, gameModeService.Object);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(
                viewModel.CpuTopology.LogicalCores);

            viewModel.SelectedProcess = new ProcessModel
            {
                ProcessId = 1234,
                Name = "Game",
                ProcessorAffinity = 3,
            };

            viewModel.SelectedCoreMask = CoreMask.FromProcessorAffinity(1, 2, "First Core");

            await Task.Delay(100);

            Assert.True(viewModel.HasPendingAffinityEdits);
            Assert.Equal("Current OS affinity: 0x3", viewModel.CurrentAffinityText);
            Assert.Equal("Pending core mask: 0x1", viewModel.PendingAffinityText);
            Assert.Equal("CPU selection staged. Use Apply CPU Assignment to apply it.", viewModel.AffinityEditStateText);
        }

        [Fact]
        public void ConstructorFallbackCoordinator_ReceivesTopologyProviderWhenProvided()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var topologyProvider = new Mock<ICpuTopologyProvider>(MockBehavior.Strict);

            var viewModel = CreateViewModel(
                processService.Object,
                gameModeService.Object,
                cpuTopologyProvider: topologyProvider.Object);

            var coordinator = typeof(ProcessViewModel)
                .GetField(
                    "processAffinityApplyCoordinator",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(viewModel);
            var provider = typeof(ProcessAffinityApplyCoordinator)
                .GetField(
                    "cpuTopologyProvider",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(coordinator);

            Assert.Same(topologyProvider.Object, provider);
        }

        [Fact]
        public void PollingInterval_InitializesAndUpdatesFromSettings()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var initial = new ApplicationSettingsModel { PollingIntervalMs = 2345 };
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settingsService.SetupGet(service => service.Settings).Returns(initial);
            using var viewModel = CreateViewModel(
                processService.Object,
                gameModeService.Object,
                cpuTopologyProvider: null,
                settingsService: settingsService.Object);
            var timer = (System.Timers.Timer)typeof(ProcessViewModel)
                .GetField("refreshTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(viewModel)!;

            Assert.Equal(2345, timer.Interval);

            var updated = initial.Clone();
            updated.PollingIntervalMs = 6789;
            settingsService.Raise(
                service => service.SettingsChanged += null,
                new ApplicationSettingsChangedEventArgs(initial, updated, [nameof(ApplicationSettingsModel.PollingIntervalMs)]));

            Assert.Equal(6789, timer.Interval);
        }

        [Fact]
        public void SelectingProcessInCpuSetsMode_SynchronizesCheckboxesFromWindowsCpuSets()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            processService.Setup(service => service.IsProcessStillRunning(It.IsAny<ProcessModel>())).ReturnsAsync(true);
            processService.Setup(service => service.RefreshProcessInfo(It.IsAny<ProcessModel>())).Returns(Task.CompletedTask);
            processService
                .Setup(service => service.GetCpuAssignmentLogicalProcessorIndexesAsync(
                    It.IsAny<ProcessModel>(), CpuAssignmentMode.CpuSets))
                .ReturnsAsync([0]);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);
            var settings = new ApplicationSettingsModel { DefaultCpuAssignmentMode = CpuAssignmentMode.AffinityMask };
            var settingsService = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settingsService.SetupGet(service => service.Settings).Returns(settings);
            using var viewModel = CreateViewModel(
                processService.Object,
                gameModeService.Object,
                cpuTopologyProvider: null,
                settingsService: settingsService.Object);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(
                viewModel.CpuTopology.LogicalCores);

            viewModel.SelectedProcess = new ProcessModel
            {
                ProcessId = 1234,
                Name = "Game",
                ProcessorAffinity = 3,
            };

            Assert.True(SpinWait.SpinUntil(
                () => viewModel.CpuCores[0].IsSelected && !viewModel.CpuCores[1].IsSelected,
                TimeSpan.FromSeconds(2)));
            Assert.Equal("Current CPU Sets: CPU 0 (set by hand, by the app itself or by another tool)", viewModel.CurrentAffinityText);
        }

        [Fact]
        public void SelectingProcessBoostedByGameBoost_SaysGameBoostSetTheCpuSets()
        {
            var processService = CreateRunningProcessService(cpuSets: [0]);
            var session = new Mock<IGameSessionService>(MockBehavior.Loose);
            session.Setup(service => service.HasAppliedCpuSets(1234)).Returns(true);
            using var viewModel = CreateViewModel(
                processService.Object,
                new Mock<IGameModeService>(MockBehavior.Loose).Object,
                cpuTopologyProvider: null,
                settingsService: null,
                gameSessionService: session.Object);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(viewModel.CpuTopology.LogicalCores);

            viewModel.SelectedProcess = new ProcessModel { ProcessId = 1234, Name = "Game", ProcessorAffinity = 3 };

            var expected = "Current CPU Sets: CPU 0 (set by Game Boost while the game runs)";
            Assert.True(SpinWait.SpinUntil(() => viewModel.CurrentAffinityText == expected, TimeSpan.FromSeconds(2)), viewModel.CurrentAffinityText);
        }

        [Fact]
        public void SelectingProcessWithCpuSets_DoesNotLeaveTheAllCoresMaskSelected()
        {
            var processService = CreateRunningProcessService(cpuSets: [0]);
            var allCores = CoreMask.CreateAllCoresMask(2);
            var masks = new System.Collections.ObjectModel.ObservableCollection<CoreMask> { allCores };
            var coreMaskService = new Mock<ICoreMaskService>(MockBehavior.Loose);
            coreMaskService.SetupGet(service => service.AvailableMasks).Returns(masks);
            coreMaskService.SetupGet(service => service.DefaultMask).Returns(allCores);
            using var viewModel = CreateViewModel(
                processService.Object,
                new Mock<IGameModeService>(MockBehavior.Loose).Object,
                cpuTopologyProvider: null,
                coreMaskService: coreMaskService.Object);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(viewModel.CpuTopology.LogicalCores);
            viewModel.AvailableCoreMasks = masks;
            viewModel.SelectedCoreMask = allCores;

            viewModel.SelectedProcess = new ProcessModel { ProcessId = 1234, Name = "Game", ProcessorAffinity = 3 };

            Assert.True(SpinWait.SpinUntil(
                () => viewModel.SelectedCoreMask == null && viewModel.CoreMaskPickerHint.Contains("All Cores", StringComparison.Ordinal),
                TimeSpan.FromSeconds(2)),
                viewModel.CoreMaskPickerHint);
            Assert.False(viewModel.HasPendingAffinityEdits);
            Assert.True(viewModel.CpuCores[0].IsSelected);
            Assert.False(viewModel.CpuCores[1].IsSelected);
        }

        [Fact]
        public void ReadCpuSetDiagnostic_ShowsOverrideSeparatelyFromDeniedReads()
        {
            var processService = CreateRunningProcessService(cpuSets: []);
            using var viewModel = CreateViewModel(processService.Object, new Mock<IGameModeService>(MockBehavior.Loose).Object, cpuTopologyProvider: null);
            viewModel.SelectedProcess = new ProcessModel { ProcessId = 1234, Name = "Game", ProcessorAffinity = 3 };
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint> { [new ProcessorRef(0, 1, 1)] = 10 });
            viewModel.CpuSetDiagnosticSource = new FakeCpuSetDiagnosticSource(
                ThreadCpuSetDiagnosticClassifier.Combine(
                    new CpuSetIdRead(CpuSetReadKind.Read, [10], 0),
                    new ThreadCpuSetProbeResult(
                        true,
                        null,
                        [
                            ThreadCpuSetProbe.NoOverride(1, null, (false, 0, 0)),
                            ThreadCpuSetProbe.Override(2, [10], null, (false, 0, 0)),
                            ThreadCpuSetProbe.OpenFailed(3, 5),
                        ]),
                    mapping));

            viewModel.ReadCpuSetDiagnosticCommand.Execute(null);

            Assert.True(SpinWait.SpinUntil(() => viewModel.CpuSetDiagnosticText.Contains("TID 2", StringComparison.Ordinal), TimeSpan.FromSeconds(2)), viewModel.CpuSetDiagnosticText);
            Assert.Contains("explicit override", viewModel.CpuSetDiagnosticText, StringComparison.Ordinal);
            Assert.Contains("TID 3: read denied.", viewModel.CpuSetDiagnosticText, StringComparison.Ordinal);
            Assert.Contains("not the CPU a thread is running on", viewModel.CpuSetDiagnosticText, StringComparison.Ordinal);
        }

        [Fact]
        public void SelectingProcessWithoutCpuSets_SaysWindowsPicksTheCores()
        {
            var processService = CreateRunningProcessService(cpuSets: []);
            using var viewModel = CreateViewModel(processService.Object, new Mock<IGameModeService>(MockBehavior.Loose).Object, cpuTopologyProvider: null);
            viewModel.CpuTopology = CreateTwoCoreTopology();
            viewModel.CpuCores = new System.Collections.ObjectModel.ObservableCollection<CpuCoreModel>(viewModel.CpuTopology.LogicalCores);

            viewModel.SelectedProcess = new ProcessModel { ProcessId = 1234, Name = "Game", ProcessorAffinity = 3 };

            var expected = "Current OS affinity: 0x3 (no CPU Sets, Windows picks the cores)";
            Assert.True(SpinWait.SpinUntil(() => viewModel.CurrentAffinityText == expected, TimeSpan.FromSeconds(2)), viewModel.CurrentAffinityText);
        }

        private static Mock<IProcessService> CreateRunningProcessService(IReadOnlyList<int> cpuSets)
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            processService.Setup(service => service.IsProcessStillRunning(It.IsAny<ProcessModel>())).ReturnsAsync(true);
            processService.Setup(service => service.RefreshProcessInfo(It.IsAny<ProcessModel>())).Returns(Task.CompletedTask);
            processService
                .Setup(service => service.GetCpuAssignmentLogicalProcessorIndexesAsync(It.IsAny<ProcessModel>(), CpuAssignmentMode.CpuSets))
                .ReturnsAsync(cpuSets);
            return processService;
        }

        private static ProcessViewModel CreateViewModel(IProcessService processService, IGameModeService gameModeService)
            => CreateViewModel(processService, gameModeService, cpuTopologyProvider: null);

        private static ProcessViewModel CreateViewModel(
            IProcessService processService,
            IGameModeService gameModeService,
            ICpuTopologyProvider? cpuTopologyProvider,
            IApplicationSettingsService? settingsService = null,
            IGameSessionService? gameSessionService = null,
            ICoreMaskService? coreMaskService = null)
        {
            var virtualizedProcessService = new Mock<IVirtualizedProcessService>(MockBehavior.Loose);
            virtualizedProcessService.SetupProperty(
                service => service.Configuration,
                new VirtualizedProcessConfig());

            var cpuTopologyService = new Mock<ICpuTopologyService>(MockBehavior.Loose);
            var powerPlanService = new Mock<IPowerPlanService>(MockBehavior.Loose);
            var notificationService = new Mock<INotificationService>(MockBehavior.Loose);
            var systemTrayService = new Mock<ISystemTrayService>(MockBehavior.Loose);
            coreMaskService ??= new Mock<ICoreMaskService>(MockBehavior.Loose).Object;
            var associationService = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);

            return new ProcessViewModel(
                NullLogger<ProcessViewModel>.Instance,
                processService,
                new ProcessFilterService(),
                virtualizedProcessService.Object,
                cpuTopologyService.Object,
                powerPlanService.Object,
                notificationService.Object,
                systemTrayService.Object,
                coreMaskService,
                associationService.Object,
                gameModeService,
                cpuTopologyProvider: cpuTopologyProvider,
                settingsService: settingsService,
                gameSessionService: gameSessionService);
        }

        private sealed class FakeCpuSetDiagnosticSource : ICpuSetDiagnosticSource
        {
            private readonly ProcessCpuSetDiagnostic diagnostic;

            public FakeCpuSetDiagnosticSource(ProcessCpuSetDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public ProcessCpuSetDiagnostic Read(int processId, string name) => this.diagnostic;
        }

        private static CpuTopologyModel CreateTwoCoreTopology()
        {
            return new CpuTopologyModel
            {
                LogicalCores =
                [
                    new CpuCoreModel { LogicalCoreId = 0, PhysicalCoreId = 0, Label = "CPU 0" },
                    new CpuCoreModel { LogicalCoreId = 1, PhysicalCoreId = 1, Label = "CPU 1" },
                ],
            };
        }
    }
}
