using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for power profile services
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Gets a power profile by ID
        /// </summary>
        /// <param name="id">The ID of the power profile</param>
        /// <returns>The power profile or null if not found</returns>
        Task<BundledPowerProfile> GetProfileById(Guid id);

        /// <summary>
        /// Gets all available power profiles
        /// </summary>
        /// <returns>Dictionary of power profiles with their IDs</returns>
        Task<Dictionary<Guid, string>> GetAllProfiles();

        /// <summary>
        /// Applies a power profile to the system
        /// </summary>
        /// <param name="id">The ID of the power profile to apply</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> ApplyProfile(Guid id);

        /// <summary>
        /// Imports a power profile from a file
        /// </summary>
        /// <param name="filePath">Path to the power profile file</param>
        /// <returns>The ID of the imported profile, or null if import failed</returns>
        Task<Guid?> ImportProfile(string filePath);

        /// <summary>
        /// Exports a power profile to a file
        /// </summary>
        /// <param name="id">The ID of the power profile to export</param>
        /// <param name="filePath">Path to save the power profile file</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> ExportProfile(Guid id, string filePath);

        /// <summary>
        /// Deletes a power profile
        /// </summary>
        /// <param name="id">The ID of the power profile to delete</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> DeleteProfile(Guid id);

        /// <summary>
        /// Creates a new power profile from system settings
        /// </summary>
        /// <param name="name">Name of the profile</param>
        /// <param name="description">Description of the profile</param>
        /// <param name="category">Category of the profile</param>
        /// <param name="author">Author of the profile</param>
        /// <param name="isDefault">Whether this profile should be the default</param>
        /// <returns>The ID of the created profile, or null if creation failed</returns>
        Task<Guid?> CreateProfileFromCurrentSettings(string name, string description, string category, string author, bool isDefault = false);
    }
}