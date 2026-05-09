namespace ThreadPilot.Core.Tests
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessListDeltaUpdaterTests
    {
        [Fact]
        public void ApplyDelta_PreservesExistingInstancesAndUpdatesProperties()
        {
            var existing = new ProcessModel
            {
                ProcessId = 42,
                Name = "ThreadPilot",
                CpuUsage = 1,
                MemoryUsage = 100,
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 1,
            };
            var processes = new ObservableCollection<ProcessModel> { existing };
            var snapshot = new[]
            {
                new ProcessModel
                {
                    ProcessId = 42,
                    Name = "ThreadPilot",
                    CpuUsage = 7,
                    MemoryUsage = 500,
                    Priority = ProcessPriorityClass.High,
                    ProcessorAffinity = 3,
                    HasVisibleWindow = true,
                    IsForeground = true,
                    Classification = ProcessClassification.ForegroundApp,
                    MainWindowTitle = "ThreadPilot - Processes",
                },
            };

            var result = ProcessListDeltaUpdater.ApplyDelta(processes, snapshot, 42);

            Assert.Same(existing, processes[0]);
            Assert.Same(existing, result.SelectedProcess);
            Assert.False(result.SelectedProcessTerminated);
            Assert.Equal(7, existing.CpuUsage);
            Assert.Equal(500, existing.MemoryUsage);
            Assert.Equal(ProcessPriorityClass.High, existing.Priority);
            Assert.Equal(3, existing.ProcessorAffinity);
            Assert.True(existing.HasVisibleWindow);
            Assert.True(existing.IsForeground);
            Assert.Equal(ProcessClassification.ForegroundApp, existing.Classification);
            Assert.Equal("ThreadPilot - Processes", existing.MainWindowTitle);
        }

        [Fact]
        public void ApplyDelta_AddsNewProcessesAndRemovesDeadProcesses()
        {
            var removed = new ProcessModel { ProcessId = 10, Name = "Dead" };
            var kept = new ProcessModel { ProcessId = 20, Name = "Kept" };
            var processes = new ObservableCollection<ProcessModel> { removed, kept };
            var snapshot = new[]
            {
                new ProcessModel { ProcessId = 20, Name = "Kept" },
                new ProcessModel { ProcessId = 30, Name = "New" },
            };

            var result = ProcessListDeltaUpdater.ApplyDelta(processes, snapshot, 20);

            Assert.Equal(2, processes.Count);
            Assert.DoesNotContain(processes, p => p.ProcessId == 10);
            Assert.Contains(processes, p => p.ProcessId == 30);
            Assert.Same(kept, result.SelectedProcess);
            Assert.False(result.SelectedProcessTerminated);
        }

        [Fact]
        public void ApplyDelta_ReportsTerminatedSelection()
        {
            var selected = new ProcessModel { ProcessId = 10, Name = "Dead" };
            var processes = new ObservableCollection<ProcessModel> { selected };

            var result = ProcessListDeltaUpdater.ApplyDelta(processes, Array.Empty<ProcessModel>(), 10);

            Assert.Empty(processes);
            Assert.Null(result.SelectedProcess);
            Assert.True(result.SelectedProcessTerminated);
        }
    }
}
