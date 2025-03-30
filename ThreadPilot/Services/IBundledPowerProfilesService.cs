using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for bundled power profile services
    /// </summary>
    public interface IBundledPowerProfilesService
    {
        /// <summary>
        /// Gets all bundled power profiles
        /// </summary>
        /// <returns>List of bundled power profiles</returns>
        Task<List<BundledPowerProfile>> GetAllBundledProfiles();

        /// <summary>
        /// Gets a bundled power profile by ID
        /// </summary>
        /// <param name="id">The ID of the profile</param>
        /// <returns>The bundled power profile or null if not found</returns>
        Task<BundledPowerProfile> GetProfileById(Guid id);

        /// <summary>
        /// Applies a bundled power profile to the system
        /// </summary>
        /// <param name="id">The ID of the profile to apply</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> ApplyProfile(Guid id);

        /// <summary>
        /// Exports a bundled power profile
        /// </summary>
        /// <param name="id">The ID of the profile to export</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> ExportProfile(Guid id);

        /// <summary>
        /// Deletes a bundled power profile
        /// </summary>
        /// <param name="id">The ID of the profile to delete</param>
        /// <returns>True if successful, otherwise false</returns>
        Task<bool> DeleteProfile(Guid id);

        /// <summary>
        /// Creates a new bundled power profile
        /// </summary>
        /// <param name="name">Name of the profile</param>
        /// <param name="description">Description of the profile</param>
        /// <param name="category">Category of the profile</param>
        /// <param name="author">Author of the profile</param>
        /// <param name="isDefault">Whether this profile should be the default</param>
        /// <returns>The ID of the created profile, or null if creation failed</returns>
        Task<Guid?> CreateProfile(string name, string description, string category, string author, bool isDefault = false);
    }
}