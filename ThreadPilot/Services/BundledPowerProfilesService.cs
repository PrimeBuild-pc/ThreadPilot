using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the bundled power profiles service
    /// </summary>
    public interface IBundledPowerProfilesService
    {
        /// <summary>
        /// Gets all available bundled power profiles
        /// </summary>
        Task<List<BundledPowerProfile>> GetAllProfilesAsync();
        
        /// <summary>
        /// Gets a profile by ID
        /// </summary>
        Task<BundledPowerProfile> GetProfileByIdAsync(Guid id);
        
        /// <summary>
        /// Adds a new power profile
        /// </summary>
        Task<bool> AddProfileAsync(BundledPowerProfile profile);
        
        /// <summary>
        /// Updates an existing power profile
        /// </summary>
        Task<bool> UpdateProfileAsync(BundledPowerProfile profile);
        
        /// <summary>
        /// Deletes a power profile
        /// </summary>
        Task<bool> DeleteProfileAsync(Guid id);
        
        /// <summary>
        /// Imports a power profile from a file
        /// </summary>
        Task<BundledPowerProfile> ImportProfileAsync(string filePath, string name, string description, string category);
    }

    /// <summary>
    /// Service for managing bundled power profiles
    /// </summary>
    public class BundledPowerProfilesService : IBundledPowerProfilesService
    {
        private List<BundledPowerProfile> _profiles;
        private readonly string _profilesDirectory;
        
        /// <summary>
        /// Creates a new instance of BundledPowerProfilesService
        /// </summary>
        public BundledPowerProfilesService()
        {
            _profiles = new List<BundledPowerProfile>();
            
            // Set the profiles directory to a subfolder of the application directory
            _profilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_profilesDirectory))
            {
                Directory.CreateDirectory(_profilesDirectory);
            }
            
            // Load built-in profiles from the assets folder
            LoadBuiltInProfiles();
        }

        /// <summary>
        /// Gets all available bundled power profiles
        /// </summary>
        public async Task<List<BundledPowerProfile>> GetAllProfilesAsync()
        {
            // Simulate async operation
            await Task.Delay(1);
            return _profiles.ToList();
        }

        /// <summary>
        /// Gets a profile by ID
        /// </summary>
        public async Task<BundledPowerProfile> GetProfileByIdAsync(Guid id)
        {
            // Simulate async operation
            await Task.Delay(1);
            return _profiles.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Adds a new power profile
        /// </summary>
        public async Task<bool> AddProfileAsync(BundledPowerProfile profile)
        {
            if (profile == null || _profiles.Any(p => p.Id == profile.Id))
            {
                return false;
            }

            // Ensure the profile isn't marked as built-in
            if (!profile.IsBuiltIn)
            {
                _profiles.Add(profile);
                await Task.Delay(1); // Simulate async operation
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates an existing power profile
        /// </summary>
        public async Task<bool> UpdateProfileAsync(BundledPowerProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var existingProfile = _profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existingProfile == null)
            {
                return false;
            }

            // Don't allow modifying built-in profiles
            if (existingProfile.IsBuiltIn)
            {
                return false;
            }

            // Update the profile properties
            var index = _profiles.IndexOf(existingProfile);
            _profiles[index] = profile;

            await Task.Delay(1); // Simulate async operation
            return true;
        }

        /// <summary>
        /// Deletes a power profile
        /// </summary>
        public async Task<bool> DeleteProfileAsync(Guid id)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == id);
            if (profile == null)
            {
                return false;
            }

            // Don't allow deleting built-in profiles
            if (profile.IsBuiltIn)
            {
                return false;
            }

            _profiles.Remove(profile);

            // If the file exists and isn't a built-in profile, delete it
            if (!profile.IsBuiltIn && File.Exists(profile.FilePath))
            {
                try
                {
                    File.Delete(profile.FilePath);
                }
                catch
                {
                    // Log error but continue
                }
            }

            await Task.Delay(1); // Simulate async operation
            return true;
        }

        /// <summary>
        /// Imports a power profile from a file
        /// </summary>
        public async Task<BundledPowerProfile> ImportProfileAsync(string filePath, string name, string description, string category)
        {
            if (!File.Exists(filePath) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                // Generate a unique filename based on the name
                string safeFileName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                string destinationPath = Path.Combine(_profilesDirectory, $"{safeFileName}.pow");
                
                // Copy the file to the profiles directory
                File.Copy(filePath, destinationPath, true);
                
                // Create a new profile
                var profile = new BundledPowerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description ?? "",
                    Category = category ?? "Custom",
                    FilePath = destinationPath,
                    IsBuiltIn = false
                };
                
                // Add the profile to the collection
                _profiles.Add(profile);
                
                await Task.Delay(1); // Simulate async operation
                return profile;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Loads built-in profiles from the assets folder
        /// </summary>
        private void LoadBuiltInProfiles()
        {
            // Define built-in profiles based on the .pow files from attached_assets
            var builtInProfiles = new List<BundledPowerProfile>
            {
                new BundledPowerProfile("AdamX Performance", "Optimized for gaming with minimal latency and maximum CPU performance", "Gaming", "attached_assets/adamx.pow", true),
                new BundledPowerProfile("Atlas Power", "Balanced power profile for workstations with moderate usage", "Productivity", "attached_assets/atlas.pow", true),
                new BundledPowerProfile("Bitsum Highest Performance", "Maximum performance for intensive workloads", "Performance", "attached_assets/bitsum.pow", true),
                new BundledPowerProfile("Core Optimizer", "Focused on core parking optimization", "System", "attached_assets/core.pow", true),
                new BundledPowerProfile("Frame Sync Boost", "Enhanced for high framerate gaming with sync optimization", "Gaming", "attached_assets/FrameSyncBoost.pow", true),
                new BundledPowerProfile("Hybred Balance", "Balanced performance across cores with efficient energy use", "Balanced", "attached_assets/hybred.pow", true),
                new BundledPowerProfile("Kaisen Power", "Aggressive performance tuning for intensive applications", "Performance", "attached_assets/kaisen.pow", true),
                new BundledPowerProfile("PowerX Ultimate", "Ultimate performance with no throttling or power saving", "Extreme", "attached_assets/powerx.pow", true),
                new BundledPowerProfile("Sapphire Rendering", "Optimized for 3D rendering and video processing", "Creative", "attached_assets/sapphire.pow", true),
                new BundledPowerProfile("Ancel Balance", "Perfect balance between performance and power efficiency", "Balanced", "attached_assets/ancel.pow", true),
                new BundledPowerProfile("Calypto Performance", "Performance-oriented profile with moderate power savings", "Performance", "attached_assets/calypto.pow", true),
                new BundledPowerProfile("ExmFree Power", "Free-running cores with no limitations", "Extreme", "attached_assets/exmfree.pow", true),
                new BundledPowerProfile("Khorvie Creator", "Designed for content creators and designers", "Creative", "attached_assets/khorvie.pow", true),
                new BundledPowerProfile("Kirby Efficiency", "Efficiency-focused profile for laptops and battery savings", "Efficiency", "attached_assets/kirby.pow", true),
                new BundledPowerProfile("Kizzimo Gaming", "Specialized for competitive gaming", "Gaming", "attached_assets/kizzimo.pow", true),
                new BundledPowerProfile("Lawliet Development", "Optimized for software development and compilation", "Development", "attached_assets/lawliet.pow", true),
                new BundledPowerProfile("VTRL Studio", "Virtual reality and streaming optimization", "Creative", "attached_assets/vtrl.pow", true),
                new BundledPowerProfile("Xilly Power", "Balanced profile with slight performance bias", "Balanced", "attached_assets/xilly.pow", true),
                new BundledPowerProfile("XOS System", "System-wide optimization with good battery life", "System", "attached_assets/xos.pow", true)
            };

            _profiles.AddRange(builtInProfiles);
        }
    }
}