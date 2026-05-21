namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using System.Reflection;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class SelectedProcessSummaryViewModelTests
    {
        [Fact]
        public async Task UpdateAsync_WithNoSelectedProcess_ClearsSummary()
        {
            var viewModel = new SelectedProcessSummaryViewModel();

            await viewModel.UpdateAsync(null);

            Assert.False(viewModel.HasSelection);
            Assert.Equal("No process selected", viewModel.CurrentProcessStatusText);
            Assert.Equal("Memory priority unavailable", viewModel.MemoryPriorityText);
            Assert.Equal("No saved rule", viewModel.RuleStatusText);
        }

        [Fact]
        public async Task UpdateAsync_WithSelectedProcess_PopulatesCheapProcessFields()
        {
            var viewModel = new SelectedProcessSummaryViewModel();

            await viewModel.UpdateAsync(CreateProcess("Game.exe", 1234, ProcessPriorityClass.High, 0x3, 512 * 1024 * 1024));

            Assert.True(viewModel.HasSelection);
            Assert.Equal(1234, viewModel.ProcessId);
            Assert.Equal("Game.exe", viewModel.ProcessName);
            Assert.Equal(@"C:\Games\Game.exe", viewModel.ExecutablePath);
            Assert.Equal("Selected process: Game.exe (PID 1234)", viewModel.ProcessTitle);
            Assert.Equal("CPU priority: High", viewModel.CpuPriorityText);
            Assert.Equal("Memory: 512 MB", viewModel.MemoryUsageText);
            Assert.Equal("Affinity: legacy mask 0x3", viewModel.AffinityText);
        }

        [Fact]
        public async Task UpdateAsync_WhenSelectionChanges_ReplacesSummary()
        {
            var viewModel = new SelectedProcessSummaryViewModel();

            await viewModel.UpdateAsync(CreateProcess("First.exe", 1, ProcessPriorityClass.Normal, 0x1, 1));
            await viewModel.UpdateAsync(CreateProcess("Second.exe", 2, ProcessPriorityClass.BelowNormal, 0x2, 2));

            Assert.Equal(2, viewModel.ProcessId);
            Assert.Equal("Second.exe", viewModel.ProcessName);
            Assert.Equal("CPU priority: BelowNormal", viewModel.CpuPriorityText);
            Assert.Equal("Affinity: legacy mask 0x2", viewModel.AffinityText);
        }

        [Fact]
        public async Task UpdateAsync_WhenMemoryPriorityReadSucceeds_PopulatesMemoryPriority()
        {
            var memoryPriority = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriority
                .Setup(service => service.GetMemoryPriorityAsync(It.IsAny<ProcessModel>()))
                .ReturnsAsync(ProcessMemoryPriority.BelowNormal);
            var viewModel = new SelectedProcessSummaryViewModel(memoryPriority.Object);

            await viewModel.UpdateAsync(CreateProcess());

            Assert.Equal(ProcessMemoryPriority.BelowNormal, viewModel.MemoryPriority);
            Assert.Equal("Memory priority: BelowNormal", viewModel.MemoryPriorityText);
        }

        [Fact]
        public async Task UpdateAsync_WhenMemoryPriorityUnavailable_ShowsUnavailableWithoutThrowing()
        {
            var memoryPriority = new Mock<IProcessMemoryPriorityService>(MockBehavior.Strict);
            memoryPriority
                .Setup(service => service.GetMemoryPriorityAsync(It.IsAny<ProcessModel>()))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied"));
            var viewModel = new SelectedProcessSummaryViewModel(memoryPriority.Object);

            await viewModel.UpdateAsync(CreateProcess());

            Assert.Null(viewModel.MemoryPriority);
            Assert.Equal("Memory priority unavailable", viewModel.MemoryPriorityText);
        }

        [Fact]
        public void SelectedProcessSummary_HasNoPerformanceMonitoringDependency()
        {
            var type = typeof(SelectedProcessSummaryViewModel);

            var constructorParameters = type
                .GetConstructors()
                .SelectMany(ctor => ctor.GetParameters())
                .Select(parameter => parameter.ParameterType);
            var fieldTypes = type
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(field => field.FieldType);

            Assert.DoesNotContain(typeof(IPerformanceMonitoringService), constructorParameters);
            Assert.DoesNotContain(typeof(IPerformanceMonitoringService), fieldTypes);
        }

        [Fact]
        public void SelectedProcessSummary_DoesNotOwnTimers()
        {
            var fieldTypes = typeof(SelectedProcessSummaryViewModel)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(field => field.FieldType);

            Assert.DoesNotContain(typeof(System.Timers.Timer), fieldTypes);
            Assert.DoesNotContain(typeof(System.Threading.Timer), fieldTypes);
        }

        [Fact]
        public async Task UpdateAsync_WhenPersistentRuleMatches_ShowsSavedRule()
        {
            var store = new Mock<IPersistentProcessRuleStore>(MockBehavior.Strict);
            store
                .Setup(ruleStore => ruleStore.LoadAsync())
                .ReturnsAsync(new[]
                {
                    new PersistentProcessRule
                    {
                        Name = "Game rule",
                        ProcessName = "Game.exe",
                        IsEnabled = true,
                    },
                });
            var viewModel = new SelectedProcessSummaryViewModel(
                persistentRuleStore: store.Object,
                persistentRuleMatcher: new PersistentProcessRuleMatcher());

            await viewModel.UpdateAsync(CreateProcess("Game.exe"));

            Assert.True(viewModel.HasThreadPilotRule);
            Assert.Equal("Saved rule exists: Game rule", viewModel.RuleStatusText);
        }

        [Fact]
        public async Task UpdateAsync_WhenNoPersistentRuleMatches_ShowsNoSavedRule()
        {
            var store = new Mock<IPersistentProcessRuleStore>(MockBehavior.Strict);
            store
                .Setup(ruleStore => ruleStore.LoadAsync())
                .ReturnsAsync(new[]
                {
                    new PersistentProcessRule
                    {
                        Name = "Other rule",
                        ProcessName = "Other.exe",
                        IsEnabled = true,
                    },
                });
            var viewModel = new SelectedProcessSummaryViewModel(
                persistentRuleStore: store.Object,
                persistentRuleMatcher: new PersistentProcessRuleMatcher());

            await viewModel.UpdateAsync(CreateProcess("Game.exe"));

            Assert.False(viewModel.HasThreadPilotRule);
            Assert.Equal("No saved rule", viewModel.RuleStatusText);
        }

        private static ProcessModel CreateProcess(
            string name = "Game.exe",
            int processId = 42,
            ProcessPriorityClass priority = ProcessPriorityClass.Normal,
            long affinity = 0xF,
            long memoryUsage = 64 * 1024 * 1024)
            => new()
            {
                ProcessId = processId,
                Name = name,
                ExecutablePath = @"C:\Games\Game.exe",
                CpuUsage = 12.5,
                MemoryUsage = memoryUsage,
                Priority = priority,
                ProcessorAffinity = affinity,
                Classification = ProcessClassification.ForegroundApp,
            };
    }
}
