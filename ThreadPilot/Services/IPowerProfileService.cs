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
        List<PowerProfile> GetAllProfiles();
        
        /// <summary>
        /// Save a power profile
        /// </summary>
        /// <param name="profile">The profile to save</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SaveProfile(PowerProfile profile);
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="profileName">The profile name</param>
        /// <returns>True if successful, false otherwise</returns>
        bool DeleteProfile(string profileName);
        
        /// <summary>
        /// Create a new default power profile with the given name
        /// </summary>
        /// <param name="name">The profile name</param>
        /// <param name="category">The profile category</param>
        /// <returns>A new power profile</returns>
        PowerProfile CreateDefaultProfile(string name, string category = "Custom");
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>The imported power profile, or null if failed</returns>
        PowerProfile? ImportProfile(string filePath);
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">The profile to export</param>
        /// <param name="filePath">The file path</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ExportProfile(PowerProfile profile, string filePath);
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">The profile to apply</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ApplyProfile(PowerProfile profile);
        
        /// <summary>
        /// Get the current active Windows power plan
        /// </summary>
        /// <returns>The power plan name</returns>
        string GetCurrentPowerPlan();
        
        /// <summary>
        /// Set the active Windows power plan
        /// </summary>
        /// <param name="planName">The plan name or GUID</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetPowerPlan(string planName);
    }
}