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
            var enhancedLoggingService = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(
                processService.Object,
                enhancedLoggingService: enhancedLoggingService.Object,
                activityAuditService: audit);
            var process = CreateProcess(priority: ProcessPriorityClass.Normal);

            await viewModel.SetContextHighPriorityCommand.ExecuteAsync(process);

            processService.Verify(
                service => service.SetProcessPriority(process, ProcessPriorityClass.High),
                Times.Once);
            enhancedLoggingService.Verify(
                service => service.LogUserActionAsync(
                    "ProcessPriorityChanged",
                    It.Is<string>(details => details.Contains("Game.exe") && details.Contains("High")),
                    It.Is<string>(context => context.Contains("PID: 42"))),
                Times.Once);
            Assert.Equal(ProcessOperationUserMessages.HighPriorityWarning, viewModel.StatusMessage);
            Assert.False(viewModel.HasError);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Priority", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
            Assert.Contains("High", entry.Message);
        }

        [Fact]
        public async Task ApplyContextAffinityCommand_UsesProvidedRowProcess()
        {
            var processService = CreateProcessService();
            var coordinator = CreateAffinityCoordinator();
            var enhancedLoggingService = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(
                processService.Object,
                processAffinityApplyCoordinator: coordinator.Object,
                enhancedLoggingService: enhancedLoggingService.Object,
                activityAuditService: audit);
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
            enhancedLoggingService.Verify(
                service => service.LogUserActionAsync(
                    "ProcessAffinityApplied",
                    It.IsAny<string>(),
                    It.Is<string>(context => context.Contains("Process: Game.exe") && context.Contains("PID: 100"))),
                Times.Once);
            Assert.Same(rowProcess, viewModel.SelectedProcess);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Affinity", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
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
        public async Task SetPriorityCommand_WhenRealtimeRequested_LogsVisibleBlockedEntry()
        {
            var processService = CreateProcessService();
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);
            viewModel.SelectedProcess = CreateProcess();

            await viewModel.SetPriorityCommand.ExecuteAsync(ProcessPriorityClass.RealTime);

            processService.Verify(
                service => service.SetProcessPriority(It.IsAny<ProcessModel>(), ProcessPriorityClass.RealTime),
                Times.Never);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Priority", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Warning, entry.Severity);
            Assert.Equal(ProcessOperationUserMessages.RealtimePriorityBlocked, entry.Message);
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
            var enhancedLoggingService = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var process = CreateProcess();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                memoryPriorityService: memoryPriorityService.Object,
                enhancedLoggingService: enhancedLoggingService.Object,
                activityAuditService: audit);

            await viewModel.SetContextMemoryPriorityLowCommand.ExecuteAsync(process);

            memoryPriorityService.Verify(
                service => service.SetMemoryPriorityAsync(process, ProcessMemoryPriority.Low),
                Times.Once);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Memory Priority", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
            enhancedLoggingService.Verify(
                service => service.LogUserActionAsync(
                    "ProcessMemoryPriorityChanged",
                    It.Is<string>(details => details.Contains("Game.exe") && details.Contains("Low")),
                    It.Is<string>(context => context.Contains("PID: 42"))),
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
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                memoryPriorityService: memoryPriorityService.Object,
                activityAuditService: audit);

            await viewModel.SetContextMemoryPriorityNormalCommand.ExecuteAsync(process);

            Assert.Equal(ProcessOperationUserMessages.AccessDenied, viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Memory Priority", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Warning, entry.Severity);
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
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);

            await viewModel.ClearContextCpuSetsCommand.ExecuteAsync(process);

            processService.Verify(service => service.ClearProcessCpuSetAsync(process), Times.Once);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Affinity", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
        }

        [Fact]
        public async Task RefreshContextProcessInfoCommand_RefreshesSelectedProcessInfo()
        {
            var processService = CreateProcessService();
            var process = CreateProcess();
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);

            await viewModel.RefreshContextProcessInfoCommand.ExecuteAsync(process);

            processService.Verify(service => service.RefreshProcessInfo(process), Times.Once);
            Assert.Equal("Process info refreshed for Game.exe.", viewModel.StatusMessage);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Process", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
        }

        [Fact]
        public async Task RefreshProcessesCommand_DoesNotCreateActivityAuditEntry()
        {
            var processService = CreateProcessService();
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);

            await viewModel.RefreshProcessesCommand.ExecuteAsync(null);

            Assert.Empty(await audit.GetEntriesAsync());
        }

        [Fact]
        public async Task LockProcessList_WhenEnabled_SkipsRefreshAndKeepsSelection()
        {
            var processService = CreateProcessService();
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);
            var selected = CreateProcess(processId: 42);
            viewModel.Processes = new ObservableCollection<ProcessModel> { selected };
            viewModel.FilteredProcesses = new ObservableCollection<ProcessModel> { selected };
            viewModel.SelectedProcess = selected;

            viewModel.IsProcessListLocked = true;
            await viewModel.RefreshProcessesCommand.ExecuteAsync(null);

            processService.Verify(service => service.GetProcessesAsync(), Times.Never);
            Assert.Same(selected, viewModel.SelectedProcess);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Process", entry.Category);
            Assert.Equal("Lock process list enabled.", entry.Message);
        }

        [Fact]
        public async Task LockProcessList_WhenDisabled_RefreshesOnceWithoutPersistentRuleSettingChange()
        {
            var processService = CreateProcessService();
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(processService.Object, activityAuditService: audit);

            viewModel.IsProcessListLocked = true;
            viewModel.IsProcessListLocked = false;

            processService.Verify(service => service.GetProcessesAsync(), Times.Once);
            var entries = await audit.GetEntriesAsync();
            Assert.Contains(entries, entry => entry.Message == "Lock process list enabled.");
            Assert.Contains(entries, entry => entry.Message == "Lock process list disabled.");
            Assert.DoesNotContain(entries, entry => entry.Message.Contains("refreshed", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(entries, entry => entry.Message.Contains("Apply saved rules", StringComparison.OrdinalIgnoreCase));
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

        [Fact]
        public async Task SaveCurrentSettingsAsRuleCommand_CreatesRuleForSelectedProcess()
        {
            var ruleStore = new CapturingRuleStore();
            var enhancedLoggingService = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore),
                enhancedLoggingService: enhancedLoggingService.Object);
            var process = CreateProcess();
            viewModel.SelectedProcess = process;
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
                new CpuCoreModel { LogicalCoreId = 1, IsSelected = true },
            ];

            await viewModel.SaveCurrentSettingsAsRuleCommand.ExecuteAsync(null);

            var rule = Assert.Single(ruleStore.SavedRules);
            Assert.Equal(process.Name, rule.ProcessName);
            Assert.Equal(process.ExecutablePath, rule.ExecutablePath);
            Assert.Equal("Saved rule for Game.exe.", viewModel.StatusMessage);
            enhancedLoggingService.Verify(
                service => service.LogUserActionAsync(
                    "PersistentRuleSaved",
                    "Saved rule for Game.exe.",
                    It.Is<string>(context => context.Contains("Process: Game.exe") && context.Contains("PID: 42"))),
                Times.Once);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleCommand_UpdatesExistingMatchingRule()
        {
            var existing = new PersistentProcessRule
            {
                Id = "rule-1",
                Name = "Old",
                IsEnabled = true,
                ProcessName = "Game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };
            var ruleStore = new CapturingRuleStore([existing]);
            var enhancedLoggingService = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore),
                enhancedLoggingService: enhancedLoggingService.Object);

            await viewModel.SaveCurrentSettingsAsRuleCommand.ExecuteAsync(CreateProcess(priority: ProcessPriorityClass.High));

            var rule = Assert.Single(ruleStore.SavedRules);
            Assert.Equal("rule-1", rule.Id);
            Assert.Equal(ProcessPriorityClass.High, rule.Priority);
            Assert.Equal("Updated saved rule for Game.exe.", viewModel.StatusMessage);
            enhancedLoggingService.Verify(
                service => service.LogUserActionAsync(
                    "PersistentRuleSaved",
                    "Updated saved rule for Game.exe.",
                    It.Is<string>(context => context.Contains("Process: Game.exe") && context.Contains("PID: 42"))),
                Times.Once);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleCommand_WithNormalPriorityAndNoAffinityOrMemoryPriority_ShowsNoActionMessage()
        {
            var ruleStore = new CapturingRuleStore();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore));
            var process = CreateProcess(priority: ProcessPriorityClass.Normal, affinity: 0);

            await viewModel.SaveCurrentSettingsAsRuleCommand.ExecuteAsync(process);

            Assert.Empty(ruleStore.SavedRules);
            Assert.Equal("There are no current settings to save as a rule.", viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleCommand_WithAffinityAndNormalPriority_DoesNotSaveApplyPriorityOnStart()
        {
            var ruleStore = new CapturingRuleStore();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore));
            var process = CreateProcess(priority: ProcessPriorityClass.Normal, affinity: 0x5);

            await viewModel.SaveCurrentSettingsAsRuleCommand.ExecuteAsync(process);

            var rule = Assert.Single(ruleStore.SavedRules);
            Assert.Equal(0x5, rule.LegacyAffinityMask);
            Assert.Null(rule.Priority);
            Assert.False(rule.ApplyPriorityOnStart);
        }

        [Fact]
        public async Task ApplyAffinityAndSaveAsRuleCommand_AppliesAffinityBeforeSavingRule()
        {
            var ruleStore = new CapturingRuleStore();
            var coordinator = CreateAffinityCoordinator();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                processAffinityApplyCoordinator: coordinator.Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore));
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
                new CpuCoreModel { LogicalCoreId = 1, IsSelected = false },
            ];
            var process = CreateProcess();

            await viewModel.ApplyAffinityAndSaveAsRuleCommand.ExecuteAsync(process);

            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    process,
                    It.Is<IReadOnlyList<bool>>(mask => mask.Count == 2 && mask[0] && !mask[1]),
                    "Manual Process tab context menu CPU selection",
                    default),
                Times.Once);
            var rule = Assert.Single(ruleStore.SavedRules);
            Assert.Equal(1, rule.LegacyAffinityMask);
            Assert.True(rule.ApplyAffinityOnStart);
        }

        [Fact]
        public async Task ApplyAffinityAndSaveAsRuleCommand_WhenAffinityApplyFails_DoesNotSaveRule()
        {
            var ruleStore = new CapturingRuleStore();
            var coordinator = new Mock<IProcessAffinityApplyCoordinator>(MockBehavior.Strict);
            coordinator
                .Setup(service => service.ApplyCoreSelectionAsync(
                    It.IsAny<ProcessModel>(),
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<string>(),
                    default))
                .ReturnsAsync(AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.AccessDenied,
                    ProcessOperationUserMessages.AccessDenied,
                    "Access denied.",
                    isAccessDenied: true));
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                processAffinityApplyCoordinator: coordinator.Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore),
                activityAuditService: audit);
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
            ];

            await viewModel.ApplyAffinityAndSaveAsRuleCommand.ExecuteAsync(CreateProcess());

            Assert.Empty(ruleStore.SavedRules);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
            var entry = Assert.Single(await audit.GetEntriesAsync());
            Assert.Equal("Affinity", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Warning, entry.Severity);
        }

        [Fact]
        public async Task ApplyAffinityAndSaveAsRuleCommand_UsesRowProcessInsteadOfStaleSelectedProcess()
        {
            var ruleStore = new CapturingRuleStore();
            var coordinator = CreateAffinityCoordinator();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                processAffinityApplyCoordinator: coordinator.Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore));
            viewModel.CpuCores =
            [
                new CpuCoreModel { LogicalCoreId = 0, IsSelected = true },
            ];
            var staleSelected = CreateProcess(name: "Old.exe", path: @"C:\Old\Old.exe");
            var rowProcess = CreateProcess(name: "Row.exe", path: @"C:\Row\Row.exe");
            viewModel.SelectedProcess = staleSelected;

            await viewModel.ApplyAffinityAndSaveAsRuleCommand.ExecuteAsync(rowProcess);

            coordinator.Verify(
                service => service.ApplyCoreSelectionAsync(
                    rowProcess,
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<string>(),
                    default),
                Times.Once);
            var rule = Assert.Single(ruleStore.SavedRules);
            Assert.Equal("Row.exe", rule.ProcessName);
            Assert.Equal(@"C:\Row\Row.exe", rule.ExecutablePath);
        }

        [Fact]
        public async Task SaveCurrentSettingsAsRuleCommand_UpdatesSelectedProcessSummary()
        {
            var ruleStore = new CapturingRuleStore();
            var viewModel = CreateViewModel(
                CreateProcessService().Object,
                persistentRuleStore: ruleStore,
                processRuleCreationService: CreateRuleCreationService(ruleStore));
            var process = CreateProcess();

            await viewModel.SaveCurrentSettingsAsRuleCommand.ExecuteAsync(process);

            Assert.True(viewModel.SelectedProcessSummary.HasThreadPilotRule);
            Assert.Equal("Saved rule exists: Game.exe rule", viewModel.SelectedProcessSummary.RuleStatusText);
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
            IProcessRuleCreationService? processRuleCreationService = null,
            Action<string>? clipboardSetter = null,
            Action<string>? executableLocationOpener = null,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IActivityAuditService? activityAuditService = null)
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
                enhancedLoggingService: enhancedLoggingService,
                activityAuditService: activityAuditService,
                memoryPriorityService: memoryPriorityService,
                persistentRuleStore: persistentRuleStore,
                persistentRuleMatcher: new PersistentProcessRuleMatcher(),
                processRuleCreationService: processRuleCreationService,
                clipboardSetter: clipboardSetter,
                executableLocationOpener: executableLocationOpener);
        }

        private static ProcessModel CreateProcess(
            string name = "Game.exe",
            int processId = 42,
            string path = @"C:\Games\Game.exe",
            ProcessPriorityClass priority = ProcessPriorityClass.Normal,
            long affinity = 0xF)
            => new()
            {
                ProcessId = processId,
                Name = name,
                ExecutablePath = path,
                CpuUsage = 1.5,
                MemoryUsage = 128 * 1024 * 1024,
                Priority = priority,
                ProcessorAffinity = affinity,
                Classification = ProcessClassification.ForegroundApp,
            };

        private static ProcessRuleCreationService CreateRuleCreationService(IPersistentProcessRuleStore ruleStore) =>
            new(
                ruleStore,
                topologyProvider: null,
                new CpuSelectionMigrationService(),
                NullLogger<ProcessRuleCreationService>.Instance);

        private sealed class CapturingRuleStore(IReadOnlyList<PersistentProcessRule>? initialRules = null)
            : IPersistentProcessRuleStore
        {
            public IReadOnlyList<PersistentProcessRule> SavedRules { get; private set; } = initialRules ?? [];

            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync() =>
                Task.FromResult(this.SavedRules);

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
            {
                this.SavedRules = rules.ToList();
                return Task.CompletedTask;
            }
        }
    }
}
