/*
 * ThreadPilot - lightweight selected process summary view model.
 */
namespace ThreadPilot.ViewModels
{
    using System.Diagnostics;
    using System.Threading;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class SelectedProcessSummaryViewModel : ObservableObject
    {
        private readonly IProcessMemoryPriorityService? memoryPriorityService;
        private readonly IPersistentProcessRuleStore? persistentRuleStore;
        private readonly IPersistentProcessRuleMatcher? persistentRuleMatcher;
        private bool hasSelection;
        private int processId;
        private string processName = string.Empty;
        private string executablePath = string.Empty;
        private double cpuUsage;
        private long memoryUsage;
        private ProcessPriorityClass cpuPriority;
        private long processorAffinity;
        private ProcessMemoryPriority? memoryPriority;
        private string processTitle = "No process selected";
        private string currentProcessStatusText = "No process selected";
        private string cpuUsageText = "CPU: unavailable";
        private string memoryUsageText = "Memory: unavailable";
        private string cpuPriorityText = "CPU priority: unavailable";
        private string memoryPriorityText = "Memory priority unavailable";
        private string affinityText = "Affinity: unavailable";
        private string ruleStatusText = "No saved rule";
        private string lastOperationMessage = "No recent ThreadPilot action";
        private string lastOperationSeverity = "Information";
        private bool isProtectedOrAccessDenied;
        private bool hasThreadPilotRule;
        private int updateVersion;

        public SelectedProcessSummaryViewModel(
            IProcessMemoryPriorityService? memoryPriorityService = null,
            IPersistentProcessRuleStore? persistentRuleStore = null,
            IPersistentProcessRuleMatcher? persistentRuleMatcher = null)
        {
            this.memoryPriorityService = memoryPriorityService;
            this.persistentRuleStore = persistentRuleStore;
            this.persistentRuleMatcher = persistentRuleMatcher;
        }

        public bool HasSelection
        {
            get => this.hasSelection;
            private set => this.SetProperty(ref this.hasSelection, value);
        }

        public int ProcessId
        {
            get => this.processId;
            private set => this.SetProperty(ref this.processId, value);
        }

        public string ProcessName
        {
            get => this.processName;
            private set => this.SetProperty(ref this.processName, value);
        }

        public string ExecutablePath
        {
            get => this.executablePath;
            private set => this.SetProperty(ref this.executablePath, value);
        }

        public double CpuUsage
        {
            get => this.cpuUsage;
            private set => this.SetProperty(ref this.cpuUsage, value);
        }

        public long MemoryUsage
        {
            get => this.memoryUsage;
            private set => this.SetProperty(ref this.memoryUsage, value);
        }

        public ProcessPriorityClass CpuPriority
        {
            get => this.cpuPriority;
            private set => this.SetProperty(ref this.cpuPriority, value);
        }

        public long ProcessorAffinity
        {
            get => this.processorAffinity;
            private set => this.SetProperty(ref this.processorAffinity, value);
        }

        public ProcessMemoryPriority? MemoryPriority
        {
            get => this.memoryPriority;
            private set => this.SetProperty(ref this.memoryPriority, value);
        }

        public string ProcessTitle
        {
            get => this.processTitle;
            private set => this.SetProperty(ref this.processTitle, value);
        }

        public string CurrentProcessStatusText
        {
            get => this.currentProcessStatusText;
            private set => this.SetProperty(ref this.currentProcessStatusText, value);
        }

        public string CpuUsageText
        {
            get => this.cpuUsageText;
            private set => this.SetProperty(ref this.cpuUsageText, value);
        }

        public string MemoryUsageText
        {
            get => this.memoryUsageText;
            private set => this.SetProperty(ref this.memoryUsageText, value);
        }

        public string CpuPriorityText
        {
            get => this.cpuPriorityText;
            private set => this.SetProperty(ref this.cpuPriorityText, value);
        }

        public string MemoryPriorityText
        {
            get => this.memoryPriorityText;
            private set => this.SetProperty(ref this.memoryPriorityText, value);
        }

        public string AffinityText
        {
            get => this.affinityText;
            private set => this.SetProperty(ref this.affinityText, value);
        }

        public string RuleStatusText
        {
            get => this.ruleStatusText;
            private set => this.SetProperty(ref this.ruleStatusText, value);
        }

        public string LastOperationMessage
        {
            get => this.lastOperationMessage;
            private set => this.SetProperty(ref this.lastOperationMessage, value);
        }

        public string LastOperationSeverity
        {
            get => this.lastOperationSeverity;
            private set => this.SetProperty(ref this.lastOperationSeverity, value);
        }

        public bool IsProtectedOrAccessDenied
        {
            get => this.isProtectedOrAccessDenied;
            private set => this.SetProperty(ref this.isProtectedOrAccessDenied, value);
        }

        public bool HasThreadPilotRule
        {
            get => this.hasThreadPilotRule;
            private set => this.SetProperty(ref this.hasThreadPilotRule, value);
        }

        public async Task UpdateAsync(
            ProcessModel? process,
            string? lastOperationMessage = null,
            bool lastOperationIsError = false)
        {
            var version = Interlocked.Increment(ref this.updateVersion);
            if (process == null)
            {
                this.Clear(version, lastOperationMessage, lastOperationIsError);
                return;
            }

            this.HasSelection = true;
            this.ProcessId = process.ProcessId;
            this.ProcessName = process.Name ?? string.Empty;
            this.ExecutablePath = process.ExecutablePath ?? string.Empty;
            this.CpuUsage = process.CpuUsage;
            this.MemoryUsage = process.MemoryUsage;
            this.CpuPriority = process.Priority;
            this.ProcessorAffinity = process.ProcessorAffinity;
            this.IsProtectedOrAccessDenied = process.Classification == ProcessClassification.ProtectedOrAccessDenied;
            this.ProcessTitle = $"Selected process: {this.ProcessName} (PID {this.ProcessId})";
            this.CurrentProcessStatusText = this.IsProtectedOrAccessDenied
                ? "Current process status: protected or access denied"
                : "Current process status: selected";
            this.CpuUsageText = $"CPU: {this.CpuUsage:N1}%";
            this.MemoryUsageText = $"Memory: {FormatMemory(this.MemoryUsage)}";
            this.CpuPriorityText = $"CPU priority: {this.CpuPriority}";
            this.AffinityText = $"Affinity: legacy mask 0x{this.ProcessorAffinity:X}";
            this.UpdateLastOperation(lastOperationMessage, lastOperationIsError);

            await this.UpdateMemoryPriorityAsync(process, version);
            if (!this.IsCurrentVersion(version))
            {
                return;
            }

            await this.UpdateRuleStatusAsync(process, version);
        }

        private static string FormatMemory(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 MB";
            }

            var megabytes = bytes / 1024d / 1024d;
            return $"{megabytes:N0} MB";
        }

