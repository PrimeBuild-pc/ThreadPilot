using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the power profile service
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private const string ProfilesDirectory = "Profiles";
        private const string ProfileExtension = ".pow";
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerProfileService"/> class
        /// </summary>
        public PowerProfileService()
        {
            // Create profiles directory if it doesn't exist
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
            }
            
            // Create default profiles if they don't exist
            EnsureDefaultProfilesExist();
        }
        
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        public List<PowerProfile> GetAllProfiles()
        {
            var profiles = new List<PowerProfile>();
            
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                {
                    Directory.CreateDirectory(ProfilesDirectory);
                }
                
                string[] files = Directory.GetFiles(ProfilesDirectory, $"*{ProfileExtension}");
                
                foreach (string file in files)
                {
                    try
                    {
                        var profile = ImportProfile(file);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch
                    {
                        // Skip profiles that can't be loaded
                    }
                }
            }
            catch (Exception)
            {
                // If we can't load profiles from disk, create some default ones
                if (profiles.Count == 0)
                {
                    profiles.Add(CreateDefaultProfile("Power Saver", "System"));
                    profiles.Add(CreateDefaultProfile("Balanced", "System"));
                    profiles.Add(CreateDefaultProfile("High Performance", "System"));
                    profiles.Add(CreateDefaultProfile("Gaming", "Gaming"));
                }
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Save a power profile
        /// </summary>
        /// <param name="profile">The profile to save</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }
            
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                {
                    Directory.CreateDirectory(ProfilesDirectory);
                }
                
                string filePath = Path.Combine(ProfilesDirectory, GetProfileFileName(profile.Name));
                
                // Serialize the profile to JSON
                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Save to file
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="profileName">The profile name</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool DeleteProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return false;
            }
            
            try
            {
                string filePath = Path.Combine(ProfilesDirectory, GetProfileFileName(profileName));
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Create a new default power profile with the given name
        /// </summary>
        /// <param name="name">The profile name</param>
        /// <param name="category">The profile category</param>
        /// <returns>A new power profile</returns>
        public PowerProfile CreateDefaultProfile(string name, string category = "Custom")
        {
            var profile = new PowerProfile
            {
                Name = name,
                Category = category,
                CreatedAt = DateTime.Now,
                IsSystemDefault = category == "System"
            };
            
            switch (name)
            {
                case "Power Saver":
                    profile.Description = "Optimized for maximum energy efficiency. Limits performance to save power.";
                    profile.WindowsPowerPlan = "Power saver";
                    profile.ParkUnusedCores = true;
                    profile.MaxActiveCores = Environment.ProcessorCount / 2;
                    break;
                    
                case "Balanced":
                    profile.Description = "Balance of performance and energy efficiency. Recommended for most users.";
                    profile.WindowsPowerPlan = "Balanced";
                    profile.ParkUnusedCores = false;
                    profile.MaxActiveCores = 0; // All cores
                    break;
                    
                case "High Performance":
                    profile.Description = "Maximum performance for demanding tasks. May use more power.";
                    profile.WindowsPowerPlan = "High performance";
                    profile.ParkUnusedCores = false;
                    profile.MaxActiveCores = 0; // All cores
                    break;
                    
                case "Gaming":
                    profile.Description = "Optimized for gaming with prioritized GPU and game processes.";
                    profile.WindowsPowerPlan = "Ultimate Performance";
                    profile.ParkUnusedCores = false;
                    profile.MaxActiveCores = 0; // All cores
                    
                    // Add some common gaming-related process rules
                    var random = new Random();
                    long fullMask = (1L << Environment.ProcessorCount) - 1;
                    
                    profile.AffinityRules.Add(new ProcessAffinityRule
                    {
                        ProcessNamePattern = "steam.exe",
                        Description = "Steam client",
                        Priority = ProcessPriority.Normal,
                        AffinityMask = fullMask / 4 // Use 25% of cores
                    });
                    
                    profile.AffinityRules.Add(new ProcessAffinityRule
                    {
                        ProcessNamePattern = "*.exe",
                        Description = "Games (executable files)",
                        Priority = ProcessPriority.High,
                        AffinityMask = fullMask // Use all cores
                    });
                    
                    break;
                    
                default:
                    profile.Description = "Custom power profile.";
                    profile.WindowsPowerPlan = "Balanced";
                    profile.ParkUnusedCores = false;
                    profile.MaxActiveCores = 0; // All cores
                    break;
            }
            
            return profile;
        }
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>The imported power profile, or null if failed</returns>
        public PowerProfile? ImportProfile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }
            
            try
            {
                // Read the file contents
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                
                // Deserialize the profile from JSON
                var profile = JsonSerializer.Deserialize<PowerProfile>(json);
                
                return profile;
            }
            catch (Exception)
            {
                // If we can't load the profile from this file, return null
                return null;
            }
        }
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">The profile to export</param>
        /// <param name="filePath">The file path</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExportProfile(PowerProfile profile, string filePath)
        {
            if (profile == null || string.IsNullOrEmpty(filePath))
            {
                return false;
            }
            
            try
            {
                // Serialize the profile to JSON
                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Save to file
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">The profile to apply</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ApplyProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }
            
            bool success = true;
            
            try
            {
                // Set Windows power plan
                if (!string.IsNullOrEmpty(profile.WindowsPowerPlan))
                {
                    success &= SetPowerPlan(profile.WindowsPowerPlan);
                }
                
                // Apply process affinity rules
                int rulesApplied = ServiceLocator.GetService<IProcessService>().ApplyProcessAffinityRules(profile);
                
                // Core parking would be implemented here in a real application
                // For now, we just log the intention
                Debug.WriteLine($"Would set core parking to: {profile.ParkUnusedCores}");
                Debug.WriteLine($"Would set max active cores to: {profile.MaxActiveCores}");
            }
            catch (Exception)
            {
                success = false;
            }
            
            return success;
        }
        
        /// <summary>
        /// Get the current active Windows power plan
        /// </summary>
        /// <returns>The power plan name</returns>
        public string GetCurrentPowerPlan()
        {
            // In a real implementation, this would query the Windows power management API
            // For now, return a simulated value
            string[] plans = { "Power saver", "Balanced", "High performance", "Ultimate Performance" };
            return plans[new Random().Next(0, plans.Length)];
        }
        
        /// <summary>
        /// Set the active Windows power plan
        /// </summary>
        /// <param name="planName">The plan name or GUID</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetPowerPlan(string planName)
        {
            // In a real implementation, this would use the Windows power management API
            // For now, just pretend it worked
            return true;
        }
        
        /// <summary>
        /// Ensure default profiles exist
        /// </summary>
        private void EnsureDefaultProfilesExist()
        {
            var defaultProfiles = new[]
            {
                "Power Saver",
                "Balanced",
                "High Performance",
                "Gaming"
            };
            
            foreach (var profileName in defaultProfiles)
            {
                string filePath = Path.Combine(ProfilesDirectory, GetProfileFileName(profileName));
                
                if (!File.Exists(filePath))
                {
                    var profile = CreateDefaultProfile(profileName, profileName == "Gaming" ? "Gaming" : "System");
                    SaveProfile(profile);
                }
            }
        }
        
        /// <summary>
        /// Get a sanitized file name for a profile
        /// </summary>
        /// <param name="profileName">The profile name</param>
        /// <returns>The sanitized file name</returns>
        private string GetProfileFileName(string profileName)
        {
            // Remove invalid characters
            string fileName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
            
            // Add extension
            return $"{fileName}{ProfileExtension}";
        }
    }
}