using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the power profile service.
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Gets all available power profiles.
        /// </summary>
        /// <returns>A list of PowerProfile objects.</returns>
        List<PowerProfile> GetAllProfiles();

        /// <summary>
        /// Gets the bundled power profiles.
        /// </summary>
        /// <returns>A list of PowerProfile objects that are bundled with the application.</returns>
        List<PowerProfile> GetBundledProfiles();

        /// <summary>
        /// Gets the user-created power profiles.
        /// </summary>
        /// <returns>A list of PowerProfile objects that are created by the user.</returns>
        List<PowerProfile> GetUserProfiles();

        /// <summary>
        /// Gets a power profile by its file path.
        /// </summary>
        /// <param name="filePath">The file path to the power profile.</param>
        /// <returns>The PowerProfile object, or null if not found.</returns>
        PowerProfile? GetProfileByPath(string filePath);

        /// <summary>
        /// Gets a power profile by its name.
        /// </summary>
        /// <param name="name">The name of the power profile.</param>
        /// <returns>The PowerProfile object, or null if not found.</returns>
        PowerProfile? GetProfileByName(string name);

        /// <summary>
        /// Creates a new power profile.
        /// </summary>
        /// <param name="profile">The PowerProfile object to create.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool CreateProfile(PowerProfile profile);

        /// <summary>
        /// Updates an existing power profile.
        /// </summary>
        /// <param name="profile">The PowerProfile object to update.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool UpdateProfile(PowerProfile profile);

        /// <summary>
        /// Deletes a power profile.
        /// </summary>
        /// <param name="profile">The PowerProfile object to delete.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool DeleteProfile(PowerProfile profile);

        /// <summary>
        /// Deletes a power profile by its file path.
        /// </summary>
        /// <param name="filePath">The file path to the power profile.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool DeleteProfileByPath(string filePath);

        /// <summary>
        /// Imports a power profile from a file.
        /// </summary>
        /// <param name="filePath">The file path to import from.</param>
        /// <returns>The imported PowerProfile object, or null if import failed.</returns>
        PowerProfile? ImportProfile(string filePath);

        /// <summary>
        /// Exports a power profile to a file.
        /// </summary>
        /// <param name="profile">The PowerProfile object to export.</param>
        /// <param name="filePath">The file path to export to.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ExportProfile(PowerProfile profile, string filePath);

        /// <summary>
        /// Applies a power profile.
        /// </summary>
        /// <param name="profile">The PowerProfile object to apply.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ApplyProfile(PowerProfile profile);

        /// <summary>
        /// Gets the active power profile.
        /// </summary>
        /// <returns>The active PowerProfile object, or null if none is active.</returns>
        PowerProfile? GetActiveProfile();

        /// <summary>
        /// Sets the active power profile.
        /// </summary>
        /// <param name="profile">The PowerProfile object to set as active.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetActiveProfile(PowerProfile profile);

        /// <summary>
        /// Clears the active power profile.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        bool ClearActiveProfile();

        /// <summary>
        /// Resets the system to default power settings.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        bool ResetToDefault();

        /// <summary>
        /// Event that is raised when the active power profile changes.
        /// </summary>
        event EventHandler<PowerProfile?>? ActiveProfileChanged;

        /// <summary>
        /// Event that is raised when a power profile is applied.
        /// </summary>
        event EventHandler<PowerProfile>? ProfileApplied;

        /// <summary>
        /// Event that is raised when a power profile is created, updated, or deleted.
        /// </summary>
        event EventHandler<PowerProfile>? ProfileModified;
    }
}