using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of power profile service
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private readonly INotificationService _notificationService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IProcessService _processService;
        
        private List<PowerProfile> _profiles;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public PowerProfileService()
        {
            _notificationService = ServiceLocator.Get<INotificationService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _processService = ServiceLocator.Get<IProcessService>();
            
            _profiles = new List<PowerProfile>();
            LoadDefaultProfiles();
        }
        
        /// <summary>
        /// Get all power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        public List<PowerProfile> GetProfiles()
        {
            return _profiles;
        }
        
        /// <summary>
        /// Get a power profile by name
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>Power profile or null if not found</returns>
        public PowerProfile? GetProfile(string name)
        {
            return _profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Create a new power profile
        /// </summary>
        /// <param name="profile">Profile to create</param>
        /// <returns>True if successful</returns>
        public bool CreateProfile(PowerProfile profile)
        {
            try
            {
                // Check if profile already exists
                if (_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _notificationService.ShowError($"A profile with the name '{profile.Name}' already exists.");
                    return false;
                }
                
                // Add the profile
                _profiles.Add(profile);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating profile: {ex.Message}");
                _notificationService.ShowError($"Error creating profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Update a power profile
        /// </summary>
        /// <param name="profile">Updated profile</param>
        /// <returns>True if successful</returns>
        public bool UpdateProfile(PowerProfile profile)
        {
            try
            {
                // Find the profile
                int index = _profiles.FindIndex(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    _notificationService.ShowError($"Profile '{profile.Name}' not found.");
                    return false;
                }
                
                // Update the profile
                _profiles[index] = profile;
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating profile: {ex.Message}");
                _notificationService.ShowError($"Error updating profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>True if successful</returns>
        public bool DeleteProfile(string name)
        {
            try
            {
                // Find the profile
                int index = _profiles.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    _notificationService.ShowError($"Profile '{name}' not found.");
                    return false;
                }
                
                // Don't delete default profiles
                if (_profiles[index].IsDefault)
                {
                    _notificationService.ShowError("Default profiles cannot be deleted.");
                    return false;
                }
                
                // Remove the profile
                _profiles.RemoveAt(index);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting profile: {ex.Message}");
                _notificationService.ShowError($"Error deleting profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">Profile to apply</param>
        /// <returns>True if successful</returns>
        public bool ApplyProfile(PowerProfile profile)
        {
            try
            {
                // Set Windows power scheme if specified
                if (!string.IsNullOrEmpty(profile.WindowsPowerScheme))
                {
                    ApplyWindowsPowerScheme(profile.WindowsPowerScheme);
                }
                
                // Apply process rules
                int affectedProcesses = 0;
                foreach (var rule in profile.ProcessRules)
                {
                    affectedProcesses += _processService.ApplyAffinityRule(rule);
                }
                
                _notificationService.ShowSuccess(
                    $"Applied profile '{profile.Name}' to {affectedProcesses} processes.");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying profile: {ex.Message}");
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <returns>Imported profile or null if failed</returns>
        public PowerProfile? ImportProfile()
        {
            try
            {
                // Show open file dialog
                string? fileName = _fileDialogService.ShowOpenFileDialog(
                    "Import Power Profile",
                    "Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*",
                    ".pow");
                
                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }
                
                // Read the file
                PowerProfile? profile = ReadProfileFromFile(fileName);
                
                if (profile != null)
                {
                    // Add the profile to the list
                    if (!_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _profiles.Add(profile);
                        _notificationService.ShowSuccess($"Imported profile '{profile.Name}'.");
                    }
                    else
                    {
                        // Add a suffix to make the name unique
                        int suffix = 1;
                        string originalName = profile.Name;
                        while (_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            profile.Name = $"{originalName} ({suffix})";
                            suffix++;
                        }
                        
                        _profiles.Add(profile);
                        _notificationService.ShowSuccess(
                            $"Imported profile as '{profile.Name}' (renamed to avoid conflict).");
                    }
                }
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing profile: {ex.Message}");
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">Profile to export</param>
        /// <returns>True if successful</returns>
        public bool ExportProfile(PowerProfile profile)
        {
            try
            {
                // Show save file dialog
                string? fileName = _fileDialogService.ShowSaveFileDialog(
                    "Export Power Profile",
                    "Power Profile Files (*.pow)|*.pow|All Files (*.*)|*.*",
                    SanitizeFileName(profile.Name) + ".pow");
                
                if (string.IsNullOrEmpty(fileName))
                {
                    return false;
                }
                
                // Write the file
                WriteProfileToFile(profile, fileName);
                
                _notificationService.ShowSuccess($"Exported profile to {fileName}.");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting profile: {ex.Message}");
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Make a copy of a power profile
        /// </summary>
        /// <param name="profile">Profile to copy</param>
        /// <returns>Copied profile</returns>
        public PowerProfile CloneProfile(PowerProfile profile)
        {
            var clone = new PowerProfile
            {
                Name = $"{profile.Name} (Copy)",
                Description = profile.Description,
                WindowsPowerScheme = profile.WindowsPowerScheme,
                IsDefault = false
            };
            
            // Clone the rules
            foreach (var rule in profile.ProcessRules)
            {
                var clonedRule = new ProcessAffinityRule
                {
                    ProcessNamePattern = rule.ProcessNamePattern,
                    Priority = rule.Priority,
                    IsExcludeList = rule.IsExcludeList,
                    AffinityMask = rule.AffinityMask
                };
                
                if (rule.CoreList != null)
                {
                    clonedRule.CoreList = new List<int>(rule.CoreList);
                }
                
                clone.ProcessRules.Add(clonedRule);
            }
            
            return clone;
        }
        
        /// <summary>
        /// Load default profiles
        /// </summary>
        private void LoadDefaultProfiles()
        {
            try
            {
                // Add default profiles
                _profiles.Add(CreatePowerSaverProfile());
                _profiles.Add(CreateBalancedProfile());
                _profiles.Add(CreateHighPerformanceProfile());
                _profiles.Add(CreateGamingProfile());
                
                // Load profiles from attached assets if available
                LoadProfilesFromAttachedAssets();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading default profiles: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load profiles from attached assets
        /// </summary>
        private void LoadProfilesFromAttachedAssets()
        {
            try
            {
                // Check the attached_assets directory
                string assetsDirectory = "attached_assets";
                if (Directory.Exists(assetsDirectory))
                {
                    // Find all .pow files
                    var files = Directory.GetFiles(assetsDirectory, "*.pow");
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            PowerProfile? profile = ReadProfileFromFile(file);
                            if (profile != null && !_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                _profiles.Add(profile);
                                Debug.WriteLine($"Loaded profile from asset: {profile.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading profile from asset {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profiles from assets: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Create a power saver profile
        /// </summary>
        /// <returns>Power saver profile</returns>
        private PowerProfile CreatePowerSaverProfile()
        {
            var profile = new PowerProfile
            {
                Name = "Power Saver",
                Description = "Optimizes for energy efficiency with minimal performance impact.",
                WindowsPowerScheme = "Power saver",
                IsDefault = true
            };
            
            // Add rules
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "chrome.exe",
                Priority = ProcessPriority.BelowNormal,
                CoreList = new List<int> { 0, 1 } // Limit to first two cores
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "firefox.exe",
                Priority = ProcessPriority.BelowNormal,
                CoreList = new List<int> { 0, 1 } // Limit to first two cores
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "MicrosoftEdge.exe",
                Priority = ProcessPriority.BelowNormal,
                CoreList = new List<int> { 0, 1 } // Limit to first two cores
            });
            
            return profile;
        }
        
        /// <summary>
        /// Create a balanced profile
        /// </summary>
        /// <returns>Balanced profile</returns>
        private PowerProfile CreateBalancedProfile()
        {
            var profile = new PowerProfile
            {
                Name = "Balanced",
                Description = "Balances performance and power consumption for everyday use.",
                WindowsPowerScheme = "Balanced",
                IsDefault = true
            };
            
            // Add rules
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "chrome.exe",
                Priority = ProcessPriority.Normal
                // No core restriction
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*virus*.exe",
                Priority = ProcessPriority.Idle,
                IsExcludeList = true // This is a negative pattern
            });
            
            return profile;
        }
        
        /// <summary>
        /// Create a high performance profile
        /// </summary>
        /// <returns>High performance profile</returns>
        private PowerProfile CreateHighPerformanceProfile()
        {
            var profile = new PowerProfile
            {
                Name = "High Performance",
                Description = "Maximizes system performance for demanding applications.",
                WindowsPowerScheme = "High performance",
                IsDefault = true
            };
            
            // Add rules
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "chrome.exe",
                Priority = ProcessPriority.AboveNormal
                // No core restriction
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "teams.exe",
                Priority = ProcessPriority.AboveNormal
                // No core restriction
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "outlook.exe",
                Priority = ProcessPriority.AboveNormal
                // No core restriction
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*virus*.exe",
                Priority = ProcessPriority.Idle,
                IsExcludeList = true // This is a negative pattern
            });
            
            return profile;
        }
        
        /// <summary>
        /// Create a gaming profile
        /// </summary>
        /// <returns>Gaming profile</returns>
        private PowerProfile CreateGamingProfile()
        {
            var profile = new PowerProfile
            {
                Name = "Gaming",
                Description = "Optimizes for gaming with maximum performance for games and background throttling.",
                WindowsPowerScheme = "Ultimate performance",
                IsDefault = true
            };
            
            // Add rules for common game processes
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*.exe",
                Priority = ProcessPriority.Normal,
                IsExcludeList = false // Apply to all processes by default
            });
            
            // Higher priority for games
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "game*.exe",
                Priority = ProcessPriority.High
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*game.exe",
                Priority = ProcessPriority.High
            });
            
            // Common game executables
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "steam.exe",
                Priority = ProcessPriority.AboveNormal
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "epicgameslauncher.exe",
                Priority = ProcessPriority.AboveNormal
            });
            
            // Lower priority for background apps
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "chrome.exe",
                Priority = ProcessPriority.BelowNormal
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "firefox.exe",
                Priority = ProcessPriority.BelowNormal
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "MicrosoftEdge.exe",
                Priority = ProcessPriority.BelowNormal
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "teams.exe",
                Priority = ProcessPriority.BelowNormal
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "outlook.exe",
                Priority = ProcessPriority.BelowNormal
            });
            
            // Very low priority for unnecessary background services
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*update*.exe",
                Priority = ProcessPriority.Idle
            });
            
            profile.ProcessRules.Add(new ProcessAffinityRule
            {
                ProcessNamePattern = "*virus*.exe",
                Priority = ProcessPriority.Idle
            });
            
            return profile;
        }
        
        /// <summary>
        /// Apply a Windows power scheme
        /// </summary>
        /// <param name="schemeName">Scheme name</param>
        private void ApplyWindowsPowerScheme(string schemeName)
        {
            try
            {
                // PowerCfg.exe is used to manage power schemes
                // This is simplified for demo purposes - in a real app
                // we would use PInvoke to call the power API directly
                
                // Map friendly names to GUIDs (these would be retrieved properly in a real app)
                string schemeGuid;
                
                switch (schemeName.ToLower())
                {
                    case "power saver":
                        schemeGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
                        break;
                    case "balanced":
                        schemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                        break;
                    case "high performance":
                        schemeGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
                        break;
                    case "ultimate performance":
                        schemeGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
                        break;
                    default:
                        // If unknown, use Balanced
                        schemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                        break;
                }
                
                // In a real app, we would call PowerCfg.exe or use PInvoke
                Debug.WriteLine($"Applying power scheme: {schemeName} (GUID: {schemeGuid})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying power scheme: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Read a power profile from a file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Power profile</returns>
        private PowerProfile? ReadProfileFromFile(string fileName)
        {
            try
            {
                // In a real implementation, this would deserialize a binary or JSON file
                // For this demo, we'll generate a profile based on the file name
                
                string profileName = Path.GetFileNameWithoutExtension(fileName);
                
                var profile = new PowerProfile
                {
                    Name = profileName,
                    Description = $"Imported profile from {Path.GetFileName(fileName)}",
                    WindowsPowerScheme = "Balanced"
                };
                
                // Add some sample rules
                profile.ProcessRules.Add(new ProcessAffinityRule
                {
                    ProcessNamePattern = "chrome.exe",
                    Priority = ProcessPriority.Normal
                });
                
                profile.ProcessRules.Add(new ProcessAffinityRule
                {
                    ProcessNamePattern = "game*.exe",
                    Priority = ProcessPriority.High
                });
                
                return profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading profile from file: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Write a power profile to a file
        /// </summary>
        /// <param name="profile">Profile to write</param>
        /// <param name="fileName">File name</param>
        private void WriteProfileToFile(PowerProfile profile, string fileName)
        {
            try
            {
                // In a real implementation, this would serialize the profile to a binary or JSON file
                // For this demo, we'll just write some placeholder content
                
                string fileContent = $"ThreadPilot Power Profile: {profile.Name}\r\n"
                                  + $"Description: {profile.Description}\r\n"
                                  + $"Power Scheme: {profile.WindowsPowerScheme}\r\n"
                                  + $"Rules Count: {profile.ProcessRules.Count}\r\n";
                
                foreach (var rule in profile.ProcessRules)
                {
                    fileContent += $"- Rule: {rule.ProcessNamePattern}, Priority: {rule.Priority}, "
                                + $"Exclude: {rule.IsExcludeList}, "
                                + $"Cores: {(rule.CoreList != null ? string.Join(",", rule.CoreList) : "All")}\r\n";
                }
                
                File.WriteAllText(fileName, fileContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing profile to file: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Sanitize a file name
        /// </summary>
        /// <param name="fileName">File name to sanitize</param>
        /// <returns>Sanitized file name</returns>
        private string SanitizeFileName(string fileName)
        {
            // Remove invalid file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "_");
            }
            
            return fileName;
        }
    }
}