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
        IEnumerable<BundledPowerProfile> GetAllProfiles();
        
        /// <summary>
        /// Get profile by ID
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        BundledPowerProfile? GetProfileById(int profileId);
        
        /// <summary>
        /// Save power profile
        /// </summary>
        /// <param name="profile">Power profile</param>
        /// <returns>True if successful</returns>
        bool SaveProfile(BundledPowerProfile profile);
        
        /// <summary>
        /// Delete power profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>True if successful</returns>
        bool DeleteProfile(int profileId);
        
        /// <summary>
        /// Apply power profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>True if successful</returns>
        bool ApplyProfile(int profileId);
        
        /// <summary>
        /// Import power profile
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Imported profile</returns>
        BundledPowerProfile? ImportProfile(string filePath);
        
        /// <summary>
        /// Export power profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="filePath">File path</param>
        /// <returns>True if successful</returns>
        bool ExportProfile(int profileId, string filePath);
    }
}