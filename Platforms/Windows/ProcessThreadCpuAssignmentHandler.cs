namespace ThreadPilot.Platforms.Windows
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;

    internal sealed partial class ProcessThreadCpuAssignmentHandler
    {
        private const uint ThreadSetInformation = 0x0020;
        private const uint ThreadQueryLimitedInformation = 0x0800;
        private const int ErrorAccessDenied = 5;
        private const int ErrorInvalidParameter = 87;

        private readonly int processId;

        public ProcessThreadCpuAssignmentHandler(int processId)
        {
            this.processId = processId;
        }

        public ThreadCpuAssignmentResult ApplyIdealProcessors(IReadOnlyList<ProcessorRef> processors)
        {
            var orderedProcessors = processors
                .Distinct()
                .OrderBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();
            if (orderedProcessors.Count == 0)
            {
                return ThreadCpuAssignmentResult.Failed("The CPU selection is empty.");
            }

            return this.ApplyToThreads((threadHandle, index) =>
            {
                var expected = ToProcessorNumber(orderedProcessors[index % orderedProcessors.Count]);
                if (!NativeMethods.SetThreadIdealProcessorEx(threadHandle, in expected, IntPtr.Zero))
                {
                    return false;
                }

                return NativeMethods.GetThreadIdealProcessorEx(threadHandle, out var observed) &&
                    observed.Group == expected.Group &&
                    observed.Number == expected.Number;
            });
        }

        public ThreadCpuAssignmentResult ApplyGroupAffinity(IReadOnlyList<ProcessorRef> processors)
        {
            var groups = BuildGroupAffinities(processors);
            if (groups.Count == 0)
            {
                return ThreadCpuAssignmentResult.Failed("The CPU selection is empty.");
            }

            return this.ApplyToThreads((threadHandle, index) =>
            {
                var expected = groups[index % groups.Count];
                if (!NativeMethods.SetThreadGroupAffinity(threadHandle, in expected, IntPtr.Zero))
                {
                    return false;
                }

                return NativeMethods.GetThreadGroupAffinity(threadHandle, out var observed) &&
                    observed.Group == expected.Group &&
                    observed.Mask == expected.Mask;
            });
        }

        internal static ThreadCpuSetProbeResult ProbeSelectedCpuSets(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var probes = new List<ThreadCpuSetProbe>();
                foreach (ProcessThread thread in process.Threads)
                {
                    probes.Add(ProbeThread(thread.Id));
                }

                return new ThreadCpuSetProbeResult(true, null, probes);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                var error = ex is Win32Exception win32 ? win32.NativeErrorCode : 0;
                return new ThreadCpuSetProbeResult(false, error == 0 ? null : error, []);
            }
        }

        internal static ThreadCpuSetProbe ProbeThread(int threadId)
        {
            using var handle = NativeMethods.OpenThread(ThreadQueryLimitedInformation, false, (uint)threadId);
            if (handle.IsInvalid)
            {
                return ThreadCpuSetProbe.OpenFailed(threadId, Marshal.GetLastWin32Error());
            }

            var affinity = ReadAffinity(handle);
            var ideal = ReadIdeal(handle);
            if (!NativeMethods.GetThreadSelectedCpuSets(handle, null, 0, out var required))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 122 || required == 0)
                {
                    return ThreadCpuSetProbe.QueryFailed(threadId, error, ideal, affinity);
                }
            }

            if (required == 0)
            {
                return ThreadCpuSetProbe.NoOverride(threadId, ideal, affinity);
            }

            var ids = new uint[required];
            if (!NativeMethods.GetThreadSelectedCpuSets(handle, ids, required, out var filled))
            {
                return ThreadCpuSetProbe.QueryFailed(threadId, Marshal.GetLastWin32Error(), ideal, affinity);
            }

            return ThreadCpuSetProbe.Override(threadId, ids.Take((int)filled).ToArray(), ideal, affinity);
        }

        public IReadOnlyList<int>? GetIdealProcessorIndexes()
        {
            try
            {
                using var process = Process.GetProcessById(this.processId);
                var indexes = new HashSet<int>();
                foreach (var threadId in process.Threads.Cast<ProcessThread>().Select(thread => thread.Id))
                {
                    using var handle = NativeMethods.OpenThread(ThreadQueryLimitedInformation, false, (uint)threadId);
                    if (handle.IsInvalid)
                    {
                        if (Marshal.GetLastWin32Error() == ErrorInvalidParameter)
                        {
                            continue;
                        }

                        return null;
                    }

                    if (!NativeMethods.GetThreadIdealProcessorEx(handle, out var processor))
                    {
                        if (Marshal.GetLastWin32Error() == ErrorInvalidParameter)
                        {
                            continue;
                        }

                        return null;
                    }

                    indexes.Add((processor.Group * 64) + processor.Number);
                }

                return indexes.Count == 0 ? null : indexes.OrderBy(index => index).ToList();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                return null;
            }
        }

        internal static IReadOnlyList<GroupAffinity> BuildGroupAffinities(IEnumerable<ProcessorRef> processors) =>
            processors
                .Distinct()
                .GroupBy(processor => processor.Group)
                .OrderBy(group => group.Key)
                .Select(group => new GroupAffinity
                {
                    Group = group.Key,
                    Mask = (nuint)group.Aggregate(0UL, (mask, processor) => mask | (1UL << processor.LogicalProcessorNumber)),
                })
                .Where(group => group.Mask != 0)
                .ToList();

        internal static IReadOnlyList<ProcessorRef> BuildIdealProcessorSequence(
            IEnumerable<ProcessorRef> processors,
            int threadCount)
        {
            var ordered = processors
                .Distinct()
                .OrderBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();
            return ordered.Count == 0 || threadCount <= 0
                ? []
                : Enumerable.Range(0, threadCount).Select(index => ordered[index % ordered.Count]).ToList();
        }

        private ThreadCpuAssignmentResult ApplyToThreads(Func<SafeWaitHandle, int, bool> apply)
        {
            var applied = 0;
            try
            {
                using var process = Process.GetProcessById(this.processId);
                var threadIds = process.Threads.Cast<ProcessThread>()
                    .Select(thread => thread.Id)
                    .OrderBy(id => id)
                    .ToList();

                for (var index = 0; index < threadIds.Count; index++)
                {
                    using var handle = NativeMethods.OpenThread(
                        ThreadSetInformation | ThreadQueryLimitedInformation,
                        false,
                        (uint)threadIds[index]);
                    if (handle.IsInvalid)
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ErrorInvalidParameter)
                        {
                            continue;
                        }

                        return ThreadCpuAssignmentResult.Failed(
                            new Win32Exception(error).Message,
                            applied,
                            error == ErrorAccessDenied);
                    }

                    if (!apply(handle, index))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ErrorInvalidParameter)
                        {
                            continue;
                        }

                        return ThreadCpuAssignmentResult.Failed(
                            error == 0 ? "Windows did not verify the requested thread CPU assignment." : new Win32Exception(error).Message,
                            applied,
                            error == ErrorAccessDenied);
                    }

                    applied++;
                }
            }
            catch (ArgumentException)
            {
                return ThreadCpuAssignmentResult.Failed("The process exited before its threads could be configured.", applied);
            }
            catch (InvalidOperationException)
            {
                return ThreadCpuAssignmentResult.Failed("The process exited before its threads could be configured.", applied);
            }
            catch (Win32Exception ex)
            {
                return ThreadCpuAssignmentResult.Failed(ex.Message, applied, ex.NativeErrorCode == ErrorAccessDenied);
            }

            return applied == 0
                ? ThreadCpuAssignmentResult.Failed("No live process threads were available.")
                : ThreadCpuAssignmentResult.Succeeded(applied);
        }

        private static ProcessorRef? ReadIdeal(SafeWaitHandle handle)
        {
            if (!NativeMethods.GetThreadIdealProcessorEx(handle, out var processor))
            {
                return null;
            }

            return new ProcessorRef(processor.Group, processor.Number, (processor.Group * 64) + processor.Number);
        }

        private static (bool Read, ushort Group, ulong Mask) ReadAffinity(SafeWaitHandle handle)
        {
            if (!NativeMethods.GetThreadGroupAffinity(handle, out var affinity))
            {
                return (false, 0, 0);
            }

            return (true, affinity.Group, (ulong)affinity.Mask);
        }

        private static ProcessorNumber ToProcessorNumber(ProcessorRef processor) =>
            new()
            {
                Group = processor.Group,
                Number = processor.LogicalProcessorNumber,
            };

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessorNumber
        {
            public ushort Group;
            public byte Number;
            public byte Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GroupAffinity
        {
            public nuint Mask;
            public ushort Group;
            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        }

        private static partial class NativeMethods
        {
            [LibraryImport("kernel32.dll", SetLastError = true)]
            internal static partial SafeWaitHandle OpenThread(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint threadId);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool SetThreadIdealProcessorEx(SafeWaitHandle thread, in ProcessorNumber idealProcessor, IntPtr previousIdealProcessor);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool GetThreadIdealProcessorEx(SafeWaitHandle thread, out ProcessorNumber idealProcessor);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool SetThreadGroupAffinity(SafeWaitHandle thread, in GroupAffinity groupAffinity, IntPtr previousGroupAffinity);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool GetThreadGroupAffinity(SafeWaitHandle thread, out GroupAffinity groupAffinity);

            [LibraryImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool GetThreadSelectedCpuSets(
                SafeWaitHandle thread,
                uint[]? cpuSetIds,
                uint cpuSetIdCount,
                out uint requiredIdCount);
        }
    }

    internal sealed record ThreadCpuAssignmentResult(bool Success, int AppliedThreadCount, bool IsAccessDenied, string Error)
    {
        public static ThreadCpuAssignmentResult Succeeded(int count) => new(true, count, false, string.Empty);

        public static ThreadCpuAssignmentResult Failed(string error, int count = 0, bool accessDenied = false) =>
            new(false, count, accessDenied, error);
    }
}
