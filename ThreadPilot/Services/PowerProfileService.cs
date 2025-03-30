using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of power profile service
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        // List of demo profiles
        private readonly List<BundledPowerProfile> _demoProfiles;
        
        // Process service
        private readonly IProcessService _processService;
        
        // System info service
        private readonly ISystemInfoService _systemInfoService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public PowerProfileService(
            IProcessService processService,
            ISystemInfoService systemInfoService,
            INotificationService notificationService)
        {
            _processService = processService;
            _systemInfoService = systemInfoService;
            _notificationService = notificationService;
            
            // Create demo profiles
            _demoProfiles = GenerateDemoProfiles();
        }
        
        /// <summary>
        /// Get all power profiles
        /// </summary>
        public IEnumerable<BundledPowerProfile> GetAllProfiles()
        {
            try
            {
                // In a real application, we would get the actual profiles
                // For now, we'll return demo data
                return _demoProfiles;
            }
            catch (Exception)
            {
                // In case of error, we'll return empty list
                return new List<BundledPowerProfile>();
            }
        }
        
        /// <summary>
        /// Get profile by ID
        /// </summary>
        public BundledPowerProfile? GetProfileById(int profileId)
        {
            try
            {
                // In a real application, we would get the actual profile
                // For now, we'll return demo data
                return _demoProfiles.FirstOrDefault(p => p.Id == profileId);
            }
            catch (Exception)
            {
                // In case of error, we'll return null
                return null;
            }
        }
        
        /// <summary>
        /// Save power profile
        /// </summary>
        public bool SaveProfile(BundledPowerProfile profile)
        {
            try
            {
                // In a real application, we would save the profile
                // For now, we'll just update our demo data
                
                var existingProfile = _demoProfiles.FirstOrDefault(p => p.Id == profile.Id);
                
                if (existingProfile != null)
                {
                    // Update existing profile
                    _demoProfiles.Remove(existingProfile);
                    _demoProfiles.Add(profile);
                }
                else
                {
                    // Add new profile
                    profile.Id = _demoProfiles.Count > 0 ? _demoProfiles.Max(p => p.Id) + 1 : 1;
                    _demoProfiles.Add(profile);
                }
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Delete power profile
        /// </summary>
        public bool DeleteProfile(int profileId)
        {
            try
            {
                // In a real application, we would delete the profile
                // For now, we'll just update our demo data
                
                var profile = _demoProfiles.FirstOrDefault(p => p.Id == profileId);
                
                if (profile == null)
                {
                    return false;
                }
                
                _demoProfiles.Remove(profile);
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Apply power profile
        /// </summary>
        public bool ApplyProfile(int profileId)
        {
            try
            {
                // In a real application, we would apply the profile
                // For now, we'll just log a message
                
                var profile = _demoProfiles.FirstOrDefault(p => p.Id == profileId);
                
                if (profile == null)
                {
                    return false;
                }
                
                Debug.WriteLine($"Applying profile: {profile.Name}");
                
                // Apply process affinity rules
                ApplyProcessAffinityRules(profile.ProcessAffinityRules);
                
                // Apply power profile
                if (!string.IsNullOrEmpty(profile.PowerProfileFilePath))
                {
                    Debug.WriteLine($"Applying power profile: {profile.PowerProfileFilePath}");
                }
                
                // Unpark CPU cores if needed
                if (profile.ShouldUnparkAllCores)
                {
                    _systemInfoService.UnparkAllCores();
                }
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Import power profile
        /// </summary>
        public BundledPowerProfile? ImportProfile(string filePath)
        {
            try
            {
                // In a real application, we would import the profile from the file
                // For now, we'll just return a demo profile
                
                // Check if file exists
                if (!File.Exists(filePath))
                {
                    return null;
                }
                
                // Get file extension
                var extension = Path.GetExtension(filePath);
                
                // Only support .pow files
                if (extension?.ToLower() != ".pow")
                {
                    return null;
                }
                
                // Get file name without extension
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                
                var id = _demoProfiles.Count > 0 ? _demoProfiles.Max(p => p.Id) + 1 : 1;
                
                var profile = new BundledPowerProfile
                {
                    Id = id,
                    Name = $"Imported - {fileName}",
                    Description = $"Imported profile from {filePath}",
                    IsEnabled = true,
                    WindowsPowerProfileGuid = Guid.NewGuid().ToString(),
                    WindowsPowerProfileName = fileName,
                    PowerProfileFilePath = filePath,
                    ShouldUnparkAllCores = true
                };
                
                // Add some default rules
                profile.ProcessAffinityRules.Add(new ProcessAffinityRule
                {
                    Id = 1,
                    Name = "High Priority Applications",
                    ProcessNamePattern = "chrome.exe|firefox.exe|msedge.exe",
                    AffinityMask = 0xFFFF, // All cores
                    Priority = ProcessPriority.AboveNormal,
                    IsEnabled = true,
                    RulePriority = 100
                });
                
                profile.ProcessAffinityRules.Add(new ProcessAffinityRule
                {
                    Id = 2,
                    Name = "Background Applications",
                    ProcessNamePattern = "spotify.exe|discord.exe|slack.exe",
                    AffinityMask = 0x55, // Every other core
                    Priority = ProcessPriority.BelowNormal,
                    IsEnabled = true,
                    RulePriority = 50
                });
                
                // Add profile to the list
                _demoProfiles.Add(profile);
                
                return profile;
            }
            catch (Exception)
            {
                // In case of error, we'll return null
                return null;
            }
        }
        
        /// <summary>
        /// Export power profile
        /// </summary>
        public bool ExportProfile(int profileId, string filePath)
        {
            try
            {
                // In a real application, we would export the profile to the file
                // For now, we'll just log a message
                
                var profile = _demoProfiles.FirstOrDefault(p => p.Id == profileId);
                
                if (profile == null)
                {
                    return false;
                }
                
                Debug.WriteLine($"Exporting profile: {profile.Name} to {filePath}");
                
                // Check if directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create an empty file (in a real application, we would write the profile data)
                File.WriteAllText(filePath, "");
                
                return true;
            }
            catch (Exception)
            {
                // In case of error, we'll return false
                return false;
            }
        }
        
        /// <summary>
        /// Generate demo profiles
        /// </summary>
        private List<BundledPowerProfile> GenerateDemoProfiles()
        {
            var profiles = new List<BundledPowerProfile>();
            
            // Profile 1: Balanced
            var profile1 = new BundledPowerProfile
            {
                Id = 1,
                Name = "Balanced",
                Description = "Balanced profile for everyday use",
                IsEnabled = true,
                WindowsPowerProfileGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                WindowsPowerProfileName = "Balanced",
                ShouldUnparkAllCores = false
            };
            
            profile1.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 1,
                Name = "High Priority Applications",
                ProcessNamePattern = "chrome.exe|firefox.exe|msedge.exe",
                AffinityMask = 0xFFFF, // All cores
                Priority = ProcessPriority.Normal,
                IsEnabled = true,
                RulePriority = 100
            });
            
            profile1.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 2,
                Name = "Background Applications",
                ProcessNamePattern = "spotify.exe|discord.exe|slack.exe",
                AffinityMask = 0x55, // Every other core
                Priority = ProcessPriority.BelowNormal,
                IsEnabled = true,
                RulePriority = 50
            });
            
            profiles.Add(profile1);
            
            // Profile 2: Performance
            var profile2 = new BundledPowerProfile
            {
                Id = 2,
                Name = "Performance",
                Description = "High performance profile for gaming and demanding applications",
                IsEnabled = true,
                WindowsPowerProfileGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                WindowsPowerProfileName = "High performance",
                ShouldUnparkAllCores = true
            };
            
            profile2.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 3,
                Name = "Games",
                ProcessNamePattern = "steam.exe|game.exe|EpicGamesLauncher.exe",
                AffinityMask = 0xFFFF, // All cores
                Priority = ProcessPriority.High,
                IsEnabled = true,
                RulePriority = 100
            });
            
            profile2.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 4,
                Name = "Background Applications",
                ProcessNamePattern = "spotify.exe|discord.exe|slack.exe",
                AffinityMask = 0x55, // Every other core
                Priority = ProcessPriority.BelowNormal,
                IsEnabled = true,
                RulePriority = 50
            });
            
            profiles.Add(profile2);
            
            // Profile 3: Power Saver
            var profile3 = new BundledPowerProfile
            {
                Id = 3,
                Name = "Power Saver",
                Description = "Power saving profile for extended battery life",
                IsEnabled = true,
                WindowsPowerProfileGuid = "a1841308-3541-4fab-bc81-f71556f20b4a",
                WindowsPowerProfileName = "Power saver",
                ShouldUnparkAllCores = false
            };
            
            profile3.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 5,
                Name = "All Applications",
                ProcessNamePattern = ".*",
                AffinityMask = 0x55, // Every other core
                Priority = ProcessPriority.BelowNormal,
                IsEnabled = true,
                RulePriority = 100
            });
            
            profiles.Add(profile3);
            
            // Profile 4: Extreme Performance
            var profile4 = new BundledPowerProfile
            {
                Id = 4,
                Name = "Extreme Performance",
                Description = "Maximum performance profile with custom power settings",
                IsEnabled = true,
                WindowsPowerProfileGuid = Guid.NewGuid().ToString(),
                WindowsPowerProfileName = "Extreme Performance",
                PowerProfileFilePath = "attached_assets/FrameSyncBoost.pow",
                ShouldUnparkAllCores = true
            };
            
            profile4.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 6,
                Name = "All Applications",
                ProcessNamePattern = ".*",
                AffinityMask = 0xFFFF, // All cores
                Priority = ProcessPriority.Normal,
                IsEnabled = true,
                RulePriority = 50
            });
            
            profile4.ProcessAffinityRules.Add(new ProcessAffinityRule
            {
                Id = 7,
                Name = "Games",
                ProcessNamePattern = "steam.exe|game.exe|EpicGamesLauncher.exe",
                AffinityMask = 0xFFFF, // All cores
                Priority = ProcessPriority.Realtime,
                IsEnabled = true,
                RulePriority = 100
            });
            
            profiles.Add(profile4);
            
            return profiles;
        }
        
        /// <summary>
        /// Apply process affinity rules
        /// </summary>
        private void ApplyProcessAffinityRules(IEnumerable<ProcessAffinityRule> rules)
        {
            // Get all processes
            var processes = _processService.GetAllProcesses();
            
            // Get enabled rules sorted by priority (highest first)
            var enabledRules = rules.Where(r => r.IsEnabled).OrderByDescending(r => r.RulePriority).ToList();
            
            foreach (var process in processes)
            {
                // Skip critical processes
                if (process.IsCritical)
                {
                    continue;
                }
                
                // Find a matching rule
                foreach (var rule in enabledRules)
                {
                    var nameMatches = !string.IsNullOrEmpty(rule.ProcessNamePattern) &&
                                      process.Name != null &&
                                      System.Text.RegularExpressions.Regex.IsMatch(process.Name, rule.ProcessNamePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    var pathMatches = !string.IsNullOrEmpty(rule.ProcessPathPattern) &&
                                      process.Path != null &&
                                      System.Text.RegularExpressions.Regex.IsMatch(process.Path, rule.ProcessPathPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    if (nameMatches || pathMatches)
                    {
                        // Apply the rule
                        _processService.SetProcessAffinity(process.Id, rule.AffinityMask);
                        _processService.SetProcessPriority(process.Id, rule.Priority);
                        
                        // We've found a matching rule, so we can move on to the next process
                        break;
                    }
                }
            }
        }
    }
}