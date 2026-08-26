namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class ProcessPowerPlanAssociationViewModelTests
    {
        [Fact]
        public void Constructor_IgnoresConfigurationReloadUntilViewIsInitialized()
        {
            var associations = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);
            var powerPlans = new Mock<IPowerPlanService>(MockBehavior.Strict);
            var processes = new Mock<IProcessService>(MockBehavior.Strict);
            var monitor = new Mock<IProcessMonitorManagerService>(MockBehavior.Loose);
            var masks = new Mock<ICoreMaskService>(MockBehavior.Strict);
            _ = new ProcessPowerPlanAssociationViewModel(
                NullLogger<ProcessPowerPlanAssociationViewModel>.Instance,
                associations.Object,
                powerPlans.Object,
                processes.Object,
                monitor.Object,
                masks.Object);

            associations.Raise(
                service => service.ConfigurationChanged += null,
                new ConfigurationChangedEventArgs("Loaded"));

            powerPlans.Verify(service => service.GetPowerPlansAsync(), Times.Never);
            processes.Verify(service => service.GetProcessesAsync(), Times.Never);
            masks.Verify(service => service.InitializeAsync(), Times.Never);
        }

        [Fact]
        public async Task RefreshSavedProcessRulesAsync_ListsWhatEachRuleActuallyApplies()
        {
            var store = new FakeRuleStore(
            [
                new PersistentProcessRule
                {
                    Id = "rule-1",
                    ProcessName = "cs2",
                    IsEnabled = true,
                    ApplyAffinityOnStart = true,
                    CpuAssignmentMode = CpuAssignmentMode.AffinityMask,
                    ApplyMemoryPriorityOnStart = true,
                    MemoryPriority = ProcessMemoryPriority.Low,
                },
            ]);
            var viewModel = CreateViewModel(store, new Mock<IProcessRuleCreationService>().Object);

            await viewModel.RefreshSavedProcessRulesAsync();

            var row = Assert.Single(viewModel.SavedProcessRules);
            Assert.Equal("cs2", row.ProcessName);
            Assert.Contains("CPU assignment", row.Applies);
            Assert.Contains("memory Low", row.Applies);
            Assert.Equal("AffinityMask", row.Mode);
            Assert.True(viewModel.HasSavedProcessRules);
        }

        [Fact]
        public async Task DeleteSavedProcessRuleCommand_RemovesTheRuleAndReloadsTheList()
        {
            var store = new FakeRuleStore(
            [
                new PersistentProcessRule { Id = "rule-1", ProcessName = "cs2", IsEnabled = true },
            ]);
            var ruleService = new Mock<IProcessRuleCreationService>();
            ruleService
                .Setup(service => service.DeleteRuleByIdAsync("rule-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    store.Rules = [];
                    return new ProcessRuleCreationResult { Success = true, UserMessage = "Deleted saved rule for cs2." };
                });
            var viewModel = CreateViewModel(store, ruleService.Object);
            await viewModel.RefreshSavedProcessRulesAsync();

            await viewModel.DeleteSavedProcessRuleCommand.ExecuteAsync(viewModel.SavedProcessRules[0]);

            ruleService.Verify(service => service.DeleteRuleByIdAsync("rule-1", It.IsAny<CancellationToken>()), Times.Once);
            Assert.Empty(viewModel.SavedProcessRules);
            Assert.False(viewModel.HasSavedProcessRules);
        }

        private static ProcessPowerPlanAssociationViewModel CreateViewModel(
            IPersistentProcessRuleStore store,
            IProcessRuleCreationService ruleService) =>
            new(
                NullLogger<ProcessPowerPlanAssociationViewModel>.Instance,
                new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose).Object,
                new Mock<IPowerPlanService>(MockBehavior.Loose).Object,
                new Mock<IProcessService>(MockBehavior.Loose).Object,
                new Mock<IProcessMonitorManagerService>(MockBehavior.Loose).Object,
                new Mock<ICoreMaskService>(MockBehavior.Loose).Object,
                persistentRuleStore: store,
                ruleCreationService: ruleService);

        private sealed class FakeRuleStore(IReadOnlyList<PersistentProcessRule> rules) : IPersistentProcessRuleStore
        {
            public IReadOnlyList<PersistentProcessRule> Rules { get; set; } = rules;

            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync() => Task.FromResult(this.Rules);

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
            {
                this.Rules = rules;
                return Task.CompletedTask;
            }
        }
    }
}
