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

        [ObservableProperty]
        private ProcessEfficiencyMode pendingEfficiencyMode = ProcessEfficiencyMode.SystemManaged;

        private PriorityUndoSnapshot? priorityUndo;

        [RelayCommand]
        private Task ApplySelectedCpuPriority() => this.SetContextCpuPriorityAsync(this.SelectedProcess, this.PendingCpuPriority);

        [RelayCommand]
        private Task ApplySelectedMemoryPriority() => this.SetContextMemoryPriorityAsync(this.SelectedProcess, this.PendingMemoryPriority);

        [RelayCommand]
        private Task ApplySelectedIoPriority() => this.SetContextIoPriorityAsync(this.SelectedProcess, this.PendingIoPriority);

        [RelayCommand]
        private Task ApplySelectedEfficiencyMode() => this.SetContextEfficiencyModeAsync(this.SelectedProcess, this.PendingEfficiencyMode);

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
