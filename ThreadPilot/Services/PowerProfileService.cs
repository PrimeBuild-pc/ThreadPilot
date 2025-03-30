using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of the power profile service
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        // Default profiles folder
        private readonly string _profilesFolder;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public PowerProfileService()
        {
            // Initialize profiles folder
            _profilesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThreadPilot",
                "Profiles");
                
            // Create the folder if it doesn't exist
            if (!Directory.Exists(_profilesFolder))
            {
                Directory.CreateDirectory(_profilesFolder);
            }
        }
        
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        public IList<BundledPowerProfile> GetAvailableProfiles()
        {
            var profiles = new List<BundledPowerProfile>();
            
            try
            {
                // Add demo power profiles - in a real app, this would load actual Windows power profiles
                // and user-created custom profiles from files
                profiles.Add(CreateDemoProfile("Balanced", "Windows default balanced power plan", "balanced", true));
                profiles.Add(CreateDemoProfile("High Performance", "Maximizes system performance and responsiveness", "performance", false));
                profiles.Add(CreateDemoProfile("Power Saver", "Saves energy by reducing performance", "powersaver", false));
                profiles.Add(CreateDemoProfile("Ultimate Performance", "Provides ultimate performance on Windows", "ultimate", false));
                
                // Add any additional saved profiles in the profiles folder
                var powFiles = Directory.GetFiles(_profilesFolder, "*.pow");
                foreach (var file in powFiles)
                {
                    var fileProfile = LoadProfileFromFile(file);
                    if (fileProfile != null && !profiles.Any(p => p.FilePath == file))
                    {
                        profiles.Add(fileProfile);
                    }
                }
                
                // Also look for attached sample profiles
                var attachedFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attached_assets");
                if (Directory.Exists(attachedFolder))
                {
                    var attachedFiles = Directory.GetFiles(attachedFolder, "*.pow");
                    foreach (var file in attachedFiles)
                    {
                        var fileProfile = LoadProfileFromFile(file);
                        if (fileProfile != null && !profiles.Any(p => p.FilePath == file))
                        {
                            profiles.Add(fileProfile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Get the currently active power profile
        /// </summary>
        public BundledPowerProfile? GetActiveProfile()
        {
            // In a real app, this would get the actual active Windows power profile
            var profiles = GetAvailableProfiles();
            return profiles.FirstOrDefault(p => p.Category == "balanced");
        }
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        public async Task<bool> ApplyProfileAsync(BundledPowerProfile profile)
        {
            // This would actually apply the Windows power profile using powercfg or other API
            await Task.Delay(1000); // Simulate work
            
            return true;
        }
        
        /// <summary>
        /// Save profile to a file
        /// </summary>
        public bool SaveProfileToFile(BundledPowerProfile profile, string filePath)
        {
            try
            {
                // In a real app, this would serialize the actual power profile data
                // For demo purposes, we'll just write some demo data
                File.WriteAllText(filePath, $"DEMOPOWER:{profile.Name}:{profile.Description}:{DateTime.Now}");
                
                // Update the file path in the profile
                profile.FilePath = filePath;
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Load profile from a file
        /// </summary>
        public BundledPowerProfile? LoadProfileFromFile(string filePath)
        {
            try
            {
                // Check if the file exists
                if (!File.Exists(filePath))
                {
                    return null;
                }
                
                // Check the file size - if it's tiny or empty, it might be corrupted
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 10)
                {
                    // Just create a default profile based on filename
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    return new BundledPowerProfile
                    {
                        Name = fileName,
                        Description = $"Imported profile: {fileName}",
                        FilePath = filePath,
                        CreatedOn = fileInfo.CreationTime,
                        ModifiedOn = fileInfo.LastWriteTime,
                        IsReadOnly = false,
                        IsSystemProfile = false,
                        Category = "custom"
                    };
                }
                
                // In a real app, this would deserialize the actual power profile data
                // For demo purposes, we'll create a profile with basic info
                var fileName2 = Path.GetFileNameWithoutExtension(filePath);
                return new BundledPowerProfile
                {
                    Name = fileName2,
                    Description = $"Custom power profile imported from {fileName2}",
                    FilePath = filePath,
                    CreatedOn = fileInfo.CreationTime,
                    ModifiedOn = fileInfo.LastWriteTime,
                    IsReadOnly = false,
                    IsSystemProfile = false,
                    Category = "custom"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Create a profile from the current system settings
        /// </summary>
        public BundledPowerProfile CreateProfileFromCurrentSettings(string name, string description)
        {
            // In a real app, this would create a profile from the actual current Windows power settings
            var profile = new BundledPowerProfile
            {
                Name = name,
                Description = description,
                CreatedOn = DateTime.Now,
                ModifiedOn = DateTime.Now,
                IsReadOnly = false,
                IsSystemProfile = false,
                Category = "custom"
            };
            
            return profile;
        }
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        public bool DeleteProfile(BundledPowerProfile profile)
        {
            try
            {
                // Don't allow deletion of system profiles
                if (profile.IsSystemProfile)
                {
                    return false;
                }
                
                // If the profile has a file, delete it
                if (!string.IsNullOrEmpty(profile.FilePath) && File.Exists(profile.FilePath))
                {
                    File.Delete(profile.FilePath);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Create a demo power profile
        /// </summary>
        private BundledPowerProfile CreateDemoProfile(string name, string description, string category, bool isActive)
        {
            // This method is for demo purposes only
            var profile = new BundledPowerProfile
            {
                Name = name,
                Description = description,
                CreatedOn = DateTime.Now.AddDays(-30),
                ModifiedOn = DateTime.Now.AddDays(-5),
                IsReadOnly = true,
                IsSystemProfile = true,
                Category = category,
                IsActive = isActive,
                Icon = category switch
                {
                    "balanced" => "Balance",
                    "performance" => "Performance",
                    "powersaver" => "Battery",
                    "ultimate" => "Ultimate",
                    _ => "Custom"
                },
                WindowsGuid = Guid.NewGuid() // In real app, this would be the actual Windows GUID
            };
            
            // For built-in profiles, the file path would point to Windows power profiles
            // We'll set a demo path
            profile.FilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "PowerProfiles", 
                $"{category}.pow");
                
            return profile;
        }
    }
}