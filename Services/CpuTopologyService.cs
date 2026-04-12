/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for detecting CPU topology using WMI and Windows APIs
    /// </summary>
    public class CpuTopologyService : ICpuTopologyService
    {
        private readonly ILogger<CpuTopologyService> _logger;
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _detectSemaphore = new(1, 1);
        private CpuTopologyModel? _currentTopology;

        private const string TOPOLOGY_CACHE_KEY = "cpu_topology";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);
        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        public event EventHandler<CpuTopologyDetectedEventArgs>? TopologyDetected;
        public CpuTopologyModel? CurrentTopology => _currentTopology;

        private enum LOGICAL_PROCESSOR_RELATIONSHIP
        {
            RelationProcessorCore = 0,
            RelationNumaNode = 1,
            RelationCache = 2,
            RelationProcessorPackage = 3,
            RelationGroup = 4,
            RelationProcessorDie = 5,
            RelationNumaNodeEx = 6,
            RelationProcessorModule = 7,
            RelationAll = 0xFFFF
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GROUP_AFFINITY
        {
            public UIntPtr Mask;
            public ushort Group;
            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct PROCESSOR_RELATIONSHIP
        {
            public byte Flags;
            public byte EfficiencyClass;
            public fixed byte Reserved[20];
            public ushort GroupCount;
            public GROUP_AFFINITY GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        {
            public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
            public int Size;
            public PROCESSOR_RELATIONSHIP Processor;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(
            LOGICAL_PROCESSOR_RELATIONSHIP relationshipType,
            IntPtr buffer,
            ref int returnedLength);

        public CpuTopologyService(ILogger<CpuTopologyService> logger, IMemoryCache? cache = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = 10,
                CompactionPercentage = 0.1
            });
        }

        public async Task<CpuTopologyModel> DetectTopologyAsync()
        {
            // PERFORMANCE IMPROVEMENT: Check cache first to avoid expensive WMI calls
            if (_cache.TryGetValue(TOPOLOGY_CACHE_KEY, out CpuTopologyModel? cachedTopology) && cachedTopology != null)
            {
                _logger.LogInformation("CPU topology retrieved from cache");
                _currentTopology = cachedTopology;
                return cachedTopology;
            }

            await _detectSemaphore.WaitAsync();

            try
            {
                // Re-check cache after entering the critical section
                if (_cache.TryGetValue(TOPOLOGY_CACHE_KEY, out cachedTopology) && cachedTopology != null)
                {
                    _logger.LogInformation("CPU topology retrieved from cache after synchronization");
                    _currentTopology = cachedTopology;
                    return cachedTopology;
                }

                _logger.LogInformation("Starting CPU topology detection (cache miss)");
                
                var topology = new CpuTopologyModel();
                
                // Get basic system information
                await DetectBasicCpuInfoAsync(topology);
                
                // Detect logical cores using multiple methods
                await DetectLogicalCoresAsync(topology);
                
                // Try to detect advanced topology (CCD, P/E cores, etc.)
                await DetectAdvancedTopologyAsync(topology);
                
                // Validate and finalize topology
                ValidateTopology(topology);
                
                _currentTopology = topology;
                topology.TopologyDetectionSuccessful = true;

                // PERFORMANCE IMPROVEMENT: Cache the topology to avoid expensive WMI calls
                _cache.Set(
                    TOPOLOGY_CACHE_KEY,
                    topology,
                    new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(CACHE_DURATION)
                        .SetSize(1));

                _logger.LogInformation("CPU topology detection completed successfully and cached. " +
                    "Logical CPUs: {LogicalCores}, Physical CPUs: {PhysicalCores}, " +
                    "Sockets: {Sockets}, HT: {HasHT}, Hybrid: {HasHybrid}, CCD: {HasCcd}",
                    topology.TotalLogicalCores, topology.TotalPhysicalCores, topology.TotalSockets,
                    topology.HasHyperThreading, topology.HasIntelHybrid, topology.HasAmdCcd);

                TopologyDetected?.Invoke(this, new CpuTopologyDetectedEventArgs(topology, true));
                return topology;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect CPU topology");
                
                // Create fallback topology
                var fallbackTopology = CreateFallbackTopology();
                _currentTopology = fallbackTopology;
                
                TopologyDetected?.Invoke(this, new CpuTopologyDetectedEventArgs(fallbackTopology, false, ex.Message));
                return fallbackTopology;
            }
            finally
            {
                _detectSemaphore.Release();
            }
        }

        private async Task DetectBasicCpuInfoAsync(CpuTopologyModel topology)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                using var collection = searcher.Get();
                
                foreach (ManagementObject processor in collection)
                {
                    topology.CpuBrand = processor["Name"]?.ToString() ?? "Unknown";
                    topology.CpuArchitecture = processor["Architecture"]?.ToString() ?? "Unknown";
                    break; // Take first processor for basic info
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect basic CPU info via WMI");
            }
        }

        private async Task DetectLogicalCoresAsync(CpuTopologyModel topology)
        {
            try
            {
                // Method 1: Use official Windows topology API for physical/logical CPU mapping.
                if (TryDetectCoresViaWindowsApi(topology))
                {
                    return;
                }

                // Method 2: Use Environment.ProcessorCount as baseline
                int logicalCoreCount = Environment.ProcessorCount;
                
                // Method 3: Try WMI for more detailed information
                await DetectCoresViaWmiAsync(topology);
                
                // If WMI failed, create basic topology
                if (topology.LogicalCores.Count == 0)
                {
                    CreateBasicTopology(topology, logicalCoreCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect logical cores, using fallback");
                CreateBasicTopology(topology, Environment.ProcessorCount);
            }
        }

        private bool TryDetectCoresViaWindowsApi(CpuTopologyModel topology)
        {
            try
            {
                int requiredLength = 0;
                if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref requiredLength))
                {
                    // Expected first call should fail with insufficient buffer.
                    return false;
                }

                int firstError = Marshal.GetLastWin32Error();
                if (firstError != ERROR_INSUFFICIENT_BUFFER || requiredLength <= 0)
                {
                    _logger.LogWarning("GetLogicalProcessorInformationEx probe failed with Win32 error {Error}", firstError);
                    return false;
                }

                IntPtr buffer = Marshal.AllocHGlobal(requiredLength);
                try
                {
                    if (!GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, buffer, ref requiredLength))
                    {
                        _logger.LogWarning("GetLogicalProcessorInformationEx read failed with Win32 error {Error}", Marshal.GetLastWin32Error());
                        return false;
                    }

                    var discovered = new List<(int PhysicalCpuId, int LogicalCpuId, byte EfficiencyClass)>();
                    int offset = 0;
                    int physicalCpuId = 0;

                    while (offset < requiredLength)
                    {
                        IntPtr itemPtr = IntPtr.Add(buffer, offset);
                        var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX>(itemPtr);

                        if (info.Size <= 0)
                        {
                            break;
                        }

                        if (info.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                        {
                            var processor = info.Processor;
                            int groupCount = processor.GroupCount;
                            int groupMaskOffset = Marshal.OffsetOf<PROCESSOR_RELATIONSHIP>(nameof(PROCESSOR_RELATIONSHIP.GroupMask)).ToInt32();
                            IntPtr groupMaskPtr = IntPtr.Add(itemPtr, 8 + groupMaskOffset);

                            var logicalCpuIdsForCore = new List<int>();

                            for (int g = 0; g < groupCount; g++)
                            {
                                int stride = Marshal.SizeOf<GROUP_AFFINITY>();
                                var groupAffinity = Marshal.PtrToStructure<GROUP_AFFINITY>(IntPtr.Add(groupMaskPtr, g * stride));

                                // This app currently represents affinity with a single 64-bit mask.
                                if (groupAffinity.Group != 0)
                                {
                                    _logger.LogWarning("Detected processor group {Group}; falling back to WMI/core-count topology path", groupAffinity.Group);
                                    return false;
                                }

                                ulong mask = groupAffinity.Mask.ToUInt64();
                                logicalCpuIdsForCore.AddRange(GetSetBitIndices(mask));
                            }

                            foreach (int logicalCpuId in logicalCpuIdsForCore.Distinct().OrderBy(id => id))
                            {
                                discovered.Add((physicalCpuId, logicalCpuId, processor.EfficiencyClass));
                            }

                            physicalCpuId++;
                        }

                        offset += info.Size;
                    }

                    if (discovered.Count == 0)
                    {
                        return false;
                    }

                    topology.LogicalCores.Clear();
                    foreach (var entry in discovered.OrderBy(d => d.LogicalCpuId))
                    {
                        topology.LogicalCores.Add(new CpuCoreModel
                        {
                            LogicalCoreId = entry.LogicalCpuId,
                            PhysicalCoreId = entry.PhysicalCpuId,
                            SocketId = 0,
                            CoreType = CpuCoreType.Standard,
                            Label = $"CPU {entry.LogicalCpuId}",
                            LogicalProcessorName = $"CPU{entry.PhysicalCpuId}_T0",
                            IsEnabled = true
                        });
                    }

                    ApplyHyperThreadingFromPhysicalMapping(topology);
                    ApplyCoreTypeFromEfficiencyClass(topology, discovered);

                    _logger.LogInformation("Detected CPU topology via GetLogicalProcessorInformationEx: {LogicalCpuCount} logical CPUs, {PhysicalCpuCount} physical CPUs",
                        topology.TotalLogicalCores,
                        topology.TotalPhysicalCores);

                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetLogicalProcessorInformationEx topology detection failed");
                return false;
            }
        }

        private static IEnumerable<int> GetSetBitIndices(ulong mask)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                if ((mask & (1UL << bit)) != 0)
                {
                    yield return bit;
                }
            }
        }

        private void ApplyHyperThreadingFromPhysicalMapping(CpuTopologyModel topology)
        {
            foreach (var coreGroup in topology.LogicalCores.GroupBy(c => c.PhysicalCoreId))
            {
                var siblings = coreGroup.OrderBy(c => c.LogicalCoreId).ToList();
                if (siblings.Count <= 1)
                {
                    continue;
                }

                for (int i = 0; i < siblings.Count; i++)
                {
                    var isLogicalSibling = i > 0;
                    siblings[i].IsHyperThreaded = isLogicalSibling;
                    siblings[i].HyperThreadSibling = siblings.Count >= 2
                        ? (isLogicalSibling ? siblings[0].LogicalCoreId : siblings[1].LogicalCoreId)
                        : null;

                    siblings[i].LogicalProcessorName = $"CPU{siblings[i].PhysicalCoreId}_T{i}";
                }
            }
        }

        private void ApplyCoreTypeFromEfficiencyClass(
            CpuTopologyModel topology,
            List<(int PhysicalCpuId, int LogicalCpuId, byte EfficiencyClass)> discovered)
        {
            var byPhysical = discovered
                .GroupBy(d => d.PhysicalCpuId)
                .Select(g => new { PhysicalCpuId = g.Key, EfficiencyClass = g.Min(x => x.EfficiencyClass) })
                .ToList();

            var classes = byPhysical
                .Select(x => x.EfficiencyClass)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            if (classes.Count <= 1)
            {
                return;
            }

            byte performanceClass = classes.Min();
            foreach (var logicalCpu in topology.LogicalCores)
            {
                byte classValue = byPhysical.First(x => x.PhysicalCpuId == logicalCpu.PhysicalCoreId).EfficiencyClass;
                logicalCpu.CoreType = classValue == performanceClass
                    ? CpuCoreType.PerformanceCore
                    : CpuCoreType.EfficiencyCore;
            }
        }

        private async Task DetectCoresViaWmiAsync(CpuTopologyModel topology)
        {
            try
            {
                // First, get physical processor information
                var physicalCoreCount = 0;
                var logicalCoreCount = 0;

                using (var processorSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                using (var processorCollection = processorSearcher.Get())
                {
                    foreach (ManagementObject processor in processorCollection)
                    {
                        var numberOfCores = Convert.ToInt32(processor["NumberOfCores"] ?? 0);
                        var numberOfLogicalProcessors = Convert.ToInt32(processor["NumberOfLogicalProcessors"] ?? 0);

                        physicalCoreCount += numberOfCores;
                        logicalCoreCount += numberOfLogicalProcessors;

                        _logger.LogInformation("Detected CPU: {Cores} physical CPUs, {LogicalProcessors} logical processors",
                            numberOfCores, numberOfLogicalProcessors);
                    }
                }

                // If WMI didn't provide the info, fall back to Environment.ProcessorCount
                if (logicalCoreCount == 0)
                {
                    logicalCoreCount = Environment.ProcessorCount;
                    physicalCoreCount = logicalCoreCount; // Assume no HT if we can't detect
                }

                // Create logical cores with proper physical core mapping
                var hasHyperThreading = logicalCoreCount > physicalCoreCount;
                var threadsPerCore = hasHyperThreading ? logicalCoreCount / physicalCoreCount : 1;

                for (int logicalId = 0; logicalId < logicalCoreCount; logicalId++)
                {
                    var physicalId = logicalId / threadsPerCore;
                    var threadIndexOnCore = logicalId % threadsPerCore;
                    var isHyperThreaded = hasHyperThreading && (threadIndexOnCore != 0);
                    var htSibling = hasHyperThreading ? (threadIndexOnCore == 0 ? logicalId + 1 : logicalId - 1) : (int?)null;

                    var core = new CpuCoreModel
                    {
                        LogicalCoreId = logicalId,
                        PhysicalCoreId = physicalId,
                        SocketId = 0, // Will be refined later
                        Label = $"CPU {logicalId}",
                        LogicalProcessorName = $"CPU{physicalId}_T{threadIndexOnCore}", // T0 = physical, T1+ = SMT
                        IsEnabled = true,
                        IsHyperThreaded = isHyperThreaded,
                        HyperThreadSibling = htSibling
                    };

                    topology.LogicalCores.Add(core);
                }

                _logger.LogInformation("Created topology: {LogicalCores} logical CPUs, {PhysicalCores} physical CPUs, HT: {HasHT}",
                    logicalCoreCount, physicalCoreCount, hasHyperThreading);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WMI logical processor detection failed");
            }
        }

        private void CreateBasicTopology(CpuTopologyModel topology, int logicalCoreCount)
        {
            topology.LogicalCores.Clear();
            
            for (int i = 0; i < logicalCoreCount; i++)
            {
                var core = new CpuCoreModel
                {
                    LogicalCoreId = i,
                    PhysicalCoreId = i, // Assume no HT for basic topology
                    SocketId = 0,
                    CoreType = CpuCoreType.Standard,
                    Label = $"CPU {i}",
                    LogicalProcessorName = $"CPU{i}_T0", // All physical CPUs in basic fallback (no HT detected)
                    IsEnabled = true
                };

                topology.LogicalCores.Add(core);
            }
        }

        private async Task DetectAdvancedTopologyAsync(CpuTopologyModel topology)
        {
            // Try to detect Intel Hybrid (P/E cores)
            await DetectIntelHybridAsync(topology);
            
            // Try to detect AMD CCD information
            await DetectAmdCcdAsync(topology);
            
            // Try to detect HyperThreading
            DetectHyperThreading(topology);
        }

        private async Task DetectIntelHybridAsync(CpuTopologyModel topology)
        {
            try
            {
                // Intel Hybrid detection is complex and requires specific APIs
                // For now, we'll use heuristics based on CPU brand and core count patterns
                if (topology.CpuBrand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                {
                    // Preserve already-detected core type data from official API if present.
                    if (topology.LogicalCores.Any(c => c.CoreType == CpuCoreType.PerformanceCore || c.CoreType == CpuCoreType.EfficiencyCore))
                    {
                        return;
                    }

                    // Check for 12th gen or later Intel processors (Alder Lake+)
                    if (topology.CpuBrand.Contains("12th") || topology.CpuBrand.Contains("13th") || 
                        topology.CpuBrand.Contains("14th") || topology.CpuBrand.Contains("15th"))
                    {
                        // Heuristic: Assume first cores are P-cores, later ones are E-cores
                        // This is a simplified approach - real detection would require CPUID
                        var totalCores = topology.LogicalCores.Count;
                        var estimatedPCores = Math.Min(8, totalCores / 2); // Rough estimate
                        
                        for (int i = 0; i < topology.LogicalCores.Count; i++)
                        {
                            if (i < estimatedPCores * 2) // P-cores with HT
                            {
                                topology.LogicalCores[i].CoreType = CpuCoreType.PerformanceCore;
                            }
                            else
                            {
                                topology.LogicalCores[i].CoreType = CpuCoreType.EfficiencyCore;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect Intel Hybrid topology");
            }
        }

        private async Task DetectAmdCcdAsync(CpuTopologyModel topology)
        {
            try
            {
                if (topology.CpuBrand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                {
                    // AMD CCD detection - improved heuristic
                    // Only assign CCD if we actually have multiple CCDs
                    var totalPhysicalCores = topology.TotalPhysicalCores;
                    var coresPerCcd = 8; // Typical for Zen 2/3/4

                    // Only assign CCD IDs if we have more than 8 physical cores (indicating multiple CCDs)
                    if (totalPhysicalCores > coresPerCcd)
                    {
                        for (int i = 0; i < topology.LogicalCores.Count; i++)
                        {
                            var physicalCoreId = topology.LogicalCores[i].PhysicalCoreId;
                            topology.LogicalCores[i].CcdId = physicalCoreId / coresPerCcd;
                            topology.LogicalCores[i].CoreType = CpuCoreType.Zen3; // Default assumption
                        }

                        _logger.LogInformation("Detected AMD multi-CCD configuration: {PhysicalCores} physical cores, estimated {CcdCount} CCDs",
                            totalPhysicalCores, (totalPhysicalCores + coresPerCcd - 1) / coresPerCcd);
                    }
                    else
                    {
                        // Single CCD or small core count - don't assign CCD IDs
                        foreach (var core in topology.LogicalCores)
                        {
                            core.CoreType = CpuCoreType.Zen3; // Default assumption
                        }

                        _logger.LogInformation("Detected AMD single-CCD configuration: {PhysicalCores} physical cores", totalPhysicalCores);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect AMD CCD topology");
            }
        }

        private void DetectHyperThreading(CpuTopologyModel topology)
        {
            try
            {
                // Normalize HT metadata by physical CPU mapping first.
                var groupedByPhysical = topology.LogicalCores
                    .GroupBy(c => c.PhysicalCoreId)
                    .Select(g => g.OrderBy(c => c.LogicalCoreId).ToList())
                    .ToList();

                if (groupedByPhysical.Any(g => g.Count > 1))
                {
                    foreach (var siblings in groupedByPhysical)
                    {
                        for (int i = 0; i < siblings.Count; i++)
                        {
                            var isLogicalSibling = i > 0;
                            siblings[i].IsHyperThreaded = isLogicalSibling;
                            siblings[i].HyperThreadSibling = siblings.Count >= 2
                                ? (isLogicalSibling ? siblings[0].LogicalCoreId : siblings[1].LogicalCoreId)
                                : null;

                            if (string.IsNullOrWhiteSpace(siblings[i].LogicalProcessorName))
                            {
                                siblings[i].LogicalProcessorName = $"CPU{siblings[i].PhysicalCoreId}_T{i}";
                            }
                        }
                    }

                    return;
                }

                // Fallback HT detection: if we only have flat sequential data.
                var logicalCount = topology.LogicalCores.Count;
                var physicalCount = topology.TotalPhysicalCores;
                
                if (logicalCount > physicalCount)
                {
                    // Mark pairs as primary/logical siblings conservatively.
                    for (int i = 0; i < topology.LogicalCores.Count; i += 2)
                    {
                        if (i + 1 < topology.LogicalCores.Count)
                        {
                            topology.LogicalCores[i].IsHyperThreaded = false;
                            topology.LogicalCores[i].HyperThreadSibling = i + 1;
                            topology.LogicalCores[i + 1].IsHyperThreaded = true;
                            topology.LogicalCores[i + 1].HyperThreadSibling = i;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect HyperThreading");
            }
        }

        private void ValidateTopology(CpuTopologyModel topology)
        {
            // Ensure we have at least one core
            if (topology.LogicalCores.Count == 0)
            {
                CreateBasicTopology(topology, Environment.ProcessorCount);
            }

            if (string.IsNullOrWhiteSpace(topology.CpuBrand))
            {
                topology.CpuBrand = "Unknown";
            }

            if (string.IsNullOrWhiteSpace(topology.CpuArchitecture))
            {
                topology.CpuArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
            }
            
            // Ensure logical core IDs are sequential
            for (int i = 0; i < topology.LogicalCores.Count; i++)
            {
                topology.LogicalCores[i].LogicalCoreId = i;
            }

            // Normalize physical core IDs to avoid invalid/negative mappings
            var normalizedPhysicalCoreIds = topology.LogicalCores
                .Select((core, index) => new
                {
                    core,
                    physical = core.PhysicalCoreId >= 0 ? core.PhysicalCoreId : index
                })
                .GroupBy(x => x.physical)
                .OrderBy(g => g.Key)
                .Select((group, normalizedId) => new { group, normalizedId })
                .ToList();

            foreach (var item in normalizedPhysicalCoreIds)
            {
                foreach (var entry in item.group)
                {
                    entry.core.PhysicalCoreId = item.normalizedId;
                }
            }

            // Update labels with explicit logical-to-physical CPU mapping.
            foreach (var core in topology.LogicalCores)
            {
                var typeLabel = core.CoreType switch
                {
                    CpuCoreType.PerformanceCore => "P-",
                    CpuCoreType.EfficiencyCore => "E-",
                    _ => ""
                };

                var threadIndex = GetThreadIndexOnPhysicalCpu(core, topology);
                var roleLabel = threadIndex == 0
                    ? $"PH{core.PhysicalCoreId}"
                    : $"L{threadIndex}/PH{core.PhysicalCoreId}";

                core.Label = $"{typeLabel}CPU {core.LogicalCoreId} ({roleLabel})";

                if (string.IsNullOrWhiteSpace(core.LogicalProcessorName))
                {
                    threadIndex = Math.Max(0, core.LogicalCoreId - core.PhysicalCoreId);
                    core.LogicalProcessorName = $"CPU{core.PhysicalCoreId}_T{threadIndex}";
                }
            }
        }

        private static int GetThreadIndexOnPhysicalCpu(CpuCoreModel core, CpuTopologyModel topology)
        {
            if (!string.IsNullOrWhiteSpace(core.LogicalProcessorName))
            {
                var marker = core.LogicalProcessorName.LastIndexOf("_T", StringComparison.Ordinal);
                if (marker >= 0)
                {
                    var suffix = core.LogicalProcessorName[(marker + 2)..];
                    if (int.TryParse(suffix, out int parsedIndex))
                    {
                        return Math.Max(0, parsedIndex);
                    }
                }
            }

            var orderedSiblings = topology.LogicalCores
                .Where(c => c.PhysicalCoreId == core.PhysicalCoreId)
                .OrderBy(c => c.LogicalCoreId)
                .ToList();

            var index = orderedSiblings.FindIndex(c => c.LogicalCoreId == core.LogicalCoreId);
            return index >= 0 ? index : 0;
        }

        private CpuTopologyModel CreateFallbackTopology()
        {
            var topology = new CpuTopologyModel();
            CreateBasicTopology(topology, Environment.ProcessorCount);
            topology.TopologyDetectionSuccessful = false;
            return topology;
        }

        private long CalculateFullAffinityMask(int logicalCoreCount)
        {
            // Affinity masks are represented as signed 64-bit values in this application.
            // For 63+ logical cores, use all available bits to avoid undefined shifts.
            return logicalCoreCount >= 63
                ? -1L
                : (1L << logicalCoreCount) - 1;
        }

        public IEnumerable<CpuAffinityPreset> GetAffinityPresets()
        {
            if (_currentTopology == null)
                return Enumerable.Empty<CpuAffinityPreset>();

            var presets = new List<CpuAffinityPreset>();

            // All CPUs preset
            presets.Add(new CpuAffinityPreset
            {
                Name = "All CPUs",
                Description = $"All {_currentTopology.TotalLogicalCores} logical CPUs",
                AffinityMask = CalculateFullAffinityMask(_currentTopology.TotalLogicalCores),
                IsAvailable = true
            });

            // Physical CPUs only (if HT is available)
            if (_currentTopology.HasHyperThreading)
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "No HT",
                    Description = $"All {_currentTopology.TotalPhysicalCores} physical CPUs (no Hyper-Threading)",
                    AffinityMask = _currentTopology.GetPhysicalCoresAffinityMask(),
                    IsAvailable = _currentTopology.GetPhysicalCoresAffinityMask() != 0
                });
            }

            // Performance CPUs (Intel Hybrid)
            if (_currentTopology.HasIntelHybrid && _currentTopology.PerformanceCores.Any())
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "Performance CPUs",
                    Description = $"Intel P-CPUs ({_currentTopology.PerformanceCores.Count()} logical CPUs)",
                    AffinityMask = _currentTopology.GetPerformanceCoresAffinityMask(),
                    IsAvailable = _currentTopology.GetPerformanceCoresAffinityMask() != 0
                });
            }

            // Efficiency CPUs (Intel Hybrid)
            if (_currentTopology.HasIntelHybrid && _currentTopology.EfficiencyCores.Any())
            {
                presets.Add(new CpuAffinityPreset
                {
                    Name = "Efficiency CPUs",
                    Description = $"Intel E-CPUs ({_currentTopology.EfficiencyCores.Count()} logical CPUs)",
                    AffinityMask = _currentTopology.GetEfficiencyCoresAffinityMask(),
                    IsAvailable = _currentTopology.GetEfficiencyCoresAffinityMask() != 0
                });
            }

            // CCD presets (AMD)
            if (_currentTopology.HasAmdCcd)
            {
                foreach (var ccdId in _currentTopology.AvailableCcds)
                {
                    var ccdCores = _currentTopology.GetCoresByCcd(ccdId);
                    presets.Add(new CpuAffinityPreset
                    {
                        Name = $"CCD {ccdId}",
                        Description = $"AMD CCD {ccdId} ({ccdCores.Count()} logical CPUs)",
                        AffinityMask = _currentTopology.GetCcdAffinityMask(ccdId),
                        IsAvailable = _currentTopology.GetCcdAffinityMask(ccdId) != 0
                    });
                }
            }

            return presets;
        }

        public bool IsAffinityMaskValid(long affinityMask)
        {
            if (_currentTopology == null) return false;

            // Long-based affinity masks cannot represent cores beyond bit 62 explicitly.
            // Accept any non-zero mask for large-core systems and let runtime APIs enforce final validity.
            if (_currentTopology.TotalLogicalCores >= 63)
            {
                return affinityMask != 0;
            }

            var maxMask = CalculateFullAffinityMask(_currentTopology.TotalLogicalCores);
            return affinityMask > 0 && affinityMask <= maxMask;
        }

        public int GetMaxLogicalCores()
        {
            return _currentTopology?.TotalLogicalCores ?? Environment.ProcessorCount;
        }

        public async Task RefreshTopologyAsync()
        {
            _cache.Remove(TOPOLOGY_CACHE_KEY);
            await DetectTopologyAsync();
        }
    }
}

