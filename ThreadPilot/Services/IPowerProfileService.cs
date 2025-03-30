using System;
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
        /// Gets all available power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        IEnumerable<PowerProfile> GetProfiles();
        
        /// <summary>
        /// Gets power profile by name
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>Power profile or null if not found</returns>
        PowerProfile GetProfileByName(string name);
        
        /// <summary>
        /// Gets active power profile
        /// </summary>
        /// <returns>Active power profile or null if not found</returns>
        PowerProfile GetActiveProfile();
        
        /// <summary>
        /// Creates new power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <param name="description">Profile description</param>
        /// <returns>New power profile</returns>
        PowerProfile CreateProfile(string name, string description);
        
        /// <summary>
        /// Saves power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SaveProfile(PowerProfile profile);
        
        /// <summary>
        /// Deletes power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        bool DeleteProfile(PowerProfile profile);
        
        /// <summary>
        /// Imports power profile from file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Imported power profile or null if import failed</returns>
        PowerProfile ImportProfile(string filePath);
        
        /// <summary>
        /// Exports power profile to file
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <param name="filePath">File path</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ExportProfile(PowerProfile profile, string filePath);
        
        /// <summary>
        /// Applies power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ApplyProfile(PowerProfile profile);
        
        /// <summary>
        /// Gets all bundled power profiles
        /// </summary>
        /// <returns>List of bundled power profiles</returns>
        IEnumerable<BundledPowerProfile> GetBundledProfiles();
        
        /// <summary>
        /// Applies affinity rules from a power profile to all processes
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>Number of processes affected</returns>
        int ApplyAffinityRules(PowerProfile profile);
    }
}