        private void Clear(int version, string? lastOperationMessage, bool lastOperationIsError)
        {
            if (!this.IsCurrentVersion(version))
            {
                return;
            }

            this.HasSelection = false;
            this.ProcessId = 0;
            this.ProcessName = string.Empty;
            this.ExecutablePath = string.Empty;
            this.CpuUsage = 0;
            this.MemoryUsage = 0;
            this.CpuPriority = default;
            this.ProcessorAffinity = 0;
            this.MemoryPriority = null;
            this.IsProtectedOrAccessDenied = false;
            this.HasThreadPilotRule = false;
            this.ProcessTitle = "No process selected";
            this.CurrentProcessStatusText = "No process selected";
            this.CpuUsageText = "CPU: unavailable";
            this.MemoryUsageText = "Memory: unavailable";
            this.CpuPriorityText = "CPU priority: unavailable";
            this.MemoryPriorityText = "Memory priority unavailable";
            this.AffinityText = "Affinity: unavailable";
            this.RuleStatusText = "No saved rule";
            this.UpdateLastOperation(lastOperationMessage, lastOperationIsError);
        }

        private async Task UpdateMemoryPriorityAsync(ProcessModel process, int version)
        {
            this.MemoryPriority = null;
            this.MemoryPriorityText = "Memory priority unavailable";

            if (this.memoryPriorityService == null)
            {
                return;
            }

            try
            {
                var priority = await this.memoryPriorityService.GetMemoryPriorityAsync(process);
                if (!this.IsCurrentVersion(version))
                {
                    return;
                }

                if (priority == null)
                {
                    return;
                }

                this.MemoryPriority = priority.Value;
                this.MemoryPriorityText = $"Memory priority: {priority.Value}";
            }
            catch (Exception)
            {
                if (!this.IsCurrentVersion(version))
                {
                    return;
                }

                this.MemoryPriority = null;
                this.MemoryPriorityText = "Memory priority unavailable";
            }
        }

        private async Task UpdateRuleStatusAsync(ProcessModel process, int version)
        {
            this.HasThreadPilotRule = false;
            this.RuleStatusText = "No saved rule";

            if (this.persistentRuleStore == null || this.persistentRuleMatcher == null)
            {
                return;
            }

            try
            {
                var rules = await this.persistentRuleStore.LoadAsync();
                if (!this.IsCurrentVersion(version))
                {
                    return;
                }

                var matchingRule = rules.FirstOrDefault(rule => this.persistentRuleMatcher.IsMatch(rule, process));
                if (matchingRule == null)
                {
                    return;
                }

                this.HasThreadPilotRule = true;
                var ruleName = string.IsNullOrWhiteSpace(matchingRule.Name) ? "saved rule" : matchingRule.Name.Trim();
                this.RuleStatusText = $"Saved rule exists: {ruleName}";
            }
            catch (Exception)
            {
                if (!this.IsCurrentVersion(version))
                {
                    return;
                }

                this.HasThreadPilotRule = false;
                this.RuleStatusText = "No saved rule";
            }
        }

        private bool IsCurrentVersion(int version) => Volatile.Read(ref this.updateVersion) == version;

        private void UpdateLastOperation(string? message, bool isError)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                this.LastOperationMessage = "No recent ThreadPilot action";
                this.LastOperationSeverity = "Information";
                return;
            }

            this.LastOperationMessage = message.Trim();
            this.LastOperationSeverity = isError ? "Error" : "Information";
        }
    }
}
