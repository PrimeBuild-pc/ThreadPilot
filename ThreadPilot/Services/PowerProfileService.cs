using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Power profile service implementation
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private const string UserProfilesDirectory = "Profiles";
        private const string BundledProfilesDirectory = "BundledProfiles";
        
        private readonly IProcessService _processService;
        private readonly List<PowerProfile> _bundledProfiles = new List<PowerProfile>();
        private readonly List<PowerProfile> _userProfiles = new List<PowerProfile>();
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processService">Process service</param>
        public PowerProfileService(IProcessService processService)
        {
            _processService = processService;
            
            // Load profiles
            LoadProfiles();
        }
        
        /// <summary>
        /// Get all power profiles
        /// </summary>
        /// <returns>Power profiles collection</returns>
        public IEnumerable<PowerProfile> GetAllProfiles()
        {
            var allProfiles = new List<PowerProfile>();
            allProfiles.AddRange(_bundledProfiles);
            allProfiles.AddRange(_userProfiles);
            
            return allProfiles;
        }
        
        /// <summary>
        /// Get bundled power profiles
        /// </summary>
        /// <returns>Bundled power profiles collection</returns>
        public IEnumerable<PowerProfile> GetBundledProfiles()
        {
            return _bundledProfiles;
        }
        
        /// <summary>
        /// Get user power profiles
        /// </summary>
        /// <returns>User power profiles collection</returns>
        public IEnumerable<PowerProfile> GetUserProfiles()
        {
            return _userProfiles;
        }
        
        /// <summary>
        /// Get profile by GUID
        /// </summary>
        /// <param name="guid">Profile GUID</param>
        /// <returns>Power profile or null if not found</returns>
        public PowerProfile GetProfileByGuid(string guid)
        {
            var allProfiles = GetAllProfiles();
            return allProfiles.FirstOrDefault(p => p.Guid == guid);
        }
        
        /// <summary>
        /// Load profile from file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Power profile or null if failed</returns>
        public PowerProfile LoadProfile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"File {filePath} does not exist");
                    return null;
                }
                
                string jsonContent = File.ReadAllText(filePath);
                var profile = JsonSerializer.Deserialize<PowerProfile>(jsonContent);
                
                if (profile != null)
                {
                    profile.FilePath = filePath;
                    profile.IsBundled = false; // User-loaded profile is not bundled
                    
                    // Generate new GUID if empty
                    if (string.IsNullOrEmpty(profile.Guid))
                    {
                        profile.Guid = Guid.NewGuid().ToString();
                    }
                    
                    // Add to user profiles if not already there
                    if (!_userProfiles.Any(p => p.Guid == profile.Guid))
                    {
                        _userProfiles.Add(profile);
                    }
                }
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Save profile
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
                
                // Generate GUID if empty
                if (string.IsNullOrEmpty(profile.Guid))
                {
                    profile.Guid = Guid.NewGuid().ToString();
                }
                
                // Update last modified
                profile.LastModified = DateTime.Now;
                
                // Create profiles directory if it doesn't exist
                string profilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UserProfilesDirectory);
                if (!Directory.Exists(profilesDirectory))
                {
                    Directory.CreateDirectory(profilesDirectory);
                }
                
                // Create file path if empty
                if (string.IsNullOrEmpty(profile.FilePath))
                {
                    string fileName = $"{SanitizeFileName(profile.Name)}.pow";
                    profile.FilePath = Path.Combine(profilesDirectory, fileName);
                }
                
                // Serialize profile
                string jsonContent = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profile.FilePath, jsonContent);
                
                // Add to user profiles if not already there
                if (!_userProfiles.Any(p => p.Guid == profile.Guid))
                {
                    _userProfiles.Add(profile);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Save profile to file
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <param name="filePath">File path</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveProfileToFile(PowerProfile profile, string filePath)
        {
            try
            {
                if (profile == null)
                {
                    return false;
                }
                
                // Serialize profile
                string jsonContent = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, jsonContent);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving profile to file: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Delete profile
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
                
                // Cannot delete bundled profiles
                if (profile.IsBundled)
                {
                    return false;
                }
                
                // Remove from user profiles
                _userProfiles.RemoveAll(p => p.Guid == profile.Guid);
                
                // Delete file if it exists
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
        /// Apply profile to system
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ApplyProfile(PowerProfile profile)
        {
            try
            {
                if (profile == null || _processService == null)
                {
                    return false;
                }
                
                var processes = _processService.GetProcesses();
                
                foreach (var rule in profile.AffinityRules.Where(r => r.IsEnabled))
                {
                    // Skip rules with no pattern
                    if (string.IsNullOrEmpty(rule.ProcessNamePattern))
                    {
                        continue;
                    }
                    
                    try
                    {
                        var regex = new Regex(rule.ProcessNamePattern, RegexOptions.IgnoreCase);
                        
                        foreach (var process in processes)
                        {
                            // Apply rule if process name matches pattern
                            if (regex.IsMatch(process.Name))
                            {
                                // Apply affinity if rule has core indices
                                if (rule.CoreIndices != null && rule.CoreIndices.Count > 0)
                                {
                                    _processService.SetProcessAffinity(process.Id, rule.CoreIndices);
                                }
                                
                                // Apply priority
                                _processService.SetProcessPriority(process.Id, rule.ProcessPriority);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error applying rule {rule.Name}: {ex.Message}");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Load profiles from disk
        /// </summary>
        private void LoadProfiles()
        {
            // Clear existing profiles
            _bundledProfiles.Clear();
            _userProfiles.Clear();
            
            // Load bundled profiles
            string bundledProfilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BundledProfilesDirectory);
            if (Directory.Exists(bundledProfilesDirectory))
            {
                foreach (var filePath in Directory.GetFiles(bundledProfilesDirectory, "*.pow"))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(filePath);
                        var profile = JsonSerializer.Deserialize<PowerProfile>(jsonContent);
                        
                        if (profile != null)
                        {
                            profile.FilePath = filePath;
                            profile.IsBundled = true; // Mark as bundled
                            
                            // Generate GUID if empty
                            if (string.IsNullOrEmpty(profile.Guid))
                            {
                                profile.Guid = Guid.NewGuid().ToString();
                            }
                            
                            _bundledProfiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading bundled profile {filePath}: {ex.Message}");
                    }
                }
            }
            
            // Load user profiles
            string userProfilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UserProfilesDirectory);
            if (Directory.Exists(userProfilesDirectory))
            {
                foreach (var filePath in Directory.GetFiles(userProfilesDirectory, "*.pow"))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(filePath);
                        var profile = JsonSerializer.Deserialize<PowerProfile>(jsonContent);
                        
                        if (profile != null)
                        {
                            profile.FilePath = filePath;
                            profile.IsBundled = false; // Mark as user profile
                            
                            // Generate GUID if empty
                            if (string.IsNullOrEmpty(profile.Guid))
                            {
                                profile.Guid = Guid.NewGuid().ToString();
                            }
                            
                            _userProfiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading user profile {filePath}: {ex.Message}");
                    }
                }
            }
            else
            {
                // Create user profiles directory if it doesn't exist
                Directory.CreateDirectory(userProfilesDirectory);
            }
            
            // Copy bundled profiles to user profiles folder if not already there
            CopyBundledProfilesToUserProfiles();
        }
        
        /// <summary>
        /// Copy bundled profiles to user profiles folder
        /// </summary>
        private void CopyBundledProfilesToUserProfiles()
        {
            // Check if first run by looking at user profiles count
            if (_userProfiles.Count == 0)
            {
                string userProfilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, UserProfilesDirectory);
                
                // Copy bundled .pow files to user profiles directory as examples
                if (Directory.Exists(userProfilesDirectory))
                {
                    // Create a default profile
                    var defaultProfile = new PowerProfile
                    {
                        Name = "Default Profile",
                        Description = "Default process affinity profile",
                        IsBundled = false,
                        LastModified = DateTime.Now,
                        Guid = Guid.NewGuid().ToString()
                    };
                    
                    // Add some example rules
                    defaultProfile.AffinityRules.Add(new ProcessAffinityRule
                    {
                        Name = "Performance Cores for Chrome",
                        ProcessNamePattern = "chrome",
                        ProcessPriority = ProcessPriority.Normal,
                        IsEnabled = true,
                        CoreIndices = new List<int> { 0, 1, 2, 3 } // First 4 cores (typically performance cores)
                    });
                    
                    defaultProfile.AffinityRules.Add(new ProcessAffinityRule
                    {
                        Name = "System Processes on Efficiency Cores",
                        ProcessNamePattern = "svchost|wininit|csrss",
                        ProcessPriority = ProcessPriority.BelowNormal,
                        IsEnabled = true,
                        CoreIndices = new List<int> { 4, 5, 6, 7 } // Last 4 cores (typically efficiency cores)
                    });
                    
                    defaultProfile.AffinityRules.Add(new ProcessAffinityRule
                    {
                        Name = "High Priority for Games",
                        ProcessNamePattern = "game|steam|origin|epic",
                        ProcessPriority = ProcessPriority.High,
                        IsEnabled = true,
                        CoreIndices = Enumerable.Range(0, 8).ToList() // All cores
                    });
                    
                    // Save default profile
                    SaveProfile(defaultProfile);
                }
            }
        }
        
        /// <summary>
        /// Sanitize file name
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Sanitized file name</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "profile";
            }
            
            // Remove invalid characters
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            
            return fileName;
        }
    }
}