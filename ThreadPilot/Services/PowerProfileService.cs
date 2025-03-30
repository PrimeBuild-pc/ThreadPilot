using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of power profile operations
    /// </summary>
    public class PowerProfileService : IPowerProfileService
    {
        private readonly List<PowerProfile> _profiles = new List<PowerProfile>();
        private readonly string _bundledProfilesPath;
        private readonly string _userProfilesPath;
        
        /// <summary>
        /// Occurs when the active power profile is changed
        /// </summary>
        public event EventHandler<PowerProfile> ActiveProfileChanged;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerProfileService"/> class
        /// </summary>
        public PowerProfileService()
        {
            // Define paths
            _bundledProfilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BundledProfiles");
            _userProfilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PowerProfiles");
            
            // Create directories if they don't exist
            Directory.CreateDirectory(_bundledProfilesPath);
            Directory.CreateDirectory(_userProfilesPath);
            
            // Load profiles
            LoadProfiles();
        }
        
        /// <summary>
        /// Gets a power profile by its ID
        /// </summary>
        /// <param name="profileId">The profile ID</param>
        /// <returns>The power profile or null if not found</returns>
        public PowerProfile GetProfile(Guid profileId)
        {
            lock (_profiles)
            {
                return _profiles.FirstOrDefault(p => p.Id == profileId);
            }
        }
        
        /// <summary>
        /// Gets a power profile by its name
        /// </summary>
        /// <param name="profileName">The profile name</param>
        /// <returns>The power profile or null if not found</returns>
        public PowerProfile GetProfileByName(string profileName)
        {
            lock (_profiles)
            {
                return _profiles.FirstOrDefault(p => p.Name == profileName);
            }
        }
        
        /// <summary>
        /// Gets all power profiles
        /// </summary>
        /// <returns>The list of all power profiles</returns>
        public List<PowerProfile> GetAllProfiles()
        {
            lock (_profiles)
            {
                return _profiles.ToList();
            }
        }
        
        /// <summary>
        /// Gets the active power profile
        /// </summary>
        /// <returns>The active power profile or null if none is active</returns>
        public PowerProfile GetActiveProfile()
        {
            lock (_profiles)
            {
                return _profiles.FirstOrDefault(p => p.IsActive);
            }
        }
        
        /// <summary>
        /// Creates a new power profile
        /// </summary>
        /// <param name="profile">The power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool CreateProfile(PowerProfile profile)
        {
            if (profile == null)
                return false;
                
            lock (_profiles)
            {
                // Check if a profile with the same name already exists
                if (_profiles.Any(p => p.Name == profile.Name))
                    return false;
                    
                // Set creation and modified dates
                profile.CreationDate = DateTime.Now;
                profile.LastModifiedDate = DateTime.Now;
                
                // Add to profiles
                _profiles.Add(profile);
                
                // Save the profile
                SaveProfile(profile);
                
                return true;
            }
        }
        
        /// <summary>
        /// Updates a power profile
        /// </summary>
        /// <param name="profileId">The profile ID</param>
        /// <param name="profile">The updated power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool UpdateProfile(Guid profileId, PowerProfile profile)
        {
            if (profile == null)
                return false;
                
            lock (_profiles)
            {
                // Find the profile
                var existingProfile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (existingProfile == null)
                    return false;
                    
                // Check if the profile is a system profile or bundled
                if (existingProfile.IsSystemProfile || existingProfile.IsBundled)
                    return false;
                    
                // Update properties
                existingProfile.Name = profile.Name;
                existingProfile.Description = profile.Description;
                existingProfile.Settings = new Dictionary<string, string>(profile.Settings);
                existingProfile.IsGamingOptimized = profile.IsGamingOptimized;
                existingProfile.IsBatteryOptimized = profile.IsBatteryOptimized;
                existingProfile.IsThermalOptimized = profile.IsThermalOptimized;
                existingProfile.UsesDynamicThreadAllocation = profile.UsesDynamicThreadAllocation;
                existingProfile.UsesPerformanceCoresForApps = profile.UsesPerformanceCoresForApps;
                existingProfile.PerformanceCoreApps = new List<string>(profile.PerformanceCoreApps);
                existingProfile.AffinityRules = new List<ProcessAffinityRule>();
                
                // Clone the affinity rules
                foreach (var rule in profile.AffinityRules)
                {
                    existingProfile.AffinityRules.Add(rule.Clone());
                }
                
                existingProfile.MaxCpuPower = profile.MaxCpuPower;
                existingProfile.MaxCpuTemperature = profile.MaxCpuTemperature;
                existingProfile.MinCpuFrequency = profile.MinCpuFrequency;
                existingProfile.MaxCpuFrequency = profile.MaxCpuFrequency;
                existingProfile.ParkEfficiencyCores = profile.ParkEfficiencyCores;
                existingProfile.UseHyperthreading = profile.UseHyperthreading;
                existingProfile.PerformanceCoresOffset = profile.PerformanceCoresOffset;
                existingProfile.EfficiencyCoresOffset = profile.EfficiencyCoresOffset;
                existingProfile.PowerSchemeGuid = profile.PowerSchemeGuid;
                existingProfile.LastModifiedDate = DateTime.Now;
                
                // Save the profile
                SaveProfile(existingProfile);
                
                return true;
            }
        }
        
        /// <summary>
        /// Deletes a power profile
        /// </summary>
        /// <param name="profileId">The profile ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool DeleteProfile(Guid profileId)
        {
            lock (_profiles)
            {
                // Find the profile
                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                    return false;
                    
                // Check if the profile is a system profile, bundled, or active
                if (profile.IsSystemProfile || profile.IsBundled || profile.IsActive)
                    return false;
                    
                // Remove from profiles
                _profiles.Remove(profile);
                
                // Delete the file
                if (!string.IsNullOrWhiteSpace(profile.FilePath) && File.Exists(profile.FilePath))
                {
                    try
                    {
                        File.Delete(profile.FilePath);
                    }
                    catch
                    {
                        // Ignore exceptions
                    }
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Sets the active power profile
        /// </summary>
        /// <param name="profileId">The profile ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetActiveProfile(Guid profileId)
        {
            lock (_profiles)
            {
                // Find the profile
                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
                if (profile == null)
                    return false;
                    
                // Check if already active
                if (profile.IsActive)
                    return true;
                    
                // Deactivate current active profile
                var activeProfile = _profiles.FirstOrDefault(p => p.IsActive);
                if (activeProfile != null)
                {
                    activeProfile.IsActive = false;
                    SaveProfile(activeProfile);
                }
                
                // Activate new profile
                profile.IsActive = true;
                SaveProfile(profile);
                
                // Raise event
                ActiveProfileChanged?.Invoke(this, profile);
                
                // Apply the profile settings (this would be implemented with actual hardware API calls)
                ApplyProfileSettings(profile);
                
                return true;
            }
        }
        
        /// <summary>
        /// Imports a power profile from a file
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>The imported power profile or null if import failed</returns>
        public PowerProfile ImportProfile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;
                    
                // Parse file based on extension
                var extension = Path.GetExtension(filePath).ToLower();
                
                PowerProfile profile = null;
                
                if (extension == ".pow")
                {
                    profile = ParsePowFile(filePath);
                }
                else if (extension == ".json")
                {
                    profile = ParseJsonFile(filePath);
                }
                else
                {
                    return null;
                }
                
                if (profile == null)
                    return null;
                    
                // Generate a new ID
                profile.Id = Guid.NewGuid();
                
                // Check if a profile with the same name already exists
                var existingName = profile.Name;
                var counter = 1;
                
                lock (_profiles)
                {
                    while (_profiles.Any(p => p.Name == profile.Name))
                    {
                        profile.Name = $"{existingName} ({counter})";
                        counter++;
                    }
                    
                    // Set import properties
                    profile.IsBundled = false;
                    profile.IsSystemProfile = false;
                    profile.IsActive = false;
                    profile.CreationDate = DateTime.Now;
                    profile.LastModifiedDate = DateTime.Now;
                    
                    // Save to user profiles
                    var newFilePath = Path.Combine(_userProfilesPath, $"{Guid.NewGuid()}.pow");
                    profile.FilePath = newFilePath;
                    
                    // Save the profile
                    SaveProfile(profile);
                    
                    // Add to profiles
                    _profiles.Add(profile);
                    
                    return profile;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to import profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Exports a power profile to a file
        /// </summary>
        /// <param name="profileId">The profile ID</param>
        /// <param name="filePath">The file path</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ExportProfile(Guid profileId, string filePath)
        {
            try
            {
                // Find the profile
                var profile = GetProfile(profileId);
                if (profile == null)
                    return false;
                    
                // Copy the profile file
                if (!string.IsNullOrWhiteSpace(profile.FilePath) && File.Exists(profile.FilePath))
                {
                    File.Copy(profile.FilePath, filePath, true);
                    return true;
                }
                
                // If no file exists, create a new one
                var extension = Path.GetExtension(filePath).ToLower();
                
                if (extension == ".pow")
                {
                    return ExportToPowFile(profile, filePath);
                }
                else if (extension == ".json")
                {
                    return ExportToJsonFile(profile, filePath);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export profile: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Loads the bundled power profiles
        /// </summary>
        /// <returns>The list of bundled power profiles</returns>
        public List<PowerProfile> LoadBundledProfiles()
        {
            var bundledProfiles = new List<PowerProfile>();
            
            try
            {
                if (!Directory.Exists(_bundledProfilesPath))
                    return bundledProfiles;
                    
                var files = Directory.GetFiles(_bundledProfilesPath, "*.pow");
                
                foreach (var file in files)
                {
                    try
                    {
                        var profile = ParsePowFile(file);
                        
                        if (profile != null)
                        {
                            profile.IsBundled = true;
                            bundledProfiles.Add(profile);
                        }
                    }
                    catch
                    {
                        // Skip files that cannot be parsed
                    }
                }
                
                return bundledProfiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load bundled profiles: {ex.Message}");
                return bundledProfiles;
            }
        }
        
        /// <summary>
        /// Resets the power profile settings to the system defaults
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool ResetToSystemDefaults()
        {
            try
            {
                // This would be implemented with actual hardware API calls
                // For now, just simulate success
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private void LoadProfiles()
        {
            try
            {
                lock (_profiles)
                {
                    _profiles.Clear();
                    
                    // Load bundled profiles
                    var bundledProfiles = LoadBundledProfiles();
                    _profiles.AddRange(bundledProfiles);
                    
                    // Load user profiles
                    if (Directory.Exists(_userProfilesPath))
                    {
                        var files = Directory.GetFiles(_userProfilesPath, "*.pow");
                        
                        foreach (var file in files)
                        {
                            try
                            {
                                var profile = ParsePowFile(file);
                                
                                if (profile != null)
                                {
                                    _profiles.Add(profile);
                                }
                            }
                            catch
                            {
                                // Skip files that cannot be parsed
                            }
                        }
                    }
                    
                    // Create default profiles if none exist
                    if (_profiles.Count == 0)
                    {
                        CreateDefaultProfiles();
                    }
                    
                    // Make sure there is an active profile
                    if (!_profiles.Any(p => p.IsActive))
                    {
                        var defaultProfile = _profiles.FirstOrDefault();
                        if (defaultProfile != null)
                        {
                            defaultProfile.IsActive = true;
                            SaveProfile(defaultProfile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load profiles: {ex.Message}");
            }
        }
        
        private void CreateDefaultProfiles()
        {
            // Create a balanced profile
            var balancedProfile = new PowerProfile
            {
                Name = "Balanced",
                Description = "Balanced performance and power saving",
                IsSystemProfile = true,
                IsActive = true,
                UseHyperthreading = true,
                MaxCpuPower = 0, // Unlimited
                MaxCpuTemperature = 0, // Unlimited
                MinCpuFrequency = 0, // Auto
                MaxCpuFrequency = 0 // Auto
            };
            
            // Create a performance profile
            var performanceProfile = new PowerProfile
            {
                Name = "Performance",
                Description = "Maximum performance",
                IsSystemProfile = true,
                IsActive = false,
                UseHyperthreading = true,
                IsGamingOptimized = true,
                MaxCpuPower = 0, // Unlimited
                MaxCpuTemperature = 0, // Unlimited
                MinCpuFrequency = 0, // Auto
                MaxCpuFrequency = 0 // Auto
            };
            
            // Create a power saver profile
            var powerSaverProfile = new PowerProfile
            {
                Name = "Power Saver",
                Description = "Maximum power saving",
                IsSystemProfile = true,
                IsActive = false,
                UseHyperthreading = true,
                IsBatteryOptimized = true,
                MaxCpuPower = 45, // Limit power
                MaxCpuTemperature = 70, // Limit temperature
                MinCpuFrequency = 0, // Auto
                MaxCpuFrequency = 3000 // Limit frequency
            };
            
            // Add the profiles
            _profiles.Add(balancedProfile);
            _profiles.Add(performanceProfile);
            _profiles.Add(powerSaverProfile);
            
            // Save the profiles
            SaveProfile(balancedProfile);
            SaveProfile(performanceProfile);
            SaveProfile(powerSaverProfile);
        }
        
        private void SaveProfile(PowerProfile profile)
        {
            try
            {
                if (profile == null)
                    return;
                    
                // Determine file path
                var filePath = profile.FilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    var directory = profile.IsBundled ? _bundledProfilesPath : _userProfilesPath;
                    filePath = Path.Combine(directory, $"{profile.Id}.pow");
                    profile.FilePath = filePath;
                }
                
                // Save the profile
                if (Path.GetExtension(filePath).ToLower() == ".pow")
                {
                    ExportToPowFile(profile, filePath);
                }
                else
                {
                    ExportToJsonFile(profile, filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save profile: {ex.Message}");
            }
        }
        
        private PowerProfile ParsePowFile(string filePath)
        {
            try
            {
                var profile = new PowerProfile
                {
                    FilePath = filePath,
                    Name = Path.GetFileNameWithoutExtension(filePath)
                };
                
                var document = XDocument.Load(filePath);
                var root = document.Root;
                
                if (root != null)
                {
                    // Parse basic properties
                    profile.Id = GetGuidAttribute(root, "id");
                    profile.Name = GetStringElement(root, "Name") ?? profile.Name;
                    profile.Description = GetStringElement(root, "Description");
                    profile.IsActive = GetBoolElement(root, "IsActive");
                    profile.IsSystemProfile = GetBoolElement(root, "IsSystemProfile");
                    profile.IsBundled = GetBoolElement(root, "IsBundled");
                    profile.CreationDate = GetDateTimeElement(root, "CreationDate");
                    profile.LastModifiedDate = GetDateTimeElement(root, "LastModifiedDate");
                    
                    // Parse optimization flags
                    profile.IsGamingOptimized = GetBoolElement(root, "IsGamingOptimized");
                    profile.IsBatteryOptimized = GetBoolElement(root, "IsBatteryOptimized");
                    profile.IsThermalOptimized = GetBoolElement(root, "IsThermalOptimized");
                    profile.UsesDynamicThreadAllocation = GetBoolElement(root, "UsesDynamicThreadAllocation");
                    profile.UsesPerformanceCoresForApps = GetBoolElement(root, "UsesPerformanceCoresForApps");
                    
                    // Parse CPU settings
                    profile.MaxCpuPower = GetIntElement(root, "MaxCpuPower");
                    profile.MaxCpuTemperature = GetIntElement(root, "MaxCpuTemperature");
                    profile.MinCpuFrequency = GetIntElement(root, "MinCpuFrequency");
                    profile.MaxCpuFrequency = GetIntElement(root, "MaxCpuFrequency");
                    profile.ParkEfficiencyCores = GetBoolElement(root, "ParkEfficiencyCores");
                    profile.UseHyperthreading = GetBoolElement(root, "UseHyperthreading", true);
                    profile.PerformanceCoresOffset = GetIntElement(root, "PerformanceCoresOffset");
                    profile.EfficiencyCoresOffset = GetIntElement(root, "EfficiencyCoresOffset");
                    profile.PowerSchemeGuid = GetStringElement(root, "PowerSchemeGuid");
                    
                    // Parse performance core apps
                    var appsElement = root.Element("PerformanceCoreApps");
                    if (appsElement != null)
                    {
                        foreach (var appElement in appsElement.Elements("App"))
                        {
                            var appName = appElement.Value;
                            if (!string.IsNullOrWhiteSpace(appName))
                            {
                                profile.PerformanceCoreApps.Add(appName);
                            }
                        }
                    }
                    
                    // Parse settings
                    var settingsElement = root.Element("Settings");
                    if (settingsElement != null)
                    {
                        foreach (var settingElement in settingsElement.Elements("Setting"))
                        {
                            var key = settingElement.Attribute("key")?.Value;
                            var value = settingElement.Attribute("value")?.Value;
                            
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                profile.Settings[key] = value ?? string.Empty;
                            }
                        }
                    }
                    
                    // Parse affinity rules
                    var rulesElement = root.Element("AffinityRules");
                    if (rulesElement != null)
                    {
                        foreach (var ruleElement in rulesElement.Elements("Rule"))
                        {
                            var rule = new ProcessAffinityRule
                            {
                                Id = GetGuidAttribute(ruleElement, "id"),
                                Name = GetStringAttribute(ruleElement, "name"),
                                ProcessNamePattern = GetStringAttribute(ruleElement, "pattern"),
                                Affinity = GetLongAttribute(ruleElement, "affinity"),
                                Priority = (ProcessPriority)GetIntAttribute(ruleElement, "priority", (int)ProcessPriority.Normal),
                                ApplyPriority = GetBoolAttribute(ruleElement, "applyPriority"),
                                ApplyAffinity = GetBoolAttribute(ruleElement, "applyAffinity", true),
                                ApplyOnProcessStart = GetBoolAttribute(ruleElement, "applyOnStart", true),
                                IsEnabled = GetBoolAttribute(ruleElement, "enabled", true),
                                UsePerformanceCores = GetBoolAttribute(ruleElement, "usePerformanceCores"),
                                UseEfficiencyCores = GetBoolAttribute(ruleElement, "useEfficiencyCores"),
                                CreationDate = GetDateTimeAttribute(ruleElement, "created"),
                                LastModifiedDate = GetDateTimeAttribute(ruleElement, "modified"),
                                CoreCount = GetIntAttribute(ruleElement, "coreCount")
                            };
                            
                            profile.AffinityRules.Add(rule);
                        }
                    }
                }
                
                return profile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse .pow file: {ex.Message}");
                return null;
            }
        }
        
        private PowerProfile ParseJsonFile(string filePath)
        {
            try
            {
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
                Console.WriteLine($"Failed to parse .json file: {ex.Message}");
                return null;
            }
        }
        
        private bool ExportToPowFile(PowerProfile profile, string filePath)
        {
            try
            {
                var document = new XDocument();
                var root = new XElement("PowerProfile");
                document.Add(root);
                
                // Add basic properties
                root.SetAttributeValue("id", profile.Id);
                root.Add(new XElement("Name", profile.Name));
                root.Add(new XElement("Description", profile.Description));
                root.Add(new XElement("IsActive", profile.IsActive));
                root.Add(new XElement("IsSystemProfile", profile.IsSystemProfile));
                root.Add(new XElement("IsBundled", profile.IsBundled));
                root.Add(new XElement("CreationDate", profile.CreationDate));
                root.Add(new XElement("LastModifiedDate", profile.LastModifiedDate));
                
                // Add optimization flags
                root.Add(new XElement("IsGamingOptimized", profile.IsGamingOptimized));
                root.Add(new XElement("IsBatteryOptimized", profile.IsBatteryOptimized));
                root.Add(new XElement("IsThermalOptimized", profile.IsThermalOptimized));
                root.Add(new XElement("UsesDynamicThreadAllocation", profile.UsesDynamicThreadAllocation));
                root.Add(new XElement("UsesPerformanceCoresForApps", profile.UsesPerformanceCoresForApps));
                
                // Add CPU settings
                root.Add(new XElement("MaxCpuPower", profile.MaxCpuPower));
                root.Add(new XElement("MaxCpuTemperature", profile.MaxCpuTemperature));
                root.Add(new XElement("MinCpuFrequency", profile.MinCpuFrequency));
                root.Add(new XElement("MaxCpuFrequency", profile.MaxCpuFrequency));
                root.Add(new XElement("ParkEfficiencyCores", profile.ParkEfficiencyCores));
                root.Add(new XElement("UseHyperthreading", profile.UseHyperthreading));
                root.Add(new XElement("PerformanceCoresOffset", profile.PerformanceCoresOffset));
                root.Add(new XElement("EfficiencyCoresOffset", profile.EfficiencyCoresOffset));
                root.Add(new XElement("PowerSchemeGuid", profile.PowerSchemeGuid));
                
                // Add performance core apps
                var appsElement = new XElement("PerformanceCoreApps");
                root.Add(appsElement);
                
                foreach (var app in profile.PerformanceCoreApps)
                {
                    appsElement.Add(new XElement("App", app));
                }
                
                // Add settings
                var settingsElement = new XElement("Settings");
                root.Add(settingsElement);
                
                foreach (var setting in profile.Settings)
                {
                    var settingElement = new XElement("Setting");
                    settingElement.SetAttributeValue("key", setting.Key);
                    settingElement.SetAttributeValue("value", setting.Value);
                    settingsElement.Add(settingElement);
                }
                
                // Add affinity rules
                var rulesElement = new XElement("AffinityRules");
                root.Add(rulesElement);
                
                foreach (var rule in profile.AffinityRules)
                {
                    var ruleElement = new XElement("Rule");
                    ruleElement.SetAttributeValue("id", rule.Id);
                    ruleElement.SetAttributeValue("name", rule.Name);
                    ruleElement.SetAttributeValue("pattern", rule.ProcessNamePattern);
                    ruleElement.SetAttributeValue("affinity", rule.Affinity);
                    ruleElement.SetAttributeValue("priority", (int)rule.Priority);
                    ruleElement.SetAttributeValue("applyPriority", rule.ApplyPriority);
                    ruleElement.SetAttributeValue("applyAffinity", rule.ApplyAffinity);
                    ruleElement.SetAttributeValue("applyOnStart", rule.ApplyOnProcessStart);
                    ruleElement.SetAttributeValue("enabled", rule.IsEnabled);
                    ruleElement.SetAttributeValue("usePerformanceCores", rule.UsePerformanceCores);
                    ruleElement.SetAttributeValue("useEfficiencyCores", rule.UseEfficiencyCores);
                    ruleElement.SetAttributeValue("created", rule.CreationDate);
                    ruleElement.SetAttributeValue("modified", rule.LastModifiedDate);
                    ruleElement.SetAttributeValue("coreCount", rule.CoreCount);
                    rulesElement.Add(ruleElement);
                }
                
                document.Save(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export to .pow file: {ex.Message}");
                return false;
            }
        }
        
        private bool ExportToJsonFile(PowerProfile profile, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export to .json file: {ex.Message}");
                return false;
            }
        }
        
        private void ApplyProfileSettings(PowerProfile profile)
        {
            try
            {
                // This would be implemented with actual hardware API calls
                // For now, just log the application
                Console.WriteLine($"Applying profile {profile.Name}...");
                
                // Example of what would be implemented:
                // - Set CPU power limit
                // - Set CPU thermal limit
                // - Set CPU frequency limits
                // - Set core parking settings
                // - Set hyperthreading setting
                // - Set voltage offset
                // - Apply affinity rules
                // - Set Windows power scheme
                
                // Apply affinity rules
                if (profile.AffinityRules.Count > 0)
                {
                    var processService = ServiceLocator.Get<IProcessService>();
                    processService.ApplyAffinityRules(profile.AffinityRules);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to apply profile settings: {ex.Message}");
            }
        }
        
        private Guid GetGuidAttribute(XElement element, string name)
        {
            var attr = element.Attribute(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(attr) && Guid.TryParse(attr, out var guid))
            {
                return guid;
            }
            
            return Guid.NewGuid();
        }
        
        private string GetStringAttribute(XElement element, string name)
        {
            return element.Attribute(name)?.Value;
        }
        
        private int GetIntAttribute(XElement element, string name, int defaultValue = 0)
        {
            var attr = element.Attribute(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(attr) && int.TryParse(attr, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        private long GetLongAttribute(XElement element, string name, long defaultValue = 0)
        {
            var attr = element.Attribute(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(attr) && long.TryParse(attr, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        private bool GetBoolAttribute(XElement element, string name, bool defaultValue = false)
        {
            var attr = element.Attribute(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(attr) && bool.TryParse(attr, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        private DateTime GetDateTimeAttribute(XElement element, string name)
        {
            var attr = element.Attribute(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(attr) && DateTime.TryParse(attr, out var value))
            {
                return value;
            }
            
            return DateTime.Now;
        }
        
        private string GetStringElement(XElement element, string name)
        {
            return element.Element(name)?.Value;
        }
        
        private int GetIntElement(XElement element, string name, int defaultValue = 0)
        {
            var elem = element.Element(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(elem) && int.TryParse(elem, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        private bool GetBoolElement(XElement element, string name, bool defaultValue = false)
        {
            var elem = element.Element(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(elem) && bool.TryParse(elem, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        private DateTime GetDateTimeElement(XElement element, string name)
        {
            var elem = element.Element(name)?.Value;
            
            if (!string.IsNullOrWhiteSpace(elem) && DateTime.TryParse(elem, out var value))
            {
                return value;
            }
            
            return DateTime.Now;
        }
    }
}