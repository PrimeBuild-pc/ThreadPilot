namespace ThreadPilot.ViewModels
{
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public partial class ProcessViewModel
    {
        [ObservableProperty]
        private ProcessPriorityClass pendingCpuPriority = ProcessPriorityClass.Normal;

        [ObservableProperty]
        private ProcessMemoryPriority pendingMemoryPriority = ProcessMemoryPriority.Normal;

        [ObservableProperty]
        private ProcessIoPriority pendingIoPriority = ProcessIoPriority.Normal;

        private PriorityUndoSnapshot? priorityUndo;

        [RelayCommand]
        private Task ApplySelectedCpuPriority() => this.SetContextCpuPriorityAsync(this.SelectedProcess, this.PendingCpuPriority);

        [RelayCommand]
        private Task ApplySelectedMemoryPriority() => this.SetContextMemoryPriorityAsync(this.SelectedProcess, this.PendingMemoryPriority);

        [RelayCommand]
        private Task ApplySelectedIoPriority() => this.SetContextIoPriorityAsync(this.SelectedProcess, this.PendingIoPriority);

        private bool CanUndoPriority() => this.priorityUndo != null;

        private void RememberPriority(PriorityUndoSnapshot? snapshot)
        {
            this.priorityUndo = snapshot;
            this.UndoPriorityCommand.NotifyCanExecuteChanged();
        }

        private static PriorityUndoSnapshot? CapturePriorityUndo(
            ProcessModel process, bool cpu = false, ProcessMemoryPriority? memory = null, ProcessIoPriority? io = null)
        {
            try
            {
                using var native = Process.GetProcessById(process.ProcessId);
                return new(process.ProcessId, native.StartTime.ToUniversalTime(), process.Name,
                    cpu ? native.PriorityClass : null, memory, io);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        [RelayCommand(CanExecute = nameof(CanUndoPriority))]
        private async Task UndoPriorityAsync()
        {
            var undo = this.priorityUndo;
            if (undo == null)
            {
                return;
            }

            try
            {
                using var native = Process.GetProcessById(undo.ProcessId);
                if (!undo.Matches(native.Id, native.StartTime.ToUniversalTime()) || native.HasExited)
                {
                    this.RememberPriority(null);
                    this.SetContextError(ProcessOperationUserMessages.ProcessExited);
                    return;
                }

                var process = new ProcessModel { ProcessId = undo.ProcessId, Name = undo.Name };
                if (undo.Cpu.HasValue)
                {
                    await this.processService.SetProcessPriority(process, undo.Cpu.Value, ProcessPriorityWriteSource.ManualUiAction);
                }
                else
                {
                    if (this.memoryPriorityService == null)
                    {
                        return;
                    }

                    var result = undo.Memory.HasValue
                        ? await this.memoryPriorityService.SetMemoryPriorityAsync(process, undo.Memory.Value)
                        : await this.memoryPriorityService.SetIoPriorityAsync(process, undo.Io!.Value);
                    if (!result.Success)
                    {
                        this.SetContextError(result.UserMessage);
                        return;
                    }
                }

                this.RememberPriority(null);
                this.SetStatus(this.UiText("ProcessView_PriorityRestored", "Previous priority restored."), false);
                await this.LogUserActionAsync("ProcessPriorityRestored", $"Previous priority restored for {undo.Name}", $"PID: {undo.ProcessId}");
                if (this.SelectedProcess?.ProcessId == undo.ProcessId)
                {
                    await this.TryRefreshContextProcessSummaryAsync(this.SelectedProcess);
                }
            }
            catch (Exception ex)
            {
                this.SetContextError(MapProcessOperationException(ex));
            }
        }

        private string UiText(string key, string fallback)
        {
            var text = this.uiLocalization?.GetString(key);
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }

        internal sealed record PriorityUndoSnapshot(int ProcessId, DateTime StartedUtc, string Name,
            ProcessPriorityClass? Cpu, ProcessMemoryPriority? Memory, ProcessIoPriority? Io)
        {
            public bool Matches(int processId, DateTime startedUtc) => this.ProcessId == processId && this.StartedUtc == startedUtc;
        }
    }
}
