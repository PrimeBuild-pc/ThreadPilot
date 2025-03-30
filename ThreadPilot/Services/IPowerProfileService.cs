using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public interface IPowerProfileService
    {
        /// <summary>
        /// Gets the currently active power profile
        /// </summary>
        /// <returns>The active power profile GUID</returns>
        string GetActivePowerProfile();

        /// <summary>
        /// Gets information about a power profile
        /// </summary>
        /// <param name="guid">The power profile GUID</param>
        /// <returns>Name of the power profile</returns>
        string GetPowerProfileName(string guid);

        /// <summary>
        /// Gets all available power profiles
        /// </summary>
        /// <returns>List of power profile GUIDs</returns>
        IEnumerable<string> GetAllPowerProfiles();

        /// <summary>
        /// Sets the active power profile
        /// </summary>
        /// <param name="guid">The power profile GUID to activate</param>
        /// <returns>True if successful</returns>
        bool SetActivePowerProfile(string guid);

        /// <summary>
        /// Imports a power profile from a .pow file
        /// </summary>
        /// <param name="filePath">Path to the .pow file</param>
        /// <returns>GUID of the imported profile, or null if import failed</returns>
        string ImportPowerProfile(string filePath);

        /// <summary>
        /// Exports a power profile to a .pow file
        /// </summary>
        /// <param name="guid">The power profile GUID to export</param>
        /// <param name="filePath">Path to save the .pow file</param>
        /// <returns>True if successful</returns>
        bool ExportPowerProfile(string guid, string filePath);

        /// <summary>
        /// Deletes a power profile
        /// </summary>
        /// <param name="guid">The power profile GUID to delete</param>
        /// <returns>True if successful</returns>
        bool DeletePowerProfile(string guid);

        /// <summary>
        /// Creates an optimized power profile for gaming
        /// </summary>
        /// <param name="name">Name for the new profile</param>
        /// <returns>GUID of the created profile, or null if creation failed</returns>
        string CreateGamingProfile(string name);

        /// <summary>
        /// Creates an optimized power profile for content creation
        /// </summary>
        /// <param name="name">Name for the new profile</param>
        /// <returns>GUID of the created profile, or null if creation failed</returns>
        string CreateContentCreationProfile(string name);

        /// <summary>
        /// Creates an optimized power profile for battery saving
        /// </summary>
        /// <param name="name">Name for the new profile</param>
        /// <returns>GUID of the created profile, or null if creation failed</returns>
        string CreateBatterySavingProfile(string name);
    }
}