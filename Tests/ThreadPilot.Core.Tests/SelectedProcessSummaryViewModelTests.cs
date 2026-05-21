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
        public async Task UpdateAsync_WhenSelectionChangesBeforeSlowMemoryPriorityCompletes_KeepsLatestSelection()
        {
            var memoryPriority = new ControlledMemoryPriorityService();
            var viewModel = new SelectedProcessSummaryViewModel(memoryPriority);
            var oldProcess = CreateProcess("Old.exe", 100, ProcessPriorityClass.Normal, 0x1, 10);
            var latestProcess = CreateProcess("Latest.exe", 200, ProcessPriorityClass.High, 0x2, 20);

            var oldUpdate = viewModel.UpdateAsync(oldProcess);
            await memoryPriority.WaitForReadAsync(oldProcess.ProcessId);

            memoryPriority.SetImmediatePriority(latestProcess.ProcessId, ProcessMemoryPriority.Normal);
            await viewModel.UpdateAsync(latestProcess);

            memoryPriority.CompleteRead(oldProcess.ProcessId, ProcessMemoryPriority.VeryLow);
            await oldUpdate;

            Assert.Equal(latestProcess.ProcessId, viewModel.ProcessId);
            Assert.Equal(latestProcess.Name, viewModel.ProcessName);
            Assert.Equal(ProcessMemoryPriority.Normal, viewModel.MemoryPriority);
            Assert.Equal("Memory priority: Normal", viewModel.MemoryPriorityText);
        }

        [Fact]
        public async Task UpdateAsync_WhenSlowRuleLookupCompletesAfterSelectionChange_KeepsLatestRuleStatus()
        {
            var store = new ControlledPersistentProcessRuleStore();
            var viewModel = new SelectedProcessSummaryViewModel(
                persistentRuleStore: store,
                persistentRuleMatcher: new PersistentProcessRuleMatcher());
            var oldProcess = CreateProcess("Old.exe", 100);
            var latestProcess = CreateProcess("Latest.exe", 200);

            var oldUpdate = viewModel.UpdateAsync(oldProcess);
            await store.WaitForLoadAsync(1);

            store.EnqueueImmediateRules(new[]
            {
                new PersistentProcessRule
                {
                    Name = "Latest rule",
                    ProcessName = latestProcess.Name,
                    IsEnabled = true,
                },
            });
            await viewModel.UpdateAsync(latestProcess);

            store.CompleteLoad(
                1,
                new[]
                {
                    new PersistentProcessRule
                    {
                        Name = "Old rule",
                        ProcessName = oldProcess.Name,
                        IsEnabled = true,
                    },
                });
            await oldUpdate;

            Assert.Equal(latestProcess.ProcessId, viewModel.ProcessId);
            Assert.Equal(latestProcess.Name, viewModel.ProcessName);
            Assert.True(viewModel.HasThreadPilotRule);
            Assert.Equal("Saved rule exists: Latest rule", viewModel.RuleStatusText);
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

        private sealed class ControlledMemoryPriorityService : IProcessMemoryPriorityService
        {
            private readonly Dictionary<int, TaskCompletionSource<ProcessMemoryPriority?>> pendingReads = new();
            private readonly Dictionary<int, TaskCompletionSource> readSignals = new();
            private readonly Dictionary<int, ProcessMemoryPriority?> immediatePriorities = new();

            public Task<ProcessMemoryPriority?> GetMemoryPriorityAsync(ProcessModel process)
            {
                if (this.immediatePriorities.TryGetValue(process.ProcessId, out var priority))
                {
                    return Task.FromResult(priority);
                }

                var pending = new TaskCompletionSource<ProcessMemoryPriority?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var signal = this.GetOrCreateReadSignal(process.ProcessId);
                this.pendingReads[process.ProcessId] = pending;
                signal.TrySetResult();
                return pending.Task;
            }

            public Task<ProcessOperationResult> SetMemoryPriorityAsync(ProcessModel process, ProcessMemoryPriority priority)
                => throw new NotSupportedException();

            public void SetImmediatePriority(int processId, ProcessMemoryPriority? priority)
            {
                this.immediatePriorities[processId] = priority;
            }

            public Task WaitForReadAsync(int processId) => this.GetOrCreateReadSignal(processId).Task;

            public void CompleteRead(int processId, ProcessMemoryPriority? priority)
            {
                this.pendingReads[processId].SetResult(priority);
            }

            private TaskCompletionSource GetOrCreateReadSignal(int processId)
            {
                if (!this.readSignals.TryGetValue(processId, out var signal))
                {
                    signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    this.readSignals[processId] = signal;
                }

                return signal;
            }
        }

        private sealed class ControlledPersistentProcessRuleStore : IPersistentProcessRuleStore
        {
            private readonly Dictionary<int, TaskCompletionSource<IReadOnlyList<PersistentProcessRule>>> pendingLoads = new();
            private readonly Dictionary<int, TaskCompletionSource> loadSignals = new();
            private readonly Queue<IReadOnlyList<PersistentProcessRule>> immediateRules = new();
            private int loadCount;

            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync()
            {
                this.loadCount++;
                var loadNumber = this.loadCount;

                if (this.immediateRules.Count > 0)
                {
                    this.GetOrCreateLoadSignal(loadNumber).TrySetResult();
                    return Task.FromResult(this.immediateRules.Dequeue());
                }

                var pending = new TaskCompletionSource<IReadOnlyList<PersistentProcessRule>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                this.pendingLoads[loadNumber] = pending;
                this.GetOrCreateLoadSignal(loadNumber).TrySetResult();
                return pending.Task;
            }

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules)
                => throw new NotSupportedException();

            public void EnqueueImmediateRules(IReadOnlyList<PersistentProcessRule> rules)
            {
                this.immediateRules.Enqueue(rules);
            }

            public Task WaitForLoadAsync(int loadNumber) => this.GetOrCreateLoadSignal(loadNumber).Task;

            public void CompleteLoad(int loadNumber, IReadOnlyList<PersistentProcessRule> rules)
            {
                this.pendingLoads[loadNumber].SetResult(rules);
            }

            private TaskCompletionSource GetOrCreateLoadSignal(int loadNumber)
            {
                if (!this.loadSignals.TryGetValue(loadNumber, out var signal))
                {
                    signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    this.loadSignals[loadNumber] = signal;
                }

                return signal;
            }
        }
    }
}
