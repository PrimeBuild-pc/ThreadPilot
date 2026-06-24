namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Models;

    public sealed class WindowsCpuTopologyProvider : ICpuTopologyProvider
    {
        private const int ErrorInsufficientBuffer = 122;

        private readonly ILogger<WindowsCpuTopologyProvider> logger;

        public WindowsCpuTopologyProvider(ILogger<WindowsCpuTopologyProvider>? logger = null)
        {
            this.logger = logger ?? NullLogger<WindowsCpuTopologyProvider>.Instance;
        }

        private enum CpuSetInformationType
        {
            CpuSetInformation = 0,
        }

        private enum LogicalProcessorRelationship
        {
            RelationProcessorCore = 0,
            RelationNumaNode = 1,
            RelationCache = 2,
            RelationProcessorPackage = 3,
            RelationGroup = 4,
            RelationProcessorDie = 5,
            RelationNumaNodeEx = 6,
            RelationProcessorModule = 7,
            RelationAll = 0xFFFF,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemCpuSetInformation
        {
            public uint Size;
            public CpuSetInformationType Type;
            public uint Id;
            public ushort Group;
            public byte LogicalProcessorIndex;
            public byte CoreIndex;
            public byte LastLevelCacheIndex;
            public byte NumaNodeIndex;
            public byte EfficiencyClass;
            public byte AllFlags;
            public uint SchedulingClassOrReserved;
            public ulong AllocationTag;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemLogicalProcessorInformationExHeader
        {
            public LogicalProcessorRelationship Relationship;
            public int Size;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemCpuSetInformation(
            IntPtr information,
            uint bufferLength,
            out uint returnedLength,
            IntPtr process,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(
            LogicalProcessorRelationship relationshipType,
            IntPtr buffer,
            ref int returnedLength);

        public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.CreateSnapshot(cancellationToken));
        }

        private CpuTopologySnapshot CreateSnapshot(CancellationToken cancellationToken)
        {
            var logicalProcessors = new HashSet<ProcessorRef>();
            var cpuSetIds = new Dictionary<ProcessorRef, uint>();
            var efficiencyClasses = new Dictionary<ProcessorRef, byte>();
            var coreIndexes = new Dictionary<ProcessorRef, int>();
            var numaNodeIndexes = new Dictionary<ProcessorRef, int>();
            var lastLevelCacheIndexes = new Dictionary<ProcessorRef, int>();
            var packageIndexes = new Dictionary<ProcessorRef, int>();
            var smtSiblingGlobalIndexes = new Dictionary<ProcessorRef, IReadOnlyList<int>>();

            this.ReadCpuSetInformation(
                logicalProcessors,
                cpuSetIds,
                efficiencyClasses,
                coreIndexes,
                numaNodeIndexes,
                lastLevelCacheIndexes,
                cancellationToken);

            this.ReadLogicalProcessorRelationships(
                logicalProcessors,
                efficiencyClasses,
                coreIndexes,
                numaNodeIndexes,
                lastLevelCacheIndexes,
                packageIndexes,
                smtSiblingGlobalIndexes,
                cancellationToken);

            if (efficiencyClasses.Values.Distinct().Count() <= 1)
            {
                efficiencyClasses.Clear();
            }

            if (logicalProcessors.Count == 0)
            {
                this.logger.LogWarning("CPU topology provider could not read Windows topology APIs; using Environment.ProcessorCount fallback");
                foreach (var processor in WindowsCpuTopologyNativeLayout.CreateFallbackProcessors(Environment.ProcessorCount))
                {
                    logicalProcessors.Add(processor);
                    coreIndexes[processor] = processor.GlobalIndex;
                }
            }

            var processors = logicalProcessors
                .OrderBy(processor => processor.GlobalIndex)
                .ThenBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();

            var signature = new CpuTopologySignature
            {
                LogicalProcessorCount = processors.Count,
                PhysicalCoreCount = coreIndexes.Count == 0
                    ? processors.Count
                    : coreIndexes.Values.Distinct().Count(),
                ProcessorGroupCount = processors.Select(processor => processor.Group).Distinct().Count(),
                NumaNodeCount = numaNodeIndexes.Values.Distinct().Count(),
                LastLevelCacheGroupCount = lastLevelCacheIndexes.Values.Distinct().Count(),
                PackageCount = packageIndexes.Values.Distinct().Count(),
                Source = nameof(WindowsCpuTopologyProvider),
            };

            return CpuTopologySnapshot.Create(
                processors,
                cpuSetIds,
                efficiencyClasses,
                signature,
                coreIndexes,
                numaNodeIndexes,
                lastLevelCacheIndexes,
                packageIndexes,
                smtSiblingGlobalIndexes);
        }

        private void ReadCpuSetInformation(
            HashSet<ProcessorRef> logicalProcessors,
            IDictionary<ProcessorRef, uint> cpuSetIds,
            IDictionary<ProcessorRef, byte> efficiencyClasses,
            IDictionary<ProcessorRef, int> coreIndexes,
            IDictionary<ProcessorRef, int> numaNodeIndexes,
            IDictionary<ProcessorRef, int> lastLevelCacheIndexes,
            CancellationToken cancellationToken)
        {
            uint requiredLength = 0;
            if (GetSystemCpuSetInformation(IntPtr.Zero, 0, out requiredLength, IntPtr.Zero, 0))
            {
                return;
            }

            var firstError = Marshal.GetLastWin32Error();
            if (firstError != ErrorInsufficientBuffer || requiredLength == 0)
            {
                this.logger.LogDebug("GetSystemCpuSetInformation probe failed with Win32 error {Error}", firstError);
                return;
            }

            var buffer = Marshal.AllocHGlobal((int)requiredLength);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GetSystemCpuSetInformation(buffer, requiredLength, out requiredLength, IntPtr.Zero, 0))
                {
                    this.logger.LogDebug("GetSystemCpuSetInformation read failed with Win32 error {Error}", Marshal.GetLastWin32Error());
                    return;
                }

                var offset = 0;
                while (offset < requiredLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var itemPtr = IntPtr.Add(buffer, offset);
                    var info = Marshal.PtrToStructure<SystemCpuSetInformation>(itemPtr);
                    if (info.Size == 0)
                    {
                        break;
                    }

                    if (info.Type == CpuSetInformationType.CpuSetInformation)
                    {
                        var processor = WindowsCpuTopologyNativeLayout.CreateProcessorRef(info.Group, info.LogicalProcessorIndex);
                        logicalProcessors.Add(processor);
                        cpuSetIds[processor] = info.Id;
                        efficiencyClasses[processor] = info.EfficiencyClass;
                        coreIndexes.TryAdd(processor, info.CoreIndex);
                        numaNodeIndexes[processor] = info.NumaNodeIndex;
                        lastLevelCacheIndexes[processor] = info.LastLevelCacheIndex;
                    }

                    offset += (int)info.Size;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ReadLogicalProcessorRelationships(
            HashSet<ProcessorRef> logicalProcessors,
            IDictionary<ProcessorRef, byte> efficiencyClasses,
            IDictionary<ProcessorRef, int> coreIndexes,
            IDictionary<ProcessorRef, int> numaNodeIndexes,
            IDictionary<ProcessorRef, int> lastLevelCacheIndexes,
            IDictionary<ProcessorRef, int> packageIndexes,
            IDictionary<ProcessorRef, IReadOnlyList<int>> smtSiblingGlobalIndexes,
            CancellationToken cancellationToken)
        {
            var requiredLength = 0;
            if (GetLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationAll, IntPtr.Zero, ref requiredLength))
            {
                return;
            }

            var firstError = Marshal.GetLastWin32Error();
            if (firstError != ErrorInsufficientBuffer || requiredLength <= 0)
            {
                this.logger.LogDebug("GetLogicalProcessorInformationEx probe failed with Win32 error {Error}", firstError);
                return;
            }

            var buffer = Marshal.AllocHGlobal(requiredLength);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GetLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationAll, buffer, ref requiredLength))
                {
                    this.logger.LogDebug("GetLogicalProcessorInformationEx read failed with Win32 error {Error}", Marshal.GetLastWin32Error());
                    return;
                }

                var coreIndex = 0;
                var packageIndex = 0;
                var lastLevelCacheIndex = 0;
                var offset = 0;
                while (offset < requiredLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var itemPtr = IntPtr.Add(buffer, offset);
                    var header = Marshal.PtrToStructure<SystemLogicalProcessorInformationExHeader>(itemPtr);
                    if (header.Size <= 0)
                    {
                        break;
                    }

                    switch (header.Relationship)
                    {
                        case LogicalProcessorRelationship.RelationProcessorCore:
                            this.ReadProcessorCoreRelationship(
                                itemPtr,
                                coreIndex++,
                                logicalProcessors,
                                efficiencyClasses,
                                coreIndexes,
                                smtSiblingGlobalIndexes);
                            break;
                        case LogicalProcessorRelationship.RelationProcessorPackage:
                            this.ReadIndexedProcessorRelationship(
                                itemPtr,
                                packageIndex++,
                                logicalProcessors,
                                packageIndexes);
                            break;
                        case LogicalProcessorRelationship.RelationCache:
                            if (TryReadL3CacheProcessors(itemPtr, out var cacheProcessors))
                            {
                                foreach (var processor in cacheProcessors)
                                {
                                    logicalProcessors.Add(processor);
                                    lastLevelCacheIndexes[processor] = lastLevelCacheIndex;
                                }

                                lastLevelCacheIndex++;
                            }

                            break;
                        case LogicalProcessorRelationship.RelationNumaNode:
                        case LogicalProcessorRelationship.RelationNumaNodeEx:
                            this.ReadNumaNodeRelationship(itemPtr, logicalProcessors, numaNodeIndexes);
                            break;
                    }

                    offset += header.Size;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ReadProcessorCoreRelationship(
            IntPtr itemPtr,
            int coreIndex,
            HashSet<ProcessorRef> logicalProcessors,
            IDictionary<ProcessorRef, byte> efficiencyClasses,
            IDictionary<ProcessorRef, int> coreIndexes,
            IDictionary<ProcessorRef, IReadOnlyList<int>> smtSiblingGlobalIndexes)
        {
            var relationshipPtr = IntPtr.Add(itemPtr, 8);
            var processor = Marshal.PtrToStructure<WindowsCpuTopologyNativeLayout.ProcessorRelationship>(relationshipPtr);
            var processorsInCore = WindowsCpuTopologyNativeLayout
                .ReadProcessorRelationshipProcessors(relationshipPtr, processor.GroupCount)
                .ToList();
            var siblingIndexes = processorsInCore.Select(item => item.GlobalIndex).ToList();

            foreach (var logicalProcessor in processorsInCore)
            {
                logicalProcessors.Add(logicalProcessor);
                efficiencyClasses[logicalProcessor] = processor.EfficiencyClass;
                coreIndexes[logicalProcessor] = coreIndex;
                smtSiblingGlobalIndexes[logicalProcessor] = siblingIndexes
                    .Where(index => index != logicalProcessor.GlobalIndex)
                    .OrderBy(index => index)
                    .ToList();
            }
        }

        private void ReadIndexedProcessorRelationship(
            IntPtr itemPtr,
            int index,
            HashSet<ProcessorRef> logicalProcessors,
            IDictionary<ProcessorRef, int> indexMap)
        {
            var relationshipPtr = IntPtr.Add(itemPtr, 8);
            var processor = Marshal.PtrToStructure<WindowsCpuTopologyNativeLayout.ProcessorRelationship>(relationshipPtr);
            foreach (var logicalProcessor in WindowsCpuTopologyNativeLayout.ReadProcessorRelationshipProcessors(relationshipPtr, processor.GroupCount))
            {
                logicalProcessors.Add(logicalProcessor);
                indexMap[logicalProcessor] = index;
            }
        }

        private static bool TryReadL3CacheProcessors(IntPtr itemPtr, out IReadOnlyList<ProcessorRef> processors)
        {
            return WindowsCpuTopologyNativeLayout.TryReadL3CacheProcessors(IntPtr.Add(itemPtr, 8), out processors);
        }

        private void ReadNumaNodeRelationship(
            IntPtr itemPtr,
            HashSet<ProcessorRef> logicalProcessors,
            IDictionary<ProcessorRef, int> numaNodeIndexes)
        {
            var processors = WindowsCpuTopologyNativeLayout.ReadNumaNodeProcessors(IntPtr.Add(itemPtr, 8), out var nodeNumber);
            foreach (var processor in processors)
            {
                logicalProcessors.Add(processor);
                numaNodeIndexes[processor] = nodeNumber;
            }
        }
    }
}
