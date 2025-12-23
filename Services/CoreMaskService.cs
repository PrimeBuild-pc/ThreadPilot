/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing CPU core affinity masks
    /// Based on CPUSetSetter's AppConfig and LogicalProcessorMask system
    /// </summary>
    public class CoreMaskService : ICoreMaskService
    {
        private readonly ILogger<CoreMaskService> _logger;
        private readonly ICpuTopologyService _cpuTopologyService;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _masksFilePath;
        private bool _initialized = false;
        
        // Tracks which masks are actively applied to processes
        private readonly Dictionary<int, string> _activeProcessMasks = new(); // ProcessId -> MaskId

        public ObservableCollection<CoreMask> AvailableMasks { get; private set; } = new();
        public CoreMask? DefaultMask => AvailableMasks.FirstOrDefault(m => m.IsDefault);
        
        /// <summary>
        /// The "All Cores" baseline mask - cannot be deleted
        /// </summary>
        private const string ALL_CORES_MASK_NAME = "All Cores";

        public CoreMaskService(
            ILogger<CoreMaskService> logger,
            ICpuTopologyService cpuTopologyService,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ThreadPilot");

            Directory.CreateDirectory(appDataPath);
            _masksFilePath = Path.Combine(appDataPath, "core_masks.json");
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            _logger.LogInformation("Initializing CoreMaskService...");

            await LoadMasksAsync();

            // If no masks exist, create defaults
            if (AvailableMasks.Count == 0)
            {
                _logger.LogInformation("No masks found, creating defaults...");
                await CreateDefaultMasksAsync();
            }

            _initialized = true;
            _logger.LogInformation("CoreMaskService initialized with {Count} masks", AvailableMasks.Count);
        }

        public async Task<CoreMask> CreateMaskAsync(string name, string description, IEnumerable<bool> boolMask)
        {
            var mask = new CoreMask
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var bit in boolMask)
                mask.BoolMask.Add(bit);

            AvailableMasks.Add(mask);
            await SaveMasksAsync();

            _logger.LogInformation("Created new mask '{Name}' with {Count} cores selected",
                name, mask.SelectedCoreCount);

            return mask;
        }

        public async Task UpdateMaskAsync(CoreMask mask)
        {
            if (mask == null)
                throw new ArgumentNullException(nameof(mask));

            var existing = GetMaskById(mask.Id);
            if (existing == null)
            {
                _logger.LogWarning("Cannot update mask {Id}: not found", mask.Id);
                return;
            }

            mask.UpdatedAt = DateTime.UtcNow;
            await SaveMasksAsync();

            _logger.LogInformation("Updated mask '{Name}'", mask.Name);
        }

        public async Task DeleteMaskAsync(string maskId)
        {
            var mask = GetMaskById(maskId);
            if (mask == null)
            {
                _logger.LogWarning("Cannot delete mask {Id}: not found", maskId);
                return;
            }

            // Cannot delete the "All Cores" baseline mask
            if (mask.Name == ALL_CORES_MASK_NAME)
            {
                _logger.LogWarning("Cannot delete 'All Cores' baseline mask");
                throw new InvalidOperationException("Cannot delete the 'All Cores' baseline mask - it is required as the default fallback");
            }

            // Check if mask is actively applied to running processes
            if (await IsMaskActivelyAppliedAsync(maskId))
            {
                _logger.LogWarning("Cannot delete mask '{Name}': it is actively applied to running processes", mask.Name);
                throw new InvalidOperationException($"Cannot delete mask '{mask.Name}' - it is currently applied to running processes. Please change the mask on those processes first.");
            }

            AvailableMasks.Remove(mask);
            await SaveMasksAsync();

            _logger.LogInformation("Deleted mask '{Name}'", mask.Name);
        }

        public CoreMask? GetMaskById(string maskId)
        {
            return AvailableMasks.FirstOrDefault(m => m.Id == maskId);
        }

        public CoreMask? GetMaskByName(string name)
        {
            return AvailableMasks.FirstOrDefault(m =>
                m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task SaveMasksAsync()
        {
            try
            {
                var data = AvailableMasks.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    description = m.Description,
                    boolMask = m.BoolMask.ToList(),
                    isDefault = m.IsDefault,
                    isEnabled = m.IsEnabled,
                    createdAt = m.CreatedAt,
                    updatedAt = m.UpdatedAt
                }).ToList();

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_masksFilePath, json);
                _logger.LogDebug("Saved {Count} masks to {Path}", AvailableMasks.Count, _masksFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save masks to {Path}", _masksFilePath);
                throw;
            }
        }

        public async Task LoadMasksAsync()
        {
            try
            {
                if (!File.Exists(_masksFilePath))
                {
                    _logger.LogInformation("Masks file not found at {Path}, will create defaults", _masksFilePath);
                    return;
                }

                var json = await File.ReadAllTextAsync(_masksFilePath);
                var data = JsonSerializer.Deserialize<List<JsonElement>>(json);

                if (data == null)
                {
                    _logger.LogWarning("Failed to deserialize masks from {Path}", _masksFilePath);
                    return;
                }

                AvailableMasks.Clear();

                foreach (var item in data)
                {
                    try
                    {
                        var mask = new CoreMask
                        {
                            Id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                            Name = item.GetProperty("name").GetString() ?? "Unnamed",
                            Description = item.GetProperty("description").GetString() ?? "",
                            IsDefault = item.GetProperty("isDefault").GetBoolean(),
                            IsEnabled = item.GetProperty("isEnabled").GetBoolean(),
                            CreatedAt = item.GetProperty("createdAt").GetDateTime(),
                            UpdatedAt = item.GetProperty("updatedAt").GetDateTime()
                        };

                        var boolMask = item.GetProperty("boolMask");
                        foreach (var bit in boolMask.EnumerateArray())
                        {
                            mask.BoolMask.Add(bit.GetBoolean());
                        }

                        AvailableMasks.Add(mask);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load individual mask, skipping");
                    }
                }

                _logger.LogInformation("Loaded {Count} masks from {Path}", AvailableMasks.Count, _masksFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load masks from {Path}", _masksFilePath);
            }
        }

        public async Task<bool> IsMaskReferencedByProfilesAsync(string maskId)
        {
            try
            {
                var profileNames = await GetProfilesReferencingMaskAsync(maskId);
                return profileNames.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if mask {MaskId} is referenced by profiles", maskId);
                return false;
            }
        }

        public async Task<bool> IsMaskActivelyAppliedAsync(string maskId)
        {
            try
            {
                // Check our tracking dictionary for active process masks
                var isActive = _activeProcessMasks.ContainsValue(maskId);
                
                if (isActive)
                {
                    // Verify processes are still running
                    var deadProcesses = new List<int>();
                    foreach (var kvp in _activeProcessMasks.Where(x => x.Value == maskId))
                    {
                        try
                        {
                            Process.GetProcessById(kvp.Key);
                        }
                        catch (ArgumentException)
                        {
                            // Process no longer exists
                            deadProcesses.Add(kvp.Key);
                        }
                    }
                    
                    // Clean up dead processes
                    foreach (var pid in deadProcesses)
                    {
                        _activeProcessMasks.Remove(pid);
                    }
                    
                    // Re-check after cleanup
                    isActive = _activeProcessMasks.ContainsValue(maskId);
                }
                
                await Task.CompletedTask;
                return isActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if mask {MaskId} is actively applied", maskId);
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetProfilesReferencingMaskAsync(string maskId)
        {
            var referencingProfiles = new List<string>();
            
            try
            {
                // Get the association service to check profiles
                var associationService = _serviceProvider.GetService(typeof(IProcessPowerPlanAssociationService)) as IProcessPowerPlanAssociationService;
                if (associationService != null)
                {
                    var associations = await associationService.GetAssociationsAsync();
                    foreach (var association in associations)
                    {
                        if (association.CoreMaskId == maskId)
                        {
                            var profileName = !string.IsNullOrEmpty(association.Description) 
                                ? association.Description 
                                : association.ExecutableName;
                            referencingProfiles.Add(profileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get profiles referencing mask {MaskId}", maskId);
            }
            
            return referencingProfiles;
        }

        public async Task UpdateProfilesToDefaultMaskAsync(string maskId)
        {
            try
            {
                var allCoresMask = GetAllCoresMask();
                if (allCoresMask == null)
                {
                    _logger.LogError("Cannot update profiles: 'All Cores' mask not found");
                    return;
                }

                var associationService = _serviceProvider.GetService(typeof(IProcessPowerPlanAssociationService)) as IProcessPowerPlanAssociationService;
                if (associationService != null)
                {
                    var associations = await associationService.GetAssociationsAsync();
                    foreach (var association in associations)
                    {
                        if (association.CoreMaskId == maskId)
                        {
                            association.CoreMaskId = allCoresMask.Id;
                            association.CoreMaskName = allCoresMask.Name;
                            await associationService.UpdateAssociationAsync(association);
                            _logger.LogInformation("Updated association '{Name}' to use 'All Cores' mask", association.ExecutableName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profiles to default mask");
            }
        }

        public CoreMask? GetAllCoresMask()
        {
            return AvailableMasks.FirstOrDefault(m => m.Name == ALL_CORES_MASK_NAME);
        }

        /// <summary>
        /// Registers that a mask is being applied to a process
        /// </summary>
        public void RegisterMaskApplication(int processId, string maskId)
        {
            _activeProcessMasks[processId] = maskId;
            _logger.LogDebug("Registered mask {MaskId} for process {ProcessId}", maskId, processId);
        }

        /// <summary>
        /// Unregisters a mask application when a process exits or mask is removed
        /// </summary>
        public void UnregisterMaskApplication(int processId)
        {
            if (_activeProcessMasks.Remove(processId))
            {
                _logger.LogDebug("Unregistered mask for process {ProcessId}", processId);
            }
        }

        /// <summary>
        /// Gets all processes that have a specific mask applied
        /// </summary>
        public IEnumerable<int> GetProcessesWithMask(string maskId)
        {
            return _activeProcessMasks.Where(x => x.Value == maskId).Select(x => x.Key);
        }

        public async Task CreateDefaultMasksAsync()
        {
            int coreCount = Environment.ProcessorCount;
            var topology = _cpuTopologyService.CurrentTopology;
            bool topologyConfident = topology?.TopologyDetectionSuccessful == true;
            bool hasHyperThreading = topology?.HasHyperThreading == true;
            bool canCreateNoSmtVariants = topologyConfident && hasHyperThreading;
            
            // Collect all default masks with their "no SMT" variants
            var defaultMasks = new List<(string name, List<bool> boolMask, string description)>();
            
            // Determine CPU manufacturer for naming convention
            bool isIntel = topology?.CpuBrand?.Contains("Intel", StringComparison.OrdinalIgnoreCase) == true;
            bool isAmd = topology?.CpuBrand?.Contains("AMD", StringComparison.OrdinalIgnoreCase) == true;
            string noSmtSuffix = isIntel ? " no HT" : " no SMT";

            // 1. Always add "All Cores" baseline mask (IsDefault = true, cannot be deleted)
            var allCoresMask = new CoreMask
            {
                Name = ALL_CORES_MASK_NAME,
                Description = "Use all available CPU cores - baseline mask",
                IsDefault = true
            };
            for (int i = 0; i < coreCount; i++)
                allCoresMask.BoolMask.Add(true);
            AvailableMasks.Add(allCoresMask);

            // 2. Intel Hybrid Architecture: P-Cores, E-Cores, LPE-Cores (Arrow Lake+)
            if (topology != null && topology.HasIntelHybrid)
            {
                // Detect efficiency class distribution for LPE support
                var efficiencyClasses = topology.LogicalCores
                    .Select(c => GetEfficiencyClass(c))
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToList();
                
                bool hasLpeCores = efficiencyClasses.Count >= 3; // P, E, LPE
                int pClass = hasLpeCores ? 2 : 1;
                int eClass = hasLpeCores ? 1 : 0;
                int lpeClass = 0;

                // P-Cores mask
                var pCoresBoolMask = new List<bool>();
                for (int i = 0; i < coreCount; i++)
                {
                    var core = topology.LogicalCores.FirstOrDefault(c => c.LogicalCoreId == i);
                    pCoresBoolMask.Add(GetEfficiencyClass(core) == pClass);
                }
                if (pCoresBoolMask.Any(b => b))
                    defaultMasks.Add(("P-Cores", pCoresBoolMask, "Intel Performance cores (highest performance)"));

                // E-Cores mask
                var eCoresBoolMask = new List<bool>();
                for (int i = 0; i < coreCount; i++)
                {
                    var core = topology.LogicalCores.FirstOrDefault(c => c.LogicalCoreId == i);
                    eCoresBoolMask.Add(GetEfficiencyClass(core) == eClass);
                }
                if (eCoresBoolMask.Any(b => b))
                    defaultMasks.Add(("E-Cores", eCoresBoolMask, "Intel Efficiency cores (power efficient)"));

                // LPE-Cores mask (Arrow Lake and beyond)
                if (hasLpeCores)
                {
                    var lpeCoresBoolMask = new List<bool>();
                    for (int i = 0; i < coreCount; i++)
                    {
                        var core = topology.LogicalCores.FirstOrDefault(c => c.LogicalCoreId == i);
                        lpeCoresBoolMask.Add(GetEfficiencyClass(core) == lpeClass);
                    }
                    if (lpeCoresBoolMask.Any(b => b))
                        defaultMasks.Add(("LPE-Cores", lpeCoresBoolMask, "Intel Low-Power Efficiency cores (ultra power efficient)"));
                }

                _logger.LogInformation("Created Intel Hybrid masks (P/E{0})", hasLpeCores ? "/LPE" : "");
            }

            // 3. AMD CCD Masks with Cache/Freq differentiation (like CPU Set Setter)
            if (topology != null && topology.HasAmdCcd)
            {
                await CreateAmdCcdMasksAsync(topology, defaultMasks, coreCount);
            }

            // 4. Generate "no SMT/HT" variants for each mask
            var resultMasks = new List<CoreMask>();
            foreach (var (name, boolMask, description) in defaultMasks)
            {
                // Original mask
                resultMasks.Add(CreateCoreMaskFromBoolList(name, boolMask, description));

                // Skip "no HT" variants for E-Cores and LPE-Cores since they don't have HyperThreading
                // Only P-Cores on Intel hybrid architectures have HT
                if (name == "E-Cores" || name == "LPE-Cores")
                {
                    continue;
                }

                // No SMT variant
                if (canCreateNoSmtVariants)
                {
                    var noSmtMask = StripSMT(boolMask, topology, out bool wasStripped);
                    if (wasStripped)
                    {
                        resultMasks.Add(CreateCoreMaskFromBoolList(
                            name + noSmtSuffix,
                            noSmtMask,
                            description + " (no HyperThreading/SMT)"));
                    }
                }
            }

            // 5. "All no HT/SMT" as the last mask
            if (canCreateNoSmtVariants)
            {
                var allCoresBoolMask = Enumerable.Repeat(true, coreCount).ToList();
                var allNoSmtMask = StripSMT(allCoresBoolMask, topology, out bool hasStripped);
                if (hasStripped)
                {
                    resultMasks.Add(CreateCoreMaskFromBoolList(
                        "All" + noSmtSuffix, 
                        allNoSmtMask, 
                        "All physical cores without HyperThreading/SMT"));
                }
            }

            // Add all generated masks to AvailableMasks
            foreach (var mask in resultMasks)
            {
                AvailableMasks.Add(mask);
            }

            await SaveMasksAsync();
            _logger.LogInformation("Created {Count} default masks with topology-aware presets (including no SMT variants)", 
                AvailableMasks.Count);
        }

        /// <summary>
        /// Creates AMD CCD masks with Cache/Freq differentiation (X3D support)
        /// Based on CPU Set Setter's GetDefaultLogicalProcessorMasks
        /// </summary>
        private async Task CreateAmdCcdMasksAsync(CpuTopologyModel topology, 
            List<(string name, List<bool> boolMask, string description)> defaultMasks, 
            int coreCount)
        {
            try
            {
                var ccdIds = topology.AvailableCcds.ToList();
                
                if (ccdIds.Count < 2)
                {
                    // Single CCD - just create one CCD mask
                    if (ccdIds.Count == 1)
                    {
                        var ccdBoolMask = new List<bool>();
                        for (int i = 0; i < coreCount; i++)
                        {
                            var core = topology.LogicalCores.FirstOrDefault(c => c.LogicalCoreId == i);
                            ccdBoolMask.Add(core?.CcdId == ccdIds[0]);
                        }
                        defaultMasks.Add(($"CCD{ccdIds[0]}", ccdBoolMask, $"AMD Core Complex Die {ccdIds[0]}"));
                    }
                    return;
                }

                // Multiple CCDs - try to detect X3D (Cache vs Freq CCDs)
                // X3D chips have one CCD with significantly more L3 cache
                // For simplicity, we'll create numbered CCD masks
                // TODO: Implement L3 cache size detection for X3D differentiation
                
                foreach (var ccdId in ccdIds)
                {
                    var ccdBoolMask = new List<bool>();
                    for (int i = 0; i < coreCount; i++)
                    {
                        var core = topology.LogicalCores.FirstOrDefault(c => c.LogicalCoreId == i);
                        ccdBoolMask.Add(core?.CcdId == ccdId);
                    }
                    
                    if (ccdBoolMask.Any(b => b))
                    {
                        defaultMasks.Add(($"CCD{ccdId}", ccdBoolMask, $"AMD Core Complex Die {ccdId}"));
                    }
                }

                _logger.LogInformation("Created {Count} AMD CCD masks for CCDs: {CCDs}",
                    ccdIds.Count, string.Join(", ", ccdIds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create AMD CCD masks");
            }
        }

        /// <summary>
        /// Strips SMT/HT threads from a bool mask, keeping only physical cores (T0)
        /// Based on CPU Set Setter's StripSMT method
        /// </summary>
        private List<bool> StripSMT(List<bool> boolMask, CpuTopologyModel? topology, out bool hasStripped)
        {
            var result = new List<bool>(boolMask.Count);
            hasStripped = false;

            var coreById = topology?.LogicalCores.ToDictionary(c => c.LogicalCoreId);
            var primaryThreadIds = new HashSet<int>();

            if (coreById != null)
            {
                foreach (var group in coreById.Values.GroupBy(c => c.PhysicalCoreId))
                {
                    var primary = group.OrderBy(c => c.LogicalCoreId).First();
                    primaryThreadIds.Add(primary.LogicalCoreId);
                }
            }

            bool topologyBased = topology?.TopologyDetectionSuccessful == true && primaryThreadIds.Count > 0;

            for (int i = 0; i < boolMask.Count; i++)
            {
                bool isSMTThread = false;
                bool keepBit = boolMask[i];

                if (keepBit)
                {
                    if (topologyBased && coreById != null && coreById.TryGetValue(i, out var core))
                    {
                        isSMTThread = core.IsHyperThreaded && !primaryThreadIds.Contains(i);
                    }
                    else
                    {
                        // Fallback heuristic based on naming
                        var fallbackCore = coreById != null && coreById.TryGetValue(i, out var c) ? c : null;
                        var name = fallbackCore?.LogicalProcessorName;
                        if (!string.IsNullOrEmpty(name) && name.Length >= 2)
                        {
                            var lastTwo = name.Substring(name.Length - 2);
                            if (lastTwo.StartsWith("T") || lastTwo.StartsWith("_T"))
                            {
                                isSMTThread = !name.EndsWith("T0") && !name.EndsWith("_T0");
                            }
                        }
                        else if (fallbackCore?.IsHyperThreaded == true && fallbackCore.HyperThreadSibling.HasValue)
                        {
                            isSMTThread = fallbackCore.LogicalCoreId > fallbackCore.HyperThreadSibling.Value;
                        }
                    }
                }

                if (keepBit && isSMTThread)
                    hasStripped = true;

                result.Add(keepBit && !isSMTThread);
            }

            return result;
        }

        /// <summary>
        /// Gets the efficiency class of a core (for Intel Hybrid detection)
        /// </summary>
        private int GetEfficiencyClass(CpuCoreModel? core)
        {
            if (core == null) return 0;
            
            return core.CoreType switch
            {
                CpuCoreType.PerformanceCore => 2, // Highest efficiency class
                CpuCoreType.EfficiencyCore => 1,  // Middle efficiency class  
                _ => 0                            // Lowest (or unknown/LPE)
            };
        }

        /// <summary>
        /// Creates a CoreMask from a bool list
        /// </summary>
        private CoreMask CreateCoreMaskFromBoolList(string name, List<bool> boolMask, string description)
        {
            var mask = new CoreMask
            {
                Name = name,
                Description = description,
                IsDefault = false,
                IsEnabled = true
            };

            foreach (var bit in boolMask)
                mask.BoolMask.Add(bit);

            return mask;
        }
    }
}

