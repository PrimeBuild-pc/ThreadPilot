using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for power profile service
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Get all available power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        List<PowerProfile> GetProfiles();
        
        /// <summary>
        /// Save a power profile
        /// </summary>
        /// <param name="profile">Profile to save</param>
        /// <returns>True if successful</returns>
        bool SaveProfile(PowerProfile profile);
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="profileName">Profile name to delete</param>
        /// <returns>True if successful</returns>
        bool DeleteProfile(string profileName);
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">Path to the profile file</param>
        /// <returns>Imported profile or null if import failed</returns>
        PowerProfile? ImportProfile(string filePath);
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">Profile to export</param>
        /// <param name="filePath">Path to export to</param>
        /// <returns>True if successful</returns>
        bool ExportProfile(PowerProfile profile, string filePath);
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">Profile to apply</param>
        /// <returns>Number of rules applied successfully</returns>
        int ApplyProfile(PowerProfile profile);
        
        /// <summary>
        /// Create default power profiles if they don't exist
        /// </summary>
        void CreateDefaultProfiles();
    }
}