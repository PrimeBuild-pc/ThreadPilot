using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing power profiles
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private readonly string _profilesDirectory;
        private readonly string _bundledProfilesDirectory;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerProfileService"/> class
        /// </summary>
        public PowerProfileService()
        {
            string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            if (basePath != null)
            {
                _profilesDirectory = Path.Combine(basePath, "Profiles");
                _bundledProfilesDirectory = Path.Combine(basePath, "BundledProfiles");
                
                // Create directories if they don't exist
                Directory.CreateDirectory(_profilesDirectory);
                Directory.CreateDirectory(_bundledProfilesDirectory);
                
                // Copy bundled profiles from resources to the bundled profiles directory
                CopyBundledProfiles();
            }
            else
            {
                _profilesDirectory = "Profiles";
                _bundledProfilesDirectory = "BundledProfiles";
            }
        }
        
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        /// <returns>Collection of power profiles</returns>
        public IEnumerable<PowerProfile> GetAllProfiles()
        {
            var profiles = new List<PowerProfile>();
            
            try
            {
                // Add system profiles
                profiles.AddRange(GetSystemProfiles());
                
                // Add JSON profiles from the profiles directory
                foreach (string filePath in Directory.GetFiles(_profilesDirectory, "*.json"))
                {
                    try
                    {
                        var profile = LoadProfileFromJson(filePath);
                        
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading profile {filePath}: {ex.Message}");
                    }
                }
                
                // Add binary profiles from the profiles directory
                foreach (string filePath in Directory.GetFiles(_profilesDirectory, "*.pow"))
                {
                    try
                    {
                        var profile = LoadProfileFromBinary(filePath);
                        
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading profile {filePath}: {ex.Message}");
                    }
                }
                
                // Add binary profiles from the bundled profiles directory
                foreach (string filePath in Directory.GetFiles(_bundledProfilesDirectory, "*.pow"))
                {
                    try
                    {
                        var profile = LoadProfileFromBinary(filePath);
                        
                        if (profile != null && !profiles.Any(p => p.Name == profile.Name))
                        {
                            profile.IsSystemDefault = true;
                            profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading bundled profile {filePath}: {ex.Message}");
                    }
                }
                
                // Mark the active power plan
                Guid activeProfileGuid = GetActivePowerPlanGuid();
                
                foreach (var profile in profiles.Where(p => p.IsSystemDefault))
                {
                    if (activeProfileGuid != Guid.Empty && profile.FilePath.Contains(activeProfileGuid.ToString()))
                    {
                        profile.IsActive = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Get a power profile by name
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>Power profile</returns>
        public PowerProfile GetProfile(string name)
        {
            try
            {
                return GetAllProfiles().FirstOrDefault(p => p.Name == name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting profile {name}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Create a default power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <param name="description">Profile description</param>
        /// <returns>Created power profile</returns>
        public PowerProfile CreateDefaultProfile(string name, string description)
        {
            return new PowerProfile
            {
                Name = name,
                Description = description,
                CreationDate = DateTime.Now,
                ModificationDate = DateTime.Now,
                IsSystemDefault = false,
                Category = "Custom",
                ApplyAffinityRules = true,
                ApplyPriorityRules = true,
                ApplyPowerSettings = true,
                ApplyCoreParking = true,
                MinimumProcessorState = 5,
                MaximumProcessorState = 100,
                CoreParkingMinCores = 50,
                ProcessorPerformanceBoostPolicy = 3, // Aggressive
                SystemCoolingPolicy = 1, // Active
                AffinityRules = new List<ProcessAffinityRule>(),
                IsActive = false
            };
        }
        
        /// <summary>
        /// Save a power profile
        /// </summary>
        /// <param name="profile">Power profile to save</param>
        /// <returns>True if the profile was saved successfully, false otherwise</returns>
        public bool SaveProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }
            
            try
            {
                profile.ModificationDate = DateTime.Now;
                
                string filePath = Path.Combine(_profilesDirectory, SanitizeFileName(profile.Name) + ".json");
                profile.FilePath = filePath;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                string json = JsonSerializer.Serialize(profile, options);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving profile {profile.Name}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>True if the profile was deleted successfully, false otherwise</returns>
        public bool DeleteProfile(string name)
        {
            try
            {
                var profile = GetProfile(name);
                
                if (profile == null || profile.IsSystemDefault)
                {
                    return false;
                }
                
                if (File.Exists(profile.FilePath))
                {
                    File.Delete(profile.FilePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting profile {name}: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">Power profile to apply</param>
        /// <returns>True if the profile was applied successfully, false otherwise</returns>
        public bool ApplyProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }
            
            bool success = true;
            
            try
            {
                // Apply affinity rules if specified
                if (profile.ApplyAffinityRules && profile.AffinityRules.Any())
                {
                    var processService = ServiceLocator.GetService<IProcessService>();
                    int appliedCount = processService.ApplyProcessAffinityRules(profile.AffinityRules);
                    
                    if (appliedCount == 0 && profile.AffinityRules.Count > 0)
                    {
                        Debug.WriteLine("Warning: No affinity rules were applied");
                        success = false;
                    }
                }
                
                // Apply power settings if specified and this is a system profile
                if (profile.ApplyPowerSettings && profile.IsSystemDefault)
                {
                    string filePath = profile.FilePath;
                    
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        // Check if the file is a .pow file
                        if (filePath.EndsWith(".pow", StringComparison.OrdinalIgnoreCase))
                        {
                            // Apply the power plan
                            success = ApplyPowerPlan(filePath) && success;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: Power plan file not found: {filePath}");
                        success = false;
                    }
                }
                
                // Save the profile if it's a custom profile
                if (!profile.IsSystemDefault)
                {
                    success = SaveProfile(profile) && success;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying profile {profile.Name}: {ex.Message}");
                success = false;
            }
            
            return success;
        }
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">Path to the profile file</param>
        /// <returns>Imported power profile</returns>
        public PowerProfile ImportProfile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }
                
                string extension = Path.GetExtension(filePath).ToLower();
                
                // Import JSON profile
                if (extension == ".json")
                {
                    var profile = LoadProfileFromJson(filePath);
                    
                    if (profile != null)
                    {
                        // Copy the file to the profiles directory
                        string destFilePath = Path.Combine(_profilesDirectory, Path.GetFileName(filePath));
                        
                        if (File.Exists(destFilePath))
                        {
                            destFilePath = Path.Combine(_profilesDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:yyyyMMddHHmmss}.json");
                        }
                        
                        File.Copy(filePath, destFilePath, true);
                        profile.FilePath = destFilePath;
                        
                        return profile;
                    }
                }
                // Import binary profile
                else if (extension == ".pow")
                {
                    var profile = LoadProfileFromBinary(filePath);
                    
                    if (profile != null)
                    {
                        // Copy the file to the profiles directory
                        string destFilePath = Path.Combine(_profilesDirectory, Path.GetFileName(filePath));
                        
                        if (File.Exists(destFilePath))
                        {
                            destFilePath = Path.Combine(_profilesDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now:yyyyMMddHHmmss}.pow");
                        }
                        
                        File.Copy(filePath, destFilePath, true);
                        profile.FilePath = destFilePath;
                        
                        return profile;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing profile from {filePath}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">Power profile to export</param>
        /// <param name="filePath">Path to save the profile file</param>
        /// <returns>True if the profile was exported successfully, false otherwise</returns>
        public bool ExportProfile(PowerProfile profile, string filePath)
        {
            if (profile == null || string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                
                // Export as JSON
                if (extension == ".json")
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    
                    string json = JsonSerializer.Serialize(profile, options);
                    File.WriteAllText(filePath, json, Encoding.UTF8);
                    
                    return true;
                }
                // Export as binary (if it's a system profile)
                else if (extension == ".pow" && profile.IsSystemDefault && !string.IsNullOrEmpty(profile.FilePath))
                {
                    if (File.Exists(profile.FilePath))
                    {
                        File.Copy(profile.FilePath, filePath, true);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting profile {profile.Name} to {filePath}: {ex.Message}");
            }
            
            return false;
        }
        
        private List<PowerProfile> GetSystemProfiles()
        {
            var systemProfiles = new List<PowerProfile>();
            
            try
            {
                // Get system power plans
                IntPtr plansBuffer = IntPtr.Zero;
                uint planCount = 0;
                uint planBufferSize = 16; // Size of a GUID
                
                uint result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME,
                    (uint)PowerDataType.POWER_DATA_GUID, ref plansBuffer, ref planBufferSize, ref planCount);
                
                if (result == 0 && planCount > 0)
                {
                    var activePlanGuid = GetActivePowerPlanGuid();
                    
                    for (int i = 0; i < planCount; i++)
                    {
                        IntPtr planPtr = IntPtr.Add(plansBuffer, i * 16); // 16 is the size of a GUID
                        Guid planGuid = Marshal.PtrToStructure<Guid>(planPtr);
                        
                        string planName = GetPowerPlanName(planGuid);
                        
                        var profile = new PowerProfile
                        {
                            Name = planName,
                            Description = "System power plan",
                            CreationDate = DateTime.Now,
                            ModificationDate = DateTime.Now,
                            IsSystemDefault = true,
                            Category = "System",
                            ApplyAffinityRules = false,
                            ApplyPriorityRules = false,
                            ApplyPowerSettings = true,
                            ApplyCoreParking = true,
                            MinimumProcessorState = 0,
                            MaximumProcessorState = 100,
                            CoreParkingMinCores = 50,
                            ProcessorPerformanceBoostPolicy = 3,
                            SystemCoolingPolicy = 1,
                            AffinityRules = new List<ProcessAffinityRule>(),
                            FilePath = $"System\\{planGuid}",
                            IsActive = planGuid == activePlanGuid
                        };
                        
                        systemProfiles.Add(profile);
                    }
                    
                    LocalFree(plansBuffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system profiles: {ex.Message}");
            }
            
            return systemProfiles;
        }
        
        private string GetPowerPlanName(Guid guid)
        {
            try
            {
                IntPtr friendlyNamePtr = IntPtr.Zero;
                uint friendlyNameSize = 0;
                
                uint result = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, friendlyNamePtr, ref friendlyNameSize);
                
                if (result == 0 && friendlyNameSize > 0)
                {
                    friendlyNamePtr = Marshal.AllocHGlobal((int)friendlyNameSize);
                    
                    if (friendlyNamePtr != IntPtr.Zero)
                    {
                        result = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, friendlyNamePtr, ref friendlyNameSize);
                        
                        if (result == 0)
                        {
                            string friendlyName = Marshal.PtrToStringUni(friendlyNamePtr);
                            Marshal.FreeHGlobal(friendlyNamePtr);
                            
                            return friendlyName;
                        }
                        
                        Marshal.FreeHGlobal(friendlyNamePtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power plan name: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        private Guid GetActivePowerPlanGuid()
        {
            try
            {
                IntPtr activeGuidPtr = IntPtr.Zero;
                uint activeGuidSize = 16; // Size of a GUID
                
                uint result = PowerGetActiveScheme(IntPtr.Zero, ref activeGuidPtr);
                
                if (result == 0 && activeGuidPtr != IntPtr.Zero)
                {
                    Guid activeGuid = Marshal.PtrToStructure<Guid>(activeGuidPtr);
                    LocalFree(activeGuidPtr);
                    
                    return activeGuid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active power plan GUID: {ex.Message}");
            }
            
            return Guid.Empty;
        }
        
        private PowerProfile LoadProfileFromJson(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var profile = JsonSerializer.Deserialize<PowerProfile>(json);
                
                if (profile != null)
                {
                    profile.FilePath = filePath;
                    return profile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile from JSON {filePath}: {ex.Message}");
            }
            
            return null;
        }
        
        private PowerProfile LoadProfileFromBinary(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                
                // Create a simple profile from the file name
                var profile = new PowerProfile
                {
                    Name = fileName,
                    Description = "Imported power profile",
                    CreationDate = File.GetCreationTime(filePath),
                    ModificationDate = File.GetLastWriteTime(filePath),
                    IsSystemDefault = false,
                    Category = "Custom",
                    ApplyAffinityRules = false,
                    ApplyPriorityRules = false,
                    ApplyPowerSettings = true,
                    ApplyCoreParking = true,
                    MinimumProcessorState = 0,
                    MaximumProcessorState = 100,
                    CoreParkingMinCores = 50,
                    ProcessorPerformanceBoostPolicy = 3,
                    SystemCoolingPolicy = 1,
                    AffinityRules = new List<ProcessAffinityRule>(),
                    FilePath = filePath,
                    IsActive = false
                };
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile from binary {filePath}: {ex.Message}");
            }
            
            return null;
        }
        
        private void CopyBundledProfiles()
        {
            try
            {
                // Check if we have the bundled profiles in the application directory
                string[] powFiles = Directory.GetFiles(_bundledProfilesDirectory, "*.pow");
                
                if (powFiles.Length == 0)
                {
                    // Copy all .pow files from the attached_assets directory
                    string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "attached_assets");
                    
                    if (Directory.Exists(assetsDirectory))
                    {
                        foreach (string filePath in Directory.GetFiles(assetsDirectory, "*.pow"))
                        {
                            string fileName = Path.GetFileName(filePath);
                            string destPath = Path.Combine(_bundledProfilesDirectory, fileName);
                            
                            if (!File.Exists(destPath))
                            {
                                File.Copy(filePath, destPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying bundled profiles: {ex.Message}");
            }
        }
        
        private bool ApplyPowerPlan(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }
                
                // Use powercfg.exe to import and set the power plan
                string tempGuid = Guid.NewGuid().ToString();
                string importArg = $"/import \"{filePath}\" {tempGuid}";
                
                // Import the power plan
                if (RunPowerCfg(importArg))
                {
                    // Set the imported power plan as active
                    string setActiveArg = $"/s {tempGuid}";
                    return RunPowerCfg(setActiveArg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying power plan {filePath}: {ex.Message}");
            }
            
            return false;
        }
        
        private bool RunPowerCfg(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powercfg.exe";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                
                process.Start();
                process.WaitForExit();
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running powercfg with arguments {arguments}: {ex.Message}");
                return false;
            }
        }
        
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "Unnamed";
            }
            
            // Replace invalid characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string validFileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Ensure the file name is not empty after sanitization
            return string.IsNullOrEmpty(validFileName) ? "Unnamed" : validFileName;
        }
        
        #region Native methods
        
        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, ref IntPtr ActivePolicyGuid);
        
        [DllImport("powrprof.dll")]
        private static extern uint PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid,
            uint AccessFlags, uint Level, ref IntPtr Buffer, ref uint BufferSize, ref uint Count);
        
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid,
            IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
        
        private enum AccessFlags
        {
            ACCESS_SCHEME = 16
        }
        
        private enum PowerDataType
        {
            POWER_DATA_GUID = 0
        }
        
        #endregion
    }
}