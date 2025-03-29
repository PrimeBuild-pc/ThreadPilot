using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing bundled power profiles (.pow files)
    /// </summary>
    public class BundledPowerProfilesService : IBundledPowerProfilesService
    {
        private readonly string _bundledProfilesPath;
        private readonly List<BundledPowerProfile> _bundledProfiles;
        private readonly IFileDialogService _fileDialogService;
        private readonly NotificationService _notificationService;
        
        /// <summary>
        /// Constructor for BundledPowerProfilesService
        /// </summary>
        public BundledPowerProfilesService(
            IFileDialogService fileDialogService, 
            NotificationService notificationService)
        {
            _fileDialogService = fileDialogService;
            _notificationService = notificationService;
            
            // Path to the bundled profiles folder (in the application directory)
            _bundledProfilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerProfiles");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_bundledProfilesPath))
            {
                Directory.CreateDirectory(_bundledProfilesPath);
            }
            
            _bundledProfiles = new List<BundledPowerProfile>();
        }
        
        /// <summary>
        /// Gets all bundled power profiles
        /// </summary>
        /// <returns>A list of bundled power profiles</returns>
        public async Task<List<BundledPowerProfile>> GetBundledProfilesAsync()
        {
            if (_bundledProfiles.Count == 0)
            {
                await LoadBundledProfilesAsync();
            }
            
            return _bundledProfiles;
        }
        
        /// <summary>
        /// Imports a bundled power profile into Windows
        /// </summary>
        /// <param name="profile">The profile to import</param>
        /// <returns>True if import was successful, false otherwise</returns>
        public async Task<bool> ImportProfileAsync(BundledPowerProfile profile)
        {
            try
            {
                // Use powercfg.exe to import the profile
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/import \"{profile.FilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _notificationService.ShowError($"Failed to import profile: {error}");
                    return false;
                }
                
                // After importing, get the GUID of the imported profile
                await RefreshProfileStatusAsync();
                
                _notificationService.ShowSuccess($"Profile '{profile.Name}' imported successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Activates a bundled power profile in Windows
        /// </summary>
        /// <param name="profile">The profile to activate</param>
        /// <returns>True if activation was successful, false otherwise</returns>
        public async Task<bool> ActivateProfileAsync(BundledPowerProfile profile)
        {
            try
            {
                if (!profile.GuidInSystem.HasValue)
                {
                    // Try to import the profile first
                    bool imported = await ImportProfileAsync(profile);
                    if (!imported)
                    {
                        return false;
                    }
                }
                
                // Use powercfg.exe to activate the profile
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = $"/setactive {profile.GuidInSystem}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    _notificationService.ShowError($"Failed to activate profile: {error}");
                    return false;
                }
                
                // Update IsActive status for all profiles
                foreach (var p in _bundledProfiles)
                {
                    p.IsActive = p.GuidInSystem == profile.GuidInSystem;
                }
                
                _notificationService.ShowSuccess($"Profile '{profile.Name}' activated successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error activating profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Imports an external .pow file and adds it to the bundled profiles
        /// </summary>
        /// <param name="filePath">Path to the .pow file</param>
        /// <returns>The imported profile if successful, null otherwise</returns>
        public async Task<BundledPowerProfile> ImportExternalProfileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }
                
                // Create a new profile
                var profile = new BundledPowerProfile(filePath);
                
                // Copy the file to the bundled profiles directory
                string destFilePath = Path.Combine(_bundledProfilesPath, profile.FileName);
                File.Copy(filePath, destFilePath, true);
                
                // Update the profile's file path
                profile.FilePath = destFilePath;
                
                // Add the profile to the list
                _bundledProfiles.Add(profile);
                
                // Import the profile into Windows
                await ImportProfileAsync(profile);
                
                return profile;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing external profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Refreshes the status of bundled profiles (checks which ones are active)
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task RefreshProfileStatusAsync()
        {
            try
            {
                // Get all Windows power profiles
                var windowsProfiles = await GetWindowsPowerProfilesAsync();
                
                // Get the active profile GUID
                string activeProfileGuid = await GetActiveProfileGuidAsync();
                
                // Update each bundled profile
                foreach (var profile in _bundledProfiles)
                {
                    // Find the Windows profile that matches this bundled profile
                    var matchingProfile = windowsProfiles.FirstOrDefault(p => 
                        string.Equals(p.Value, profile.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (!string.IsNullOrEmpty(matchingProfile.Key))
                    {
                        // Update the profile's GUID
                        profile.GuidInSystem = Guid.Parse(matchingProfile.Key);
                        
                        // Update the active status
                        profile.IsActive = string.Equals(matchingProfile.Key, activeProfileGuid, 
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        // The profile is not in Windows
                        profile.GuidInSystem = null;
                        profile.IsActive = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing profile status: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets all power profiles imported into Windows
        /// </summary>
        /// <returns>A dictionary of profile GUIDs and their names</returns>
        public async Task<Dictionary<string, string>> GetWindowsPowerProfilesAsync()
        {
            var profiles = new Dictionary<string, string>();
            
            try
            {
                // Use powercfg.exe to list all power profiles
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/list",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                // Parse the output to extract profile GUIDs and names
                // Example output line: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                var regex = new Regex(@"Power Scheme GUID: ([0-9a-fA-F\-]+)\s+\((.*?)\)");
                var matches = regex.Matches(output);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        string guid = match.Groups[1].Value.Trim();
                        string name = match.Groups[2].Value.Trim();
                        profiles[guid] = name;
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error getting Windows power profiles: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// Gets the GUID of the active power profile
        /// </summary>
        /// <returns>The GUID of the active profile</returns>
        private async Task<string> GetActiveProfileGuidAsync()
        {
            try
            {
                // Use powercfg.exe to get the active power profile
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/getactivescheme",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                // Parse the output to extract the active profile GUID
                // Example output line: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *"
                var regex = new Regex(@"Power Scheme GUID: ([0-9a-fA-F\-]+)");
                var match = regex.Match(output);
                
                if (match.Success && match.Groups.Count >= 2)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error getting active power profile: {ex.Message}");
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Loads bundled power profiles from the application directory
        /// </summary>
        private async Task LoadBundledProfilesAsync()
        {
            try
            {
                _bundledProfiles.Clear();
                
                // Check if the attached assets directory exists (provided with the application)
                string attachedAssetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "attached_assets");
                if (Directory.Exists(attachedAssetsPath))
                {
                    // Copy all .pow files from the attached_assets directory to the PowerProfiles directory
                    foreach (var file in Directory.GetFiles(attachedAssetsPath, "*.pow"))
                    {
                        string destFile = Path.Combine(_bundledProfilesPath, Path.GetFileName(file));
                        if (!File.Exists(destFile))
                        {
                            File.Copy(file, destFile);
                        }
                    }
                }
                
                // Load all .pow files from the PowerProfiles directory
                if (Directory.Exists(_bundledProfilesPath))
                {
                    foreach (var file in Directory.GetFiles(_bundledProfilesPath, "*.pow"))
                    {
                        try
                        {
                            var profile = new BundledPowerProfile(file);
                            _bundledProfiles.Add(profile);
                        }
                        catch (Exception ex)
                        {
                            _notificationService.ShowError($"Error loading profile {file}: {ex.Message}");
                        }
                    }
                }
                
                // Refresh the status of all profiles
                await RefreshProfileStatusAsync();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading bundled profiles: {ex.Message}");
            }
        }
    }
}