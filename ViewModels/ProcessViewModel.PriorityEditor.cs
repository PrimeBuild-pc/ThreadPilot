namespace ThreadPilot.ViewModels
{
    using System.ComponentModel;
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public partial class ProcessViewModel
    {
        // Null shows an empty picker: the value could not be read for this process.
        [ObservableProperty]
        private ProcessPriorityClass? pendingCpuPriority;

        [ObservableProperty]
        private ProcessMemoryPriority? pendingMemoryPriority;

        [ObservableProperty]
        private ProcessIoPriority? pendingIoPriority;

        [ObservableProperty]
        private ProcessEfficiencyMode? pendingEfficiencyMode;

        private PriorityUndoSnapshot? priorityUndo;

        [RelayCommand]
        private Task ApplySelectedCpuPriority() =>
            this.PendingCpuPriority is { } value ? this.SetContextCpuPriorityAsync(this.SelectedProcess, value) : Task.CompletedTask;

        [RelayCommand]
        private Task ApplySelectedMemoryPriority() =>
            this.PendingMemoryPriority is { } value ? this.SetContextMemoryPriorityAsync(this.SelectedProcess, value) : Task.CompletedTask;

        [RelayCommand]
        private Task ApplySelectedIoPriority() =>
            this.PendingIoPriority is { } value ? this.SetContextIoPriorityAsync(this.SelectedProcess, value) : Task.CompletedTask;

        [RelayCommand]
        private Task ApplySelectedEfficiencyMode() =>
            this.PendingEfficiencyMode is { } value ? this.SetContextEfficiencyModeAsync(this.SelectedProcess, value) : Task.CompletedTask;

        // The summary is re-read on selection and after every change, so the pickers start from what the process has now.
        private void OnSelectedProcessSummaryChanged(object? sender, PropertyChangedEventArgs e)
        {
            var summary = this.SelectedProcessSummary;
            switch (e.PropertyName)
            {
                case nameof(SelectedProcessSummaryViewModel.CpuPriority):
                case nameof(SelectedProcessSummaryViewModel.HasSelection):
                    this.PendingCpuPriority = summary.HasSelection ? summary.CpuPriority : null;
                    break;
                case nameof(SelectedProcessSummaryViewModel.MemoryPriority):
                    this.PendingMemoryPriority = summary.MemoryPriority;
                    break;
                case nameof(SelectedProcessSummaryViewModel.IoPriority):
                    this.PendingIoPriority = summary.IoPriority;
                    break;
                case nameof(SelectedProcessSummaryViewModel.EfficiencyMode):
                    this.PendingEfficiencyMode = summary.EfficiencyMode;
                    break;
            }
        }

        [RelayCommand]
        private Task SetContextEfficiencyOn(ProcessModel? process) => this.SetContextEfficiencyModeAsync(process, ProcessEfficiencyMode.On);

        [RelayCommand]
        private Task SetContextEfficiencyOff(ProcessModel? process) => this.SetContextEfficiencyModeAsync(process, ProcessEfficiencyMode.Off);

        [RelayCommand]
        private Task SetContextEfficiencySystemManaged(ProcessModel? process) => this.SetContextEfficiencyModeAsync(process, ProcessEfficiencyMode.SystemManaged);

        private async Task SetContextEfficiencyModeAsync(ProcessModel? process, ProcessEfficiencyMode mode)
        {
            if (process == null || this.memoryPriorityService == null)
            {
                return;
            }

            var previous = await this.memoryPriorityService.GetEfficiencyModeAsync(process);
            var undo = previous.HasValue ? CapturePriorityUndo(process, efficiency: previous) : null;
            var result = await this.memoryPriorityService.SetEfficiencyModeAsync(process, mode);
            if (!result.Success)
            {
                this.SetContextError(result.UserMessage);
                await this.LogUserActionAsync(
                    "ProcessEfficiencyModeFailed",
                    result.UserMessage,
                    $"Process: {process.Name}, PID: {process.ProcessId}, Mode: {mode}");
                await this.UpdateSelectedProcessSummaryAsync(process);
                return;
            }

            this.RememberPriority(undo);
            this.SetStatus($"Efficiency mode applied to {process.Name}: {mode}.", false);
            await this.LogUserActionAsync(
                "ProcessEfficiencyModeChanged",
                $"Efficiency mode changed for {process.Name}: {mode}",
                $"PID: {process.ProcessId}");
            await this.UpdateSelectedProcessSummaryAsync(process);
        }

        private bool CanUndoPriority() => this.priorityUndo != null;

        private void RememberPriority(PriorityUndoSnapshot? snapshot)
        {
            this.priorityUndo = snapshot;
            this.UndoPriorityCommand.NotifyCanExecuteChanged();
        }

        private static PriorityUndoSnapshot? CapturePriorityUndo(
            ProcessModel process, bool cpu = false, ProcessMemoryPriority? memory = null, ProcessIoPriority? io = null,
            ProcessEfficiencyMode? efficiency = null)
        {
            try
            {
                using var native = Process.GetProcessById(process.ProcessId);
                return new(process.ProcessId, native.StartTime.ToUniversalTime(), process.Name,
                    cpu ? native.PriorityClass : null, memory, io, efficiency);
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

                    var result = undo.Memory.HasValue ? await this.memoryPriorityService.SetMemoryPriorityAsync(process, undo.Memory.Value)
                        : undo.Io.HasValue ? await this.memoryPriorityService.SetIoPriorityAsync(process, undo.Io.Value)
                        : await this.memoryPriorityService.SetEfficiencyModeAsync(process, undo.Efficiency!.Value);
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
            ProcessPriorityClass? Cpu, ProcessMemoryPriority? Memory, ProcessIoPriority? Io, ProcessEfficiencyMode? Efficiency)
        {
            public bool Matches(int processId, DateTime startedUtc) => this.ProcessId == processId && this.StartedUtc == startedUtc;
        }
    }
}
