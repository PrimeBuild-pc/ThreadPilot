namespace ThreadPilot.ViewModels
{
    using System.Globalization;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;

    internal static class CpuSetDiagnosticFormatter
    {
        public static string Format(ProcessCpuSetDiagnostic diagnostic, Func<string, string, string> text)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);
            ArgumentNullException.ThrowIfNull(text);

            var lines = new List<string>
            {
                FormatDefault(diagnostic, text),
                string.Format(
                    CultureInfo.CurrentCulture,
                    text(
                        "ProcessView_CpuSetDiag_Summary",
                        "{0} threads inherit the process default; {1} have an explicit override; {2} reads were denied; {3} threads ended during the read; {4} reads were partial."),
                    diagnostic.InheritedCount,
                    diagnostic.OverrideCount,
                    diagnostic.DeniedCount,
                    diagnostic.TerminatedCount,
                    diagnostic.PartialCount),
            };

            if (!diagnostic.ThreadsEnumerated)
            {
                lines.Add(text(
                    "ProcessView_CpuSetDiag_ThreadsUnlisted",
                    "The thread list could not be read. The summary above does not include those threads."));
            }

            if (diagnostic.IsPartial)
            {
                lines.Add(text(
                    "ProcessView_CpuSetDiag_PartialBanner",
                    "Read is partial or denied. Missing threads are not the same as threads that inherit the process default."));
            }

            lines.Add(text(
                "ProcessView_CpuSetDiag_NotRunningCpu",
                "These are assignments read from Windows, not the CPU a thread is running on."));

            foreach (var thread in diagnostic.Threads.Where(item => item.HasExplicitOverride || item.Kind is CpuSetReadKind.AccessDenied or CpuSetReadKind.Terminated or CpuSetReadKind.Unavailable or CpuSetReadKind.Partial).Take(12))
            {
                lines.Add(FormatThread(thread, text));
            }

            var listed = diagnostic.Threads.Count(item => item.HasExplicitOverride || item.Kind is CpuSetReadKind.AccessDenied or CpuSetReadKind.Terminated or CpuSetReadKind.Unavailable or CpuSetReadKind.Partial);
            if (listed > 12)
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    text("ProcessView_CpuSetDiag_More", "And {0} more threads were not listed."),
                    listed - 12));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static string FormatProcessors(IReadOnlyList<ProcessorRef> processors)
        {
            if (processors.Count == 0)
            {
                return string.Empty;
            }

            var showGroup = processors.Any(processor => processor.Group != 0);
            return string.Join(", ", processors.Select(processor => showGroup
                ? FormattableString.Invariant($"G{processor.Group}:CPU {processor.LogicalProcessorNumber}")
                : FormattableString.Invariant($"CPU {processor.LogicalProcessorNumber}")));
        }

        private static string FormatDefault(ProcessCpuSetDiagnostic diagnostic, Func<string, string, string> text)
        {
            return diagnostic.DefaultKind switch
            {
                CpuSetReadKind.Empty => text(
                    "ProcessView_CpuSetDiag_DefaultNone",
                    "Process default: none. Threads without an override are placed by Windows."),
                CpuSetReadKind.AccessDenied => text(
                    "ProcessView_CpuSetDiag_DefaultDenied",
                    "Process default: read denied."),
                CpuSetReadKind.Unavailable => text(
                    "ProcessView_CpuSetDiag_DefaultUnavailable",
                    "Process default: could not be read."),
                _ => string.Format(
                    CultureInfo.CurrentCulture,
                    diagnostic.DefaultMappingIncomplete
                        ? text("ProcessView_CpuSetDiag_DefaultPartial", "Process default: partial read, {0}.")
                        : text("ProcessView_CpuSetDiag_DefaultRead", "Process default: {0}."),
                    DescribeIds(diagnostic.DefaultCpuSetIds, diagnostic.DefaultProcessors)),
            };
        }

        private static string FormatThread(ThreadCpuSetObservation thread, Func<string, string, string> text)
        {
            if (thread.Kind == CpuSetReadKind.AccessDenied)
            {
                return string.Format(CultureInfo.CurrentCulture, text("ProcessView_CpuSetDiag_ThreadDenied", "TID {0}: read denied."), thread.ThreadId);
            }

            if (thread.Kind == CpuSetReadKind.Terminated)
            {
                return string.Format(CultureInfo.CurrentCulture, text("ProcessView_CpuSetDiag_ThreadEnded", "TID {0}: thread ended during the read."), thread.ThreadId);
            }

            if (!thread.HasExplicitOverride)
            {
                return string.Format(CultureInfo.CurrentCulture, text("ProcessView_CpuSetDiag_ThreadUnread", "TID {0}: could not be read."), thread.ThreadId);
            }

            var assignment = DescribeIds(thread.CpuSetIds, thread.Processors);
            var line = string.Format(
                CultureInfo.CurrentCulture,
                thread.MappingIncomplete
                    ? text("ProcessView_CpuSetDiag_OverridePartial", "TID {0}: explicit override, partial read, {1}.")
                    : text("ProcessView_CpuSetDiag_Override", "TID {0}: explicit override, {1}."),
                thread.ThreadId,
                assignment);
            if (thread.IdealProcessor is ProcessorRef ideal)
            {
                line += " " + string.Format(
                    CultureInfo.CurrentCulture,
                    text("ProcessView_CpuSetDiag_Ideal", "Ideal CPU {0} (not the CPU it is running on)."),
                    FormatProcessors([ideal]));
            }

            if (thread.AffinityConflictsWithAssignment)
            {
                line += " " + text(
                    "ProcessView_CpuSetDiag_RigidAffinity",
                    "Hard affinity does not include this assignment.");
            }

            return line;
        }

        private static string DescribeIds(IReadOnlyList<uint> ids, IReadOnlyList<ProcessorRef> processors)
        {
            var formatted = FormatProcessors(processors);
            var unmapped = ids.Distinct().Count() - processors.Count;
            if (formatted.Length == 0)
            {
                return ids.Count == 0 ? "none" : "CPU set ids " + string.Join(", ", ids.Distinct().OrderBy(id => id));
            }

            return unmapped > 0
                ? formatted + ", " + unmapped.ToString(CultureInfo.CurrentCulture) + " unmapped"
                : formatted;
        }
    }
}
