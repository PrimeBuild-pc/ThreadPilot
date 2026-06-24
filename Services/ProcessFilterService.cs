namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreadPilot.Models;

    public sealed class ProcessFilterCriteria
    {
        public string SearchText { get; init; } = string.Empty;

        public bool HideSystemProcesses { get; init; }

        public bool HideIdleProcesses { get; init; }

        public string SortMode { get; init; } = "CpuUsage";
    }

    public class ProcessFilterService
    {
        private static readonly string[] SystemProcessNames =
        {
            "System", "Registry", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
            "services.exe", "lsass.exe", "svchost.exe", "spoolsv.exe", "explorer.exe",
            "dwm.exe", "audiodg.exe", "conhost.exe", "dllhost.exe", "rundll32.exe",
            "taskhostw.exe", "SearchIndexer.exe", "WmiPrvSE.exe", "MsMpEng.exe",
            "SecurityHealthService.exe", "SecurityHealthSystray.exe",
        };

        public IReadOnlyList<ProcessModel> FilterAndSort(IEnumerable<ProcessModel> source, ProcessFilterCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(criteria);

            var filtered = source;

            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                filtered = filtered.Where(p => p.Name.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.HideSystemProcesses)
            {
                filtered = filtered.Where(p => !this.IsSystemProcess(p));
            }

            if (criteria.HideIdleProcesses)
            {
                filtered = filtered.Where(p => p.CpuUsage > 0.1);
            }

            var sorted = criteria.SortMode switch
            {
                "CpuUsage" => filtered.OrderByDescending(p => p.CpuUsage),
                "MemoryUsage" => filtered.OrderByDescending(p => p.MemoryUsage),
                "Name" => filtered.OrderBy(p => p.Name),
                "ProcessId" => filtered.OrderBy(p => p.ProcessId),
                _ => filtered.OrderByDescending(p => p.CpuUsage),
            };

            return sorted.ToList();
        }

        public bool IsSystemProcess(ProcessModel process)
        {
            if (process == null)
            {
                return false;
            }

            var processName = NormalizeProcessName(process.Name);

            return SystemProcessNames.Any(sp => processName.Equals(NormalizeProcessName(sp), StringComparison.OrdinalIgnoreCase)) ||
                   processName.StartsWith("system", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeProcessName(string processName)
        {
            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
        }
    }
}
