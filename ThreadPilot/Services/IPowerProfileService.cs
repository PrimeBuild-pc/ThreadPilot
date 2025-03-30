using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for power profile operations
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Gets all available power profiles
        /// </summary>
        /// <returns>A list of power profiles</returns>
        List<PowerProfile> GetAllProfiles();
        
        /// <summary>
        /// Gets a power profile by ID
        /// </summary>
        /// <param name="id">The profile ID</param>
        /// <returns>The power profile or null if not found</returns>
        PowerProfile GetProfileById(Guid id);
        
        /// <summary>
        /// Gets a power profile by name
        /// </summary>
        /// <param name="name">The profile name</param>
        /// <returns>The power profile or null if not found</returns>
        PowerProfile GetProfileByName(string name);
        
        /// <summary>
        /// Gets the active power profile
        /// </summary>
        /// <returns>The active power profile</returns>
        PowerProfile GetActiveProfile();
        
        /// <summary>
        /// Sets the active power profile
        /// </summary>
        /// <param name="id">The profile ID</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetActiveProfile(Guid id);
        
        /// <summary>
        /// Sets the active power profile by name
        /// </summary>
        /// <param name="name">The profile name</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetActiveProfileByName(string name);
        
        /// <summary>
        /// Creates a new power profile
        /// </summary>
        /// <param name="profile">The profile to create</param>
        /// <returns>The created profile with assigned ID</returns>
        PowerProfile CreateProfile(PowerProfile profile);
        
        /// <summary>
        /// Updates a power profile
        /// </summary>
        /// <param name="profile">The profile to update</param>
        /// <returns>True if successful, false otherwise</returns>
        bool UpdateProfile(PowerProfile profile);
        
        /// <summary>
        /// Deletes a power profile
        /// </summary>
        /// <param name="id">The profile ID</param>
        /// <returns>True if successful, false otherwise</returns>
        bool DeleteProfile(Guid id);
        
        /// <summary>
        /// Exports a power profile to a file
        /// </summary>
        /// <param name="id">The profile ID</param>
        /// <param name="filePath">The file path</param>
        /// <returns>True if successful, false otherwise</returns>
        bool ExportProfile(Guid id, string filePath);
        
        /// <summary>
        /// Imports a power profile from a file
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <returns>The imported profile or null if failed</returns>
        PowerProfile ImportProfile(string filePath);
        
        /// <summary>
        /// Gets the bundled power profiles
        /// </summary>
        /// <returns>A list of bundled power profiles</returns>
        List<PowerProfile> GetBundledProfiles();
        
        /// <summary>
        /// Creates a power profile from binary data
        /// </summary>
        /// <param name="data">The binary data</param>
        /// <returns>The created profile or null if failed</returns>
        PowerProfile CreateProfileFromBinary(byte[] data);
        
        /// <summary>
        /// Saves changes to all modified profiles
        /// </summary>
        /// <returns>True if all profiles were saved successfully, false otherwise</returns>
        bool SaveChanges();
        
        /// <summary>
        /// Refreshes the list of profiles
        /// </summary>
        void RefreshProfiles();
        
        /// <summary>
        /// Occurs when a profile is created
        /// </summary>
        event EventHandler<PowerProfile> ProfileCreated;
        
        /// <summary>
        /// Occurs when a profile is updated
        /// </summary>
        event EventHandler<PowerProfile> ProfileUpdated;
        
        /// <summary>
        /// Occurs when a profile is deleted
        /// </summary>
        event EventHandler<Guid> ProfileDeleted;
        
        /// <summary>
        /// Occurs when the active profile is changed
        /// </summary>
        event EventHandler<PowerProfile> ActiveProfileChanged;
    }
}