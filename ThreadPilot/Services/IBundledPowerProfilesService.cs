using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing bundled power profiles (.pow files)
    /// </summary>
    public interface IBundledPowerProfilesService
    {
        /// <summary>
        /// Gets all bundled power profiles
        /// </summary>
        /// <returns>A list of bundled power profiles</returns>
        Task<List<BundledPowerProfile>> GetBundledProfilesAsync();
        
        /// <summary>
        /// Imports a bundled power profile
        /// </summary>
        /// <param name="profile">The profile to import</param>
        /// <returns>True if import was successful, false otherwise</returns>
        Task<bool> ImportProfileAsync(BundledPowerProfile profile);
        
        /// <summary>
        /// Activates a bundled power profile
        /// </summary>
        /// <param name="profile">The profile to activate</param>
        /// <returns>True if activation was successful, false otherwise</returns>
        Task<bool> ActivateProfileAsync(BundledPowerProfile profile);
        
        /// <summary>
        /// Imports an external .pow file and adds it to the bundled profiles
        /// </summary>
        /// <param name="filePath">Path to the .pow file</param>
        /// <returns>The imported profile if successful, null otherwise</returns>
        Task<BundledPowerProfile> ImportExternalProfileAsync(string filePath);
        
        /// <summary>
        /// Refreshes the status of bundled profiles (checks which ones are active)
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task RefreshProfileStatusAsync();
        
        /// <summary>
        /// Gets all power profiles imported into Windows
        /// </summary>
        /// <returns>A dictionary of profile GUIDs and their names</returns>
        Task<Dictionary<string, string>> GetWindowsPowerProfilesAsync();
    }
}