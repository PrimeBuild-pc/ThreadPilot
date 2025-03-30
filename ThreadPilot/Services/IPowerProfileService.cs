using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the power profile service
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        IList<BundledPowerProfile> GetAvailableProfiles();
        
        /// <summary>
        /// Get the currently active power profile
        /// </summary>
        BundledPowerProfile? GetActiveProfile();
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        Task<bool> ApplyProfileAsync(BundledPowerProfile profile);
        
        /// <summary>
        /// Save profile to a file
        /// </summary>
        bool SaveProfileToFile(BundledPowerProfile profile, string filePath);
        
        /// <summary>
        /// Load profile from a file
        /// </summary>
        BundledPowerProfile? LoadProfileFromFile(string filePath);
        
        /// <summary>
        /// Create a profile from the current system settings
        /// </summary>
        BundledPowerProfile CreateProfileFromCurrentSettings(string name, string description);
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        bool DeleteProfile(BundledPowerProfile profile);
    }
}