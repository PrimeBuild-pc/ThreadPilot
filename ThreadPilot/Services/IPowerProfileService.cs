using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows power profiles
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Get all power profiles from the system
        /// </summary>
        List<PowerProfile> GetPowerProfiles();
        
        /// <summary>
        /// Apply a power profile to the system
        /// </summary>
        /// <param name="guidOrName">The GUID or name of the power profile</param>
        void ApplyPowerProfile(string guidOrName);
        
        /// <summary>
        /// Import a power profile from a file
        /// </summary>
        /// <param name="filePath">The file path of the power profile</param>
        /// <param name="profileName">The name to give the imported profile</param>
        /// <returns>The GUID of the imported profile</returns>
        string ImportPowerProfile(string filePath, string profileName);
        
        /// <summary>
        /// Export a power profile to a file
        /// </summary>
        /// <param name="guid">The GUID of the power profile</param>
        /// <param name="filePath">The file path to export to</param>
        void ExportPowerProfile(string guid, string filePath);
        
        /// <summary>
        /// Delete a power profile from the system
        /// </summary>
        /// <param name="guid">The GUID of the power profile</param>
        void DeletePowerProfile(string guid);
    }
}