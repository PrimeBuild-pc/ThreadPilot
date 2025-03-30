using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing power profiles
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        List<BundledPowerProfile> GetAvailableProfiles();
        
        /// <summary>
        /// Apply a power profile to the system
        /// </summary>
        /// <param name="profile">Profile to apply</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ApplyProfile(BundledPowerProfile profile);
        
        /// <summary>
        /// Save a power profile
        /// </summary>
        /// <param name="profile">Profile to save</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SaveProfile(BundledPowerProfile profile);
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="profileId">ID of the profile to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        bool DeleteProfile(string profileId);
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">Profile to export</param>
        /// <param name="filePath">Destination file path</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ExportProfile(BundledPowerProfile profile, string filePath);
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">Source file path</param>
        /// <returns>Imported profile or null if import failed</returns>
        BundledPowerProfile? ImportProfile(string filePath);
        
        /// <summary>
        /// Get the currently active power profile
        /// </summary>
        /// <returns>Active profile or null if no custom profile is active</returns>
        BundledPowerProfile? GetActiveProfile();
    }
}