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
        /// Get all power profiles
        /// </summary>
        /// <returns>List of power profiles</returns>
        List<PowerProfile> GetProfiles();
        
        /// <summary>
        /// Get a power profile by name
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>Power profile or null if not found</returns>
        PowerProfile? GetProfile(string name);
        
        /// <summary>
        /// Create a new power profile
        /// </summary>
        /// <param name="profile">Profile to create</param>
        /// <returns>True if successful</returns>
        bool CreateProfile(PowerProfile profile);
        
        /// <summary>
        /// Update a power profile
        /// </summary>
        /// <param name="profile">Updated profile</param>
        /// <returns>True if successful</returns>
        bool UpdateProfile(PowerProfile profile);
        
        /// <summary>
        /// Delete a power profile
        /// </summary>
        /// <param name="name">Profile name</param>
        /// <returns>True if successful</returns>
        bool DeleteProfile(string name);
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">Profile to apply</param>
        /// <returns>True if successful</returns>
        bool ApplyProfile(PowerProfile profile);
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <returns>Imported profile or null if failed</returns>
        PowerProfile? ImportProfile();
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="profile">Profile to export</param>
        /// <returns>True if successful</returns>
        bool ExportProfile(PowerProfile profile);
        
        /// <summary>
        /// Make a copy of a power profile
        /// </summary>
        /// <param name="profile">Profile to copy</param>
        /// <returns>Copied profile</returns>
        PowerProfile CloneProfile(PowerProfile profile);
    }
}