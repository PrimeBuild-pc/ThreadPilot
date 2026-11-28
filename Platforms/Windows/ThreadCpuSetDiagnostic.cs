namespace ThreadPilot.Platforms.Windows
{
    using ThreadPilot.Models;

    internal enum CpuSetReadKind
    {
        Read,
        Empty,
        AccessDenied,
        Terminated,
        Unavailable,
        Partial,
    }

    internal sealed record CpuSetIdRead(CpuSetReadKind Kind, IReadOnlyList<uint> Ids, int Win32Error);

    internal readonly record struct ThreadCpuSetProbe(
        int ThreadId,
        bool Opened,
        int OpenError,
        bool Queried,
        int QueryError,
        uint RequiredCount,
        IReadOnlyList<uint> Ids,
        ProcessorRef? IdealProcessor,
        bool AffinityRead,
        ushort AffinityGroup,
        ulong AffinityMask)
    {
        public static ThreadCpuSetProbe OpenFailed(int threadId, int error) =>
            new(threadId, false, error, false, 0, 0, [], null, false, 0, 0);

        public static ThreadCpuSetProbe QueryFailed(int threadId, int error, ProcessorRef? ideal, (bool Read, ushort Group, ulong Mask) affinity) =>
            new(threadId, true, 0, false, error, 0, [], ideal, affinity.Read, affinity.Group, affinity.Mask);

        public static ThreadCpuSetProbe NoOverride(int threadId, ProcessorRef? ideal, (bool Read, ushort Group, ulong Mask) affinity) =>
            new(threadId, true, 0, true, 0, 0, [], ideal, affinity.Read, affinity.Group, affinity.Mask);

        public static ThreadCpuSetProbe Override(int threadId, IReadOnlyList<uint> ids, ProcessorRef? ideal, (bool Read, ushort Group, ulong Mask) affinity) =>
            new(threadId, true, 0, true, 0, (uint)ids.Count, ids, ideal, affinity.Read, affinity.Group, affinity.Mask);
    }

    internal sealed record ThreadCpuSetProbeResult(bool ProcessEnumerated, int? ProcessError, IReadOnlyList<ThreadCpuSetProbe> Threads);

    internal sealed record ThreadCpuSetObservation(
        int ThreadId,
        CpuSetReadKind Kind,
        bool HasExplicitOverride,
        IReadOnlyList<uint> CpuSetIds,
        IReadOnlyList<ProcessorRef> Processors,
        bool MappingIncomplete,
        int? Win32Error,
        ProcessorRef? IdealProcessor,
        bool AffinityConflictsWithAssignment);

    internal sealed record ProcessCpuSetDiagnostic(
        CpuSetReadKind DefaultKind,
        IReadOnlyList<uint> DefaultCpuSetIds,
        IReadOnlyList<ProcessorRef> DefaultProcessors,
        bool DefaultMappingIncomplete,
        bool ThreadsEnumerated,
        int? ThreadEnumerationError,
        IReadOnlyList<ThreadCpuSetObservation> Threads)
    {
        public int InheritedCount => this.Threads.Count(thread => thread.Kind == CpuSetReadKind.Empty && !thread.HasExplicitOverride);

        public int OverrideCount => this.Threads.Count(thread => thread.HasExplicitOverride);

        public int DeniedCount => this.Threads.Count(thread => thread.Kind == CpuSetReadKind.AccessDenied);

        public int TerminatedCount => this.Threads.Count(thread => thread.Kind == CpuSetReadKind.Terminated);

        public int PartialCount => this.Threads.Count(thread => thread.Kind == CpuSetReadKind.Partial || thread.MappingIncomplete);

        public bool IsPartial =>
            this.DefaultKind is CpuSetReadKind.Partial or CpuSetReadKind.AccessDenied or CpuSetReadKind.Unavailable
            || this.DefaultMappingIncomplete
            || !this.ThreadsEnumerated
            || this.Threads.Any(thread => thread.Kind is not CpuSetReadKind.Read and not CpuSetReadKind.Empty || thread.MappingIncomplete);
    }

    internal static class ThreadCpuSetDiagnosticClassifier
    {
        public static ProcessCpuSetDiagnostic Combine(
            CpuSetIdRead defaults,
            ThreadCpuSetProbeResult probes,
            CpuSetMapping mapping)
        {
            ArgumentNullException.ThrowIfNull(defaults);
            ArgumentNullException.ThrowIfNull(probes);
            ArgumentNullException.ThrowIfNull(mapping);

            var mappedDefault = Map(defaults.Ids, mapping);
            var defaultKind = defaults.Kind;
            if (defaultKind == CpuSetReadKind.Read && mappedDefault.Incomplete)
            {
                defaultKind = CpuSetReadKind.Partial;
            }

            var threads = probes.Threads
                .Select(probe => Classify(probe, mapping))
                .OrderBy(thread => thread.Kind == CpuSetReadKind.Empty ? 1 : 0)
                .ThenBy(thread => thread.ThreadId)
                .ToList();

            return new ProcessCpuSetDiagnostic(
                defaultKind,
                defaults.Ids,
                mappedDefault.Processors,
                mappedDefault.Incomplete,
                probes.ProcessEnumerated,
                probes.ProcessError,
                threads);
        }

        public static ThreadCpuSetObservation Classify(ThreadCpuSetProbe probe, CpuSetMapping mapping)
        {
            ArgumentNullException.ThrowIfNull(mapping);

            if (!probe.Opened)
            {
                return Failed(probe.ThreadId, KindForOpenError(probe.OpenError), probe.OpenError, probe);
            }

            if (!probe.Queried)
            {
                return Failed(probe.ThreadId, KindForQueryError(probe.QueryError), probe.QueryError, probe);
            }

            if (probe.RequiredCount == 0 || probe.Ids.Count == 0)
            {
                return new ThreadCpuSetObservation(
                    probe.ThreadId,
                    CpuSetReadKind.Empty,
                    false,
                    [],
                    [],
                    false,
                    null,
                    probe.IdealProcessor,
                    false);
            }

            var mapped = Map(probe.Ids, mapping);
            var kind = mapped.Incomplete ? CpuSetReadKind.Partial : CpuSetReadKind.Read;
            return new ThreadCpuSetObservation(
                probe.ThreadId,
                kind,
                true,
                probe.Ids,
                mapped.Processors,
                mapped.Incomplete,
                null,
                probe.IdealProcessor,
                AffinityConflicts(probe, mapped.Processors));
        }

        public static (IReadOnlyList<ProcessorRef> Processors, bool Incomplete) Map(IReadOnlyList<uint> ids, CpuSetMapping mapping)
        {
            var processors = new List<ProcessorRef>();
            var incomplete = false;
            foreach (var id in ids.Distinct().OrderBy(id => id))
            {
                if (mapping.TryGetProcessorRef(id, out var processor))
                {
                    processors.Add(processor);
                }
                else
                {
                    incomplete = true;
                }
            }

            if (ids.Count > 0 && mapping.IsEmpty)
            {
                incomplete = true;
            }

            return (processors, incomplete);
        }

        private static ThreadCpuSetObservation Failed(int threadId, CpuSetReadKind kind, int error, ThreadCpuSetProbe probe) =>
            new(threadId, kind, false, [], [], kind == CpuSetReadKind.Partial, error, probe.IdealProcessor, false);

        private static CpuSetReadKind KindForOpenError(int error) => error switch
        {
            5 => CpuSetReadKind.AccessDenied,
            87 => CpuSetReadKind.Terminated,
            _ => CpuSetReadKind.Unavailable,
        };

        private static CpuSetReadKind KindForQueryError(int error) => error switch
        {
            5 => CpuSetReadKind.AccessDenied,
            87 => CpuSetReadKind.Terminated,
            122 => CpuSetReadKind.Partial,
            _ => CpuSetReadKind.Unavailable,
        };

        private static bool AffinityConflicts(ThreadCpuSetProbe probe, IReadOnlyList<ProcessorRef> processors)
        {
            if (!probe.AffinityRead || processors.Count == 0)
            {
                return false;
            }

            return processors.Any(processor =>
                processor.Group != probe.AffinityGroup
                || processor.LogicalProcessorNumber >= 64
                || (probe.AffinityMask & (1UL << processor.LogicalProcessorNumber)) == 0);
        }
    }

    internal interface ICpuSetDiagnosticSource
    {
        ProcessCpuSetDiagnostic Read(int processId, string name);
    }

    internal sealed class ProcessCpuSetDiagnosticReader : ICpuSetDiagnosticSource
    {
        public static ProcessCpuSetDiagnosticReader Instance { get; } = new();

        public ProcessCpuSetDiagnostic Read(int processId, string name)
        {
            using var handler = new ProcessCpuSetHandler((uint)processId, name);
            var defaults = handler.ReadDefaultCpuSets();
            var probes = ProcessThreadCpuAssignmentHandler.ProbeSelectedCpuSets(processId);
            return ThreadCpuSetDiagnosticClassifier.Combine(defaults, probes, handler.Mapping);
        }
    }
}
