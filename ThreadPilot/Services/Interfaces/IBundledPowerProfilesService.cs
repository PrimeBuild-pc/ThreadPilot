using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing bundled power profiles (.pow files)
    /// </summary>
    public interface IBundledPowerProfilesService
    {
        /// <summary>
        /// Refreshes the list of available power profiles
        /// </summary>
        void RefreshProfiles();

        /// <summary>
        /// Gets all available power profiles (bundled, imported, and Windows profiles)
        /// </summary>
        /// <returns>Collection of power profiles</returns>
        IEnumerable<BundledPowerProfile> GetAllProfiles();

        /// <summary>
        /// Imports a power profile from a .pow file
        /// </summary>
        /// <param name="filePath">Path to the .pow file</param>
        /// <returns>The imported profile or null if import failed</returns>
        BundledPowerProfile ImportProfile(string filePath);

        /// <summary>
        /// Exports a power profile to a .pow file
        /// </summary>
        /// <param name="profile">The profile to export</param>
        /// <param name="filePath">The path where to save the file</param>
        /// <returns>True if export was successful</returns>
        bool ExportProfile(BundledPowerProfile profile, string filePath);

        /// <summary>
        /// Activates the specified power profile
        /// </summary>
        /// <param name="profile">The profile to activate</param>
        /// <returns>True if activation was successful</returns>
        bool ActivateProfile(BundledPowerProfile profile);

        /// <summary>
        /// Deletes the specified power profile
        /// </summary>
        /// <param name="profile">The profile to delete</param>
        /// <returns>True if deletion was successful</returns>
        bool DeleteProfile(BundledPowerProfile profile);
    }
}