namespace ThreadPilot.Core.Tests
{
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.ViewModels;

    public sealed partial class ThreadCpuSetDiagnosticTests
    {
        [Fact]
        public void NoOverride_IsNotAnExplicitSelection()
        {
            var observation = ThreadCpuSetDiagnosticClassifier.Classify(
                ThreadCpuSetProbe.NoOverride(11, new ProcessorRef(0, 1, 1), (false, 0, 0)),
                CpuSetMapping.Empty);

            Assert.Equal(CpuSetReadKind.Empty, observation.Kind);
            Assert.False(observation.HasExplicitOverride);
            Assert.Empty(observation.CpuSetIds);
        }

        [Fact]
        public void Override_IsSeparateFromTheProcessDefault()
        {
            var mapping = Mapping((0, 1, 1, 10u), (0, 2, 2, 20u));
            var diagnostic = ThreadCpuSetDiagnosticClassifier.Combine(
                new CpuSetIdRead(CpuSetReadKind.Read, [10], 0),
                new ThreadCpuSetProbeResult(true, null, [ThreadCpuSetProbe.Override(42, [20], new ProcessorRef(0, 2, 2), (false, 0, 0))]),
                mapping);

            Assert.Equal([10u], diagnostic.DefaultCpuSetIds);
            var thread = Assert.Single(diagnostic.Threads);
            Assert.True(thread.HasExplicitOverride);
            Assert.Equal([20u], thread.CpuSetIds);
            Assert.Equal(0, diagnostic.InheritedCount);
            Assert.Equal(1, diagnostic.OverrideCount);
            var text = CpuSetDiagnosticFormatter.Format(diagnostic, (_, fallback) => fallback);
            Assert.Contains("Process default: CPU 1.", text, StringComparison.Ordinal);
            Assert.Contains("TID 42: explicit override, CPU 2.", text, StringComparison.Ordinal);
            Assert.Contains("not the CPU a thread is running on", text, StringComparison.Ordinal);
        }

        [Fact]
        public void TerminatedThread_IsReportedAndDoesNotCountAsInherited()
        {
            var diagnostic = Combine(ThreadCpuSetProbe.OpenFailed(8, 87));

            Assert.Equal(CpuSetReadKind.Terminated, Assert.Single(diagnostic.Threads).Kind);
            Assert.Equal(1, diagnostic.TerminatedCount);
            Assert.Equal(0, diagnostic.InheritedCount);
            Assert.True(diagnostic.IsPartial);
            Assert.Contains("thread ended during the read", CpuSetDiagnosticFormatter.Format(diagnostic, (_, fallback) => fallback), StringComparison.Ordinal);
        }

