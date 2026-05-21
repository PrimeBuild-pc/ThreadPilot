namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class ProcessViewModelContextMenuTests
    {
        [Fact]
        public async Task ContextCpuPriorityCommand_CallsSafePriorityServicePath()
        {
            var processService = CreateProcessService();
            var viewModel = CreateViewModel(processService.Object);
            var process = CreateProcess(priority: ProcessPriorityClass.Normal);

            await viewModel.SetContextHighPriorityCommand.ExecuteAsync(process);

            processService.Verify(
                service => service.SetProcessPriority(process, ProcessPriorityClass.High),
                Times.Once);
            Assert.Equal(ProcessOperationUserMessages.HighPriorityWarning, viewModel.StatusMessage);
            Assert.False(viewModel.HasError);
        }

        [Fact]
        public async Task ApplyContextAffinityCommand_UsesProvidedRowProcess()
        {
            var processService = CreateProcessService();
            var coordinator = CreateAffinityCoordinator();
            var viewModel = CreateViewModel(
                processService.Object,
                processAffinityApplyCoordinator: coordinator.Object);
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
                new CpuCoreModel { LogicalCoreId = 1, IsSelected = false },
            ];
            var rowProcess = CreateProcess(processId: 100);

            await viewModel.ApplyContextAffinityCommand.ExecuteAsync(rowProcess);

            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    rowProcess,
                    It.Is<IReadOnlyList<bool>>(mask => mask.Count == 2 && mask[0] && !mask[1]),
                    "Manual Process tab context menu CPU selection",
                    default),
                Times.Once);
            Assert.Same(rowProcess, viewModel.SelectedProcess);
        }

        [Fact]
        public async Task ApplyContextAffinityCommand_WhenRowProcessDiffersFromSelectedProcess_UsesRowProcess()
        {
            var processService = CreateProcessService();
            var coordinator = CreateAffinityCoordinator();
            var viewModel = CreateViewModel(
                processService.Object,
                processAffinityApplyCoordinator: coordinator.Object);
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
                new CpuCoreModel { LogicalCoreId = 1, IsSelected = true },
            ];
            var oldSelectedProcess = CreateProcess(processId: 1, name: "Old.exe");
            var rowProcess = CreateProcess(processId: 2, name: "Row.exe");
            viewModel.SelectedProcess = oldSelectedProcess;

            await viewModel.ApplyContextAffinityCommand.ExecuteAsync(rowProcess);

            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    rowProcess,
                    It.IsAny<IReadOnlyList<bool>>(),
                    "Manual Process tab context menu CPU selection",
                    default),
                Times.Once);
            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    oldSelectedProcess,
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<string>(),
                    default),
                Times.Never);
            Assert.Same(rowProcess, viewModel.SelectedProcess);
        }

        [Fact]
        public async Task ApplyContextAffinityCommand_DoesNotCallLegacyLongDirectly()
        {
            var processService = CreateProcessService();
            var coordinator = CreateAffinityCoordinator();
            var viewModel = CreateViewModel(
                processService.Object,
                processAffinityApplyCoordinator: coordinator.Object);
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
            ];
            var rowProcess = CreateProcess();

            await viewModel.ApplyContextAffinityCommand.ExecuteAsync(rowProcess);

            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    rowProcess,
                    It.IsAny<IReadOnlyList<bool>>(),
                    "Manual Process tab context menu CPU selection",
                    default),
                Times.Once);
            processService.Verify(
                service => service.SetProcessorAffinity(It.IsAny<ProcessModel>(), It.IsAny<long>()),
                Times.Never);
        }

        [Fact]
        public void ContextCpuPriorityActions_DoNotExposeRealtimeAsNormalAction()
        {
            var viewModel = CreateViewModel(CreateProcessService().Object);

            Assert.DoesNotContain(ProcessPriorityClass.RealTime, viewModel.ContextMenuCpuPriorityActions);
            Assert.Contains(ProcessPriorityClass.High, viewModel.ContextMenuCpuPriorityActions);
        }

        [Fact]
        public async Task ContextMemoryPriorityCommand_CallsMemoryPriorityService()
        {
            var memoryPriorityService = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriorityService
                .Setup(service => service.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), ProcessMemoryPriority.Low))
                .ReturnsAsync(ProcessOperationResult.Succeeded("Memory priority applied.", "ok"));
            memoryPriorityService
                .Setup(service => service.GetMemoryPriorityAsync(It.IsAny<ProcessModel>()))
                .ReturnsAsync(ProcessMemoryPriority.Low);
            var process = CreateProcess();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                memoryPriorityService: memoryPriorityService.Object);

            await viewModel.SetContextMemoryPriorityLowCommand.ExecuteAsync(process);

            memoryPriorityService.Verify(
                service => service.SetMemoryPriorityAsync(process, ProcessMemoryPriority.Low),
                Times.Once);
        }

        [Fact]
        public async Task ContextMemoryPriorityCommand_WhenServiceFails_ShowsSafeUserMessage()
        {
            var memoryPriorityService = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriorityService
                .Setup(service => service.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), ProcessMemoryPriority.Normal))
                .ReturnsAsync(ProcessOperationResult.Failed(
                    "AccessDenied",
                    ProcessOperationUserMessages.AccessDenied,
                    "Access is denied.",
                    isAccessDenied: true));
            var process = CreateProcess();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                memoryPriorityService: memoryPriorityService.Object);

            await viewModel.SetContextMemoryPriorityNormalCommand.ExecuteAsync(process);

            Assert.Equal(ProcessOperationUserMessages.AccessDenied, viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
        }

        [Fact]
        public async Task ContextMemoryPriorityCommand_WhenSuccessful_UpdatesSelectedProcessSummary()
        {
            var memoryPriorityService = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriorityService
                .Setup(service => service.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), ProcessMemoryPriority.BelowNormal))
                .ReturnsAsync(ProcessOperationResult.Succeeded("Memory priority applied.", "ok"));
            memoryPriorityService
                .Setup(service => service.GetMemoryPriorityAsync(It.IsAny<ProcessModel>()))
                .ReturnsAsync(ProcessMemoryPriority.BelowNormal);
            var process = CreateProcess();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                memoryPriorityService: memoryPriorityService.Object);

            await viewModel.SetContextMemoryPriorityBelowNormalCommand.ExecuteAsync(process);

            Assert.Equal(ProcessMemoryPriority.BelowNormal, viewModel.SelectedProcessSummary.MemoryPriority);
            Assert.Equal("Memory priority: BelowNormal", viewModel.SelectedProcessSummary.MemoryPriorityText);
        }

        [Fact]
        public async Task CopyContextProcessInfo_IncludesNamePidAndPath()
        {
            string? copiedText = null;
            var process = CreateProcess();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                clipboardSetter: text => copiedText = text);

            await viewModel.CopyContextProcessInfoCommand.ExecuteAsync(process);

            Assert.NotNull(copiedText);
            Assert.Contains("Name: Game.exe", copiedText);
            Assert.Contains("PID: 42", copiedText);
            Assert.Contains(@"Path: C:\Games\Game.exe", copiedText);
        }

        [Fact]
        public async Task CopyContextProcessInfo_WhenPathMissing_DoesNotThrow()
        {
            string? copiedText = null;
            var process = CreateProcess(path: string.Empty);
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                clipboardSetter: text => copiedText = text);

            var exception = await Record.ExceptionAsync(
                () => viewModel.CopyContextProcessInfoCommand.ExecuteAsync(process));

            Assert.Null(exception);
            Assert.Contains("Path: unavailable", copiedText);
        }

        [Fact]
        public async Task OpenContextExecutableLocation_WhenPathMissing_DoesNotThrow()
        {
            var viewModel = CreateViewModel(CreateProcessService().Object);

            var exception = await Record.ExceptionAsync(
                () => viewModel.OpenContextExecutableLocationCommand.ExecuteAsync(CreateProcess(path: string.Empty)));

            Assert.Null(exception);
            Assert.Equal("Executable path is unavailable for Game.exe.", viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
        }

        [Fact]
        public async Task ClearContextCpuSetsCommand_CallsSafeCpuSetClearPath()
        {
            var processService = CreateProcessService();
            processService
                .Setup(service => service.ClearProcessCpuSetAsync(It.IsAny<ProcessModel>()))
                .ReturnsAsync(true);
            var process = CreateProcess();
            var viewModel = CreateViewModel(processService.Object);

            await viewModel.ClearContextCpuSetsCommand.ExecuteAsync(process);

            processService.Verify(service => service.ClearProcessCpuSetAsync(process), Times.Once);
        }

        [Fact]
        public async Task RefreshContextProcessInfoCommand_RefreshesSelectedProcessInfo()
        {
            var processService = CreateProcessService();
            var process = CreateProcess();
            var viewModel = CreateViewModel(processService.Object);

            await viewModel.RefreshContextProcessInfoCommand.ExecuteAsync(process);

            processService.Verify(service => service.RefreshProcessInfo(process), Times.Once);
            Assert.Equal("Process info refreshed for Game.exe.", viewModel.StatusMessage);
        }

        [Fact]
        public async Task ContextMenuActions_DoNotCreatePersistentRules()
        {
            var processService = CreateProcessService();
            var memoryPriorityService = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriorityService
                .Setup(service => service.SetMemoryPriorityAsync(It.IsAny<ProcessModel>(), ProcessMemoryPriority.VeryLow))
                .ReturnsAsync(ProcessOperationResult.Succeeded("Memory priority applied.", "ok"));
            memoryPriorityService
                .Setup(service => service.GetMemoryPriorityAsync(It.IsAny<ProcessModel>()))
                .ReturnsAsync(ProcessMemoryPriority.VeryLow);
            var ruleStore = new Mock<IPersistentProcessRuleStore>(MockBehavior.Strict);
            ruleStore
                .Setup(store => store.LoadAsync())
                .ReturnsAsync(Array.Empty<PersistentProcessRule>());
            var viewModel = CreateViewModel(
                processService.Object,
                memoryPriorityService: memoryPriorityService.Object,
                persistentRuleStore: ruleStore.Object,
                clipboardSetter: _ => { });
            var process = CreateProcess();

            await viewModel.SetContextAboveNormalPriorityCommand.ExecuteAsync(process);
            await viewModel.SetContextMemoryPriorityVeryLowCommand.ExecuteAsync(process);
            await viewModel.CopyContextProcessInfoCommand.ExecuteAsync(process);

            ruleStore.Verify(store => store.SaveAsync(It.IsAny<IReadOnlyList<PersistentProcessRule>>()), Times.Never);
        }

        private static Mock<IProcessService> CreateProcessService()
        {
            var processService = new Mock<IProcessService>(MockBehavior.Loose);
            processService
                .Setup(service => service.GetProcessesAsync())
                .ReturnsAsync(new ObservableCollection<ProcessModel>());
            processService
                .Setup(service => service.GetActiveApplicationsAsync())
                .ReturnsAsync(new ObservableCollection<ProcessModel>());
            processService
                .Setup(service => service.IsProcessStillRunning(It.IsAny<ProcessModel>()))
                .ReturnsAsync(true);
            processService
                .Setup(service => service.RefreshProcessInfo(It.IsAny<ProcessModel>()))
                .Returns(Task.CompletedTask);
            return processService;
        }

        private static Mock<IProcessAffinityApplyCoordinator> CreateAffinityCoordinator()
        {
            var coordinator = new Mock<IProcessAffinityApplyCoordinator>(MockBehavior.Strict);
            coordinator
                .Setup(service => service.ApplyCoreSelectionAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<string>(),
                    default))
                .ReturnsAsync(AffinityApplyResult.Succeeded(1, 1));
            return coordinator;
        }

        private static ProcessViewModel CreateViewModel(
            IProcessService processService,
            IProcessAffinityApplyCoordinator? processAffinityApplyCoordinator = null,
            IProcessMemoryPriorityService? memoryPriorityService = null,
            IPersistentProcessRuleStore? persistentRuleStore = null,
            Action<string>? clipboardSetter = null,
            Action<string>? executableLocationOpener = null)
        {
            var virtualizedProcessService = new Mock<IVirtualizedProcessService>(MockBehavior.Loose);
            virtualizedProcessService.SetupProperty(
                service => service.Configuration,
                new VirtualizedProcessConfig());

            var cpuTopologyService = new Mock<ICpuTopologyService>(MockBehavior.Loose);
            var powerPlanService = new Mock<IPowerPlanService>(MockBehavior.Loose);
            var notificationService = new Mock<INotificationService>(MockBehavior.Loose);
            var systemTrayService = new Mock<ISystemTrayService>(MockBehavior.Loose);
            var coreMaskService = new Mock<ICoreMaskService>(MockBehavior.Loose);
            var associationService = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);
            var gameModeService = new Mock<IGameModeService>(MockBehavior.Loose);

            return new ProcessViewModel(
                NullLogger<ProcessViewModel>.Instance,
                processService,
                new ProcessFilterService(),
                virtualizedProcessService.Object,
                cpuTopologyService.Object,
                powerPlanService.Object,
                notificationService.Object,
                systemTrayService.Object,
                coreMaskService.Object,
                associationService.Object,
                gameModeService.Object,
                processAffinityApplyCoordinator: processAffinityApplyCoordinator,
                memoryPriorityService: memoryPriorityService,
                persistentRuleStore: persistentRuleStore,
                persistentRuleMatcher: new PersistentProcessRuleMatcher(),
                clipboardSetter: clipboardSetter,
                executableLocationOpener: executableLocationOpener);
        }

        private static ProcessModel CreateProcess(
            string name = "Game.exe",
            int processId = 42,
            string path = @"C:\Games\Game.exe",
            ProcessPriorityClass priority = ProcessPriorityClass.Normal)
            => new()
            {
                ProcessId = processId,
                Name = name,
                ExecutablePath = path,
                CpuUsage = 1.5,
                MemoryUsage = 128 * 1024 * 1024,
                Priority = priority,
                ProcessorAffinity = 0xF,
                Classification = ProcessClassification.ForegroundApp,
            };
    }
}
