using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of power profile service
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private readonly IProcessService _processService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public PowerProfileService()
        {
            _processService = ServiceLocator.Resolve<IProcessService>();
        }
        
        /// <summary>
        /// Gets all available power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        public IEnumerable<PowerProfile> GetProfiles()
        {
            var profiles = new List<PowerProfile>();
            
            try
            {
                // Add built-in Windows power schemes
                profiles.AddRange(GetWindowsPowerProfiles());
                
                // Add custom profiles from profile directory
                var profileDirectory = App.ProfileDirectory;
                if (Directory.Exists(profileDirectory))
                {
                    foreach (var file in Directory.GetFiles(profileDirectory, "*.json"))
                    {
                        try
                        {
                            var profile = LoadProfileFromFile(file);
                            if (profile != null)
                            {
                                profiles.Add(profile);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading profile from {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Gets power profile by name
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>Power profile or null if not found</returns>
        public PowerProfile GetProfileByName(string name)
        {
            return GetProfiles().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Gets active power profile
        /// </summary>
        /// <returns>Active power profile or null if not found</returns>
        public PowerProfile GetActiveProfile()
        {
            try
            {
                // Get active Windows power scheme
                var activeGuid = GetActiveWindowsPowerSchemeGuid();
                if (!string.IsNullOrEmpty(activeGuid))
                {
                    return GetProfiles().FirstOrDefault(p => p.Guid == activeGuid);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active profile: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Creates new power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <param name="description">Profile description</param>
        /// <returns>New power profile</returns>
        public PowerProfile CreateProfile(string name, string description)
        {
            var profile = new PowerProfile
            {
                Name = name,
                Description = description,
                IsBundled = false,
                LastModified = DateTime.Now,
                Guid = Guid.NewGuid().ToString()
            };
            
            return SaveProfile(profile) ? profile : null;
        }
        
        /// <summary>
        /// Saves power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveProfile(PowerProfile profile)
        {
            try
            {
                if (profile == null)
                {
                    return false;
                }
                
                // Don't save bundled profiles
                if (profile.IsBundled)
                {
                    return false;
                }
                
                // Update last modified date
                profile.LastModified = DateTime.Now;
                
                // Ensure profile directory exists
                var profileDirectory = App.ProfileDirectory;
                if (!Directory.Exists(profileDirectory))
                {
                    Directory.CreateDirectory(profileDirectory);
                }
                
                // Save profile to file
                var filePath = Path.Combine(profileDirectory, $"{profile.Name}.json");
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Deletes power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool DeleteProfile(PowerProfile profile)
        {
            try
            {
                if (profile == null)
                {
                    return false;
                }
                
                // Don't delete bundled profiles
                if (profile.IsBundled)
                {
                    return false;
                }
                
                // Delete profile file if exists
                if (!string.IsNullOrEmpty(profile.FilePath) && File.Exists(profile.FilePath))
                {
                    File.Delete(profile.FilePath);
                    return true;
                }
                
                // Try to find profile file by name
                var profileDirectory = App.ProfileDirectory;
                var filePath = Path.Combine(profileDirectory, $"{profile.Name}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Imports power profile from file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Imported power profile or null if import failed</returns>
        public PowerProfile ImportProfile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }
                
                var extension = Path.GetExtension(filePath).ToLower();
                
                // Import profile based on file extension
                if (extension == ".json")
                {
                    // Import JSON profile
                    var profile = LoadProfileFromFile(filePath);
                    if (profile != null)
                    {
                        // Make it a custom profile
                        profile.IsBundled = false;
                        profile.LastModified = DateTime.Now;
                        profile.FilePath = filePath;
                        
                        // Save profile
                        SaveProfile(profile);
                        
                        return profile;
                    }
                }
                else if (extension == ".pow")
                {
                    // Import PSD power profile
                    var profile = new PowerProfile
                    {
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Description = "Imported power profile",
                        IsBundled = false,
                        LastModified = DateTime.Now,
                        FilePath = filePath,
                        Guid = Guid.NewGuid().ToString()
                    };
                    
                    // Save profile
                    SaveProfile(profile);
                    
                    return profile;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Exports power profile to file
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <param name="filePath">File path</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExportProfile(PowerProfile profile, string filePath)
        {
            try
            {
                if (profile == null || string.IsNullOrEmpty(filePath))
                {
                    return false;
                }
                
                var extension = Path.GetExtension(filePath).ToLower();
                
                // Export profile based on file extension
                if (extension == ".json")
                {
                    // Export JSON profile
                    var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                    return true;
                }
                else if (extension == ".pow")
                {
                    // Export PSD power profile
                    // This would need to be implemented based on the format
                    return false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Applies power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ApplyProfile(PowerProfile profile)
        {
            try
            {
                if (profile == null)
                {
                    return false;
                }
                
                // Apply Windows power scheme if available
                var success = false;
                if (!string.IsNullOrEmpty(profile.Guid))
                {
                    success = SetActiveWindowsPowerScheme(profile.Guid);
                }
                
                // Apply process affinity rules
                var rulesApplied = ApplyAffinityRules(profile);
                
                return success || rulesApplied > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets all bundled power profiles
        /// </summary>
        /// <returns>List of bundled power profiles</returns>
        public IEnumerable<BundledPowerProfile> GetBundledProfiles()
        {
            var profiles = new List<BundledPowerProfile>();
            
            try
            {
                // Look for bundled profiles in Assets directory
                var assetsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                if (Directory.Exists(assetsDirectory))
                {
                    foreach (var file in Directory.GetFiles(assetsDirectory, "*.pow"))
                    {
                        try
                        {
                            var fileName = Path.GetFileName(file);
                            var name = Path.GetFileNameWithoutExtension(file);
                            
                            // Get description from the file if possible
                            // For now, use generic descriptions
                            var description = "Bundled power profile";
                            
                            profiles.Add(new BundledPowerProfile
                            {
                                Name = name,
                                Description = description,
                                FileName = fileName
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading bundled profile {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting bundled profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Applies affinity rules from a power profile to all processes
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>Number of processes affected</returns>
        public int ApplyAffinityRules(PowerProfile profile)
        {
            if (profile == null || _processService == null)
            {
                return 0;
            }
            
            return _processService.ApplyAffinityRules(profile.AffinityRules);
        }
        
        /// <summary>
        /// Gets Windows power profiles
        /// </summary>
        /// <returns>List of Windows power profiles</returns>
        private IEnumerable<PowerProfile> GetWindowsPowerProfiles()
        {
            var profiles = new List<PowerProfile>();
            
            try
            {
                // Run powercfg to get list of power schemes
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        // Parse power schemes
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.Contains("GUID:"))
                            {
                                try
                                {
                                    var guidStart = line.IndexOf("(") + 1;
                                    var guidEnd = line.IndexOf(")", guidStart);
                                    var guid = line.Substring(guidStart, guidEnd - guidStart).Trim();
                                    
                                    var nameStart = line.IndexOf(":") + 1;
                                    var nameEnd = line.IndexOf("(", nameStart);
                                    var name = line.Substring(nameStart, nameEnd - nameStart).Trim();
                                    
                                    profiles.Add(new PowerProfile
                                    {
                                        Name = name,
                                        Description = "Windows power scheme",
                                        IsBundled = true,
                                        LastModified = DateTime.Now,
                                        Guid = guid
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error parsing power scheme line {line}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows power profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Gets active Windows power scheme GUID
        /// </summary>
        /// <returns>Active power scheme GUID</returns>
        private string GetActiveWindowsPowerSchemeGuid()
        {
            try
            {
                // Run powercfg to get active power scheme
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        // Parse active power scheme
                        if (output.Contains("GUID:"))
                        {
                            var guidStart = output.IndexOf("(") + 1;
                            var guidEnd = output.IndexOf(")", guidStart);
                            return output.Substring(guidStart, guidEnd - guidStart).Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active Windows power scheme: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Sets active Windows power scheme
        /// </summary>
        /// <param name="guid">Power scheme GUID</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool SetActiveWindowsPowerScheme(string guid)
        {
            try
            {
                // Run powercfg to set active power scheme
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/setactive {guid}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        return process.ExitCode == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting active Windows power scheme: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Loads power profile from JSON file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Power profile or null if loading failed</returns>
        private PowerProfile LoadProfileFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }
                
                var json = File.ReadAllText(filePath);
                var profile = JsonSerializer.Deserialize<PowerProfile>(json);
                
                if (profile != null)
                {
                    profile.FilePath = filePath;
                }
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile from file {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}