        [Fact]
        public void AccessDenied_IsShownAsDeniedNotAsNoOverride()
        {
            var diagnostic = Combine(ThreadCpuSetProbe.OpenFailed(9, 5), ThreadCpuSetProbe.NoOverride(10, null, (false, 0, 0)));

            Assert.Equal(1, diagnostic.DeniedCount);
            Assert.Equal(1, diagnostic.InheritedCount);
            Assert.True(diagnostic.IsPartial);
            var text = CpuSetDiagnosticFormatter.Format(diagnostic, (_, fallback) => fallback);
            Assert.Contains("TID 9: read denied.", text, StringComparison.Ordinal);
            Assert.Contains("Read is partial or denied.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("TID 9: explicit override", text, StringComparison.Ordinal);
        }

        [Fact]
        public void UnmappedCpuSetId_IsAPartialRead()
        {
            var mapping = Mapping((0, 0, 0, 10u));
            var observation = ThreadCpuSetDiagnosticClassifier.Classify(
                ThreadCpuSetProbe.Override(4, [10, 999], null, (false, 0, 0)),
                mapping);

            Assert.Equal(CpuSetReadKind.Partial, observation.Kind);
            Assert.True(observation.HasExplicitOverride);
            Assert.True(observation.MappingIncomplete);
            Assert.Equal(new ProcessorRef(0, 0, 0), Assert.Single(observation.Processors));
        }

        [Fact]
        public void MultipleGroups_AreNotFlattenedIntoOneMask()
        {
            var mapping = CpuSetMapping.Create(new Dictionary<ProcessorRef, uint>
            {
                [new ProcessorRef(0, 1, 1)] = 10,
                [new ProcessorRef(1, 0, 64)] = 20,
            });
            var observation = ThreadCpuSetDiagnosticClassifier.Classify(
                ThreadCpuSetProbe.Override(7, [20, 10], null, (true, 0, 0x2)),
                mapping);

            Assert.Equal([(ushort)0, (ushort)1], observation.Processors.Select(processor => processor.Group));
            Assert.False(observation.AffinityConflictsWithAssignment);
            var text = CpuSetDiagnosticFormatter.FormatProcessors(observation.Processors);
            Assert.Equal("G0:CPU 1, G1:CPU 0", text);
        }

        [Fact]
        public void RigidAffinity_IsDistinctFromTheCpuSetAssignment()
        {
            var mapping = Mapping((0, 2, 2, 20u));
            var observation = ThreadCpuSetDiagnosticClassifier.Classify(
                ThreadCpuSetProbe.Override(5, [20], null, (true, 0, 0x1)),
                mapping);

            Assert.True(observation.AffinityConflictsWithAssignment);
            Assert.Contains(
                "Hard affinity does not include this assignment.",
                CpuSetDiagnosticFormatter.Format(
                    ThreadCpuSetDiagnosticClassifier.Combine(
                        new CpuSetIdRead(CpuSetReadKind.Empty, [], 0),
                        new ThreadCpuSetProbeResult(true, null, [ThreadCpuSetProbe.Override(5, [20], null, (true, 0, 0x1))]),
                        mapping),
                    (_, fallback) => fallback),
                StringComparison.Ordinal);
        }

        [Fact]
        public void LiveProcess_NoOverrideThenOverrideThenTerminatedThread()
        {
            using var handler = new ProcessCpuSetHandler((uint)Environment.ProcessId, "ThreadPilot.Tests");
            var cpuSetIds = handler.Mapping.CpuSetIds;
            Assert.NotEmpty(cpuSetIds);
            var cpuSetId = cpuSetIds[0];

            using var started = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            var threadId = 0;
            var thread = new Thread(() =>
            {
                threadId = (int)Native.GetCurrentThreadId();
                started.Set();
                release.Wait();
            });
            thread.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

            try
            {
                var before = ProcessThreadCpuAssignmentHandler.ProbeThread(threadId);
                Assert.False(before.RequiredCount > 0 && before.Ids.Count > 0, "The worker started with an unexpected CPU Set override.");

                using (var handle = Native.OpenThread(0x0400 | 0x0800, false, (uint)threadId))
                {
                    Assert.False(handle.IsInvalid);
                    Assert.True(Native.SetThreadSelectedCpuSets(handle, [cpuSetId], 1));
                    var overridden = ProcessThreadCpuAssignmentHandler.ProbeThread(threadId);
                    Assert.Equal([cpuSetId], overridden.Ids);
                    Assert.True(Native.SetThreadSelectedCpuSets(handle, null, 0));
                }

                var cleared = ProcessThreadCpuAssignmentHandler.ProbeThread(threadId);
                Assert.Equal(0u, cleared.RequiredCount);
                Assert.Empty(cleared.Ids);
            }
            finally
            {
                release.Set();
                Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
            }

            // A just-exited thread object can still be opened until Windows drops it, and the id can be reused.
            // ERROR_INVALID_PARAMETER from OpenThread is the terminated signal the reader records; a tid that
            // was never a thread produces that error without the race.
            var ended = ProcessThreadCpuAssignmentHandler.ProbeThread(int.MaxValue);
            Assert.False(ended.Opened);
            Assert.Equal(87, ended.OpenError);
            Assert.Equal(
                CpuSetReadKind.Terminated,
                ThreadCpuSetDiagnosticClassifier.Classify(ended, handler.Mapping).Kind);
        }

        private static ProcessCpuSetDiagnostic Combine(params ThreadCpuSetProbe[] probes) =>
            ThreadCpuSetDiagnosticClassifier.Combine(
                new CpuSetIdRead(CpuSetReadKind.Empty, [], 0),
                new ThreadCpuSetProbeResult(true, null, probes),
                CpuSetMapping.Empty);

        private static CpuSetMapping Mapping(params (ushort Group, byte Number, int Global, uint Id)[] items) =>
            CpuSetMapping.Create(items.ToDictionary(
                item => new ProcessorRef(item.Group, item.Number, item.Global),
                item => item.Id));

        private static partial class Native
        {
            [LibraryImport("kernel32.dll")]
            internal static partial uint GetCurrentThreadId();

            [LibraryImport("kernel32.dll", SetLastError = true)]
            internal static partial SafeWaitHandle OpenThread(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint threadId);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool SetThreadSelectedCpuSets(SafeWaitHandle thread, uint[]? cpuSetIds, uint cpuSetIdCount);
        }
    }
}
