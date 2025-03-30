using System.Collections.Generic;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for interacting with Windows Power Profiles directly
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Gets the active power profile GUID
        /// </summary>
        /// <returns>The GUID of the active power profile</returns>
        string GetActiveProfileGuid();

        /// <summary>
        /// Sets the active power profile
        /// </summary>
        /// <param name="guid">The GUID of the profile to activate</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetActiveProfile(string guid);

        /// <summary>
        /// Gets information about a power profile
        /// </summary>
        /// <param name="guid">The GUID of the profile</param>
        /// <returns>Dictionary of profile settings</returns>
        Dictionary<string, string> GetProfileInfo(string guid);

        /// <summary>
        /// Deletes a power profile
        /// </summary>
        /// <param name="guid">The GUID of the profile to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        bool DeleteProfile(string guid);
    }
}