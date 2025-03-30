using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for power profile management service
    /// </summary>
    public interface IPowerProfileService
    {
        /// <summary>
        /// Get all available Windows power plans
        /// </summary>
        /// <returns>List of power profiles</returns>
        List<PowerProfile> GetAllPowerPlans();
        
        /// <summary>
        /// Get the active Windows power plan
        /// </summary>
        /// <returns>Active power profile</returns>
        PowerProfile GetActivePowerPlan();
        
        /// <summary>
        /// Set active power plan
        /// </summary>
        /// <param name="profileGuid">Power plan GUID</param>
        /// <returns>True if successful</returns>
        bool SetActivePowerPlan(Guid profileGuid);
        
        /// <summary>
        /// Get all saved custom power profiles
        /// </summary>
        /// <returns>List of custom power profiles</returns>
        List<PowerProfile> GetCustomPowerProfiles();
        
        /// <summary>
        /// Get all bundled power profiles
        /// </summary>
        /// <returns>List of bundled power profiles</returns>
        List<BundledPowerProfile> GetBundledProfiles();
        
        /// <summary>
        /// Save a custom power profile
        /// </summary>
        /// <param name="profile">Profile to save</param>
        /// <returns>True if successful</returns>
        bool SaveCustomProfile(PowerProfile profile);
        
        /// <summary>
        /// Delete a custom power profile
        /// </summary>
        /// <param name="profilePath">Path to the profile file</param>
        /// <returns>True if successful</returns>
        bool DeleteCustomProfile(string profilePath);
        
        /// <summary>
        /// Load a power profile from file
        /// </summary>
        /// <param name="filePath">Path to the profile file</param>
        /// <returns>Loaded power profile or null if failed</returns>
        PowerProfile LoadProfileFromFile(string filePath);
        
        /// <summary>
        /// Apply a bundled power profile
        /// </summary>
        /// <param name="bundledProfile">Bundled profile to apply</param>
        /// <returns>True if successful</returns>
        bool ApplyBundledProfile(BundledPowerProfile bundledProfile);
        
        /// <summary>
        /// Apply a Windows power plan and optionally set additional settings
        /// </summary>
        /// <param name="profileGuid">Power plan GUID</param>
        /// <param name="disableCoreParking">Whether to disable core parking</param>
        /// <param name="processorBoostMode">Processor boost mode to set</param>
        /// <param name="systemResponsiveness">System responsiveness value to set</param>
        /// <param name="networkThrottlingIndex">Network throttling index to set</param>
        /// <returns>True if successful</returns>
        bool ApplyPowerPlanWithSettings(
            Guid profileGuid, 
            bool disableCoreParking = false, 
            int? processorBoostMode = null,
            int? systemResponsiveness = null,
            int? networkThrottlingIndex = null);
        
        /// <summary>
        /// Save a bundled power profile
        /// </summary>
        /// <param name="bundledProfile">Bundled profile to save</param>
        /// <returns>True if successful</returns>
        bool SaveBundledProfile(BundledPowerProfile bundledProfile);
        
        /// <summary>
        /// Delete a bundled power profile
        /// </summary>
        /// <param name="bundleName">Name of the bundle to delete</param>
        /// <returns>True if successful</returns>
        bool DeleteBundledProfile(string bundleName);
        
        /// <summary>
        /// Import a power profile from a .pow file
        /// </summary>
        /// <param name="filePath">Path to the .pow file</param>
        /// <returns>True if successful</returns>
        bool ImportPowerProfile(string filePath);
        
        /// <summary>
        /// Export a power profile to a .pow file
        /// </summary>
        /// <param name="profileGuid">Power plan GUID</param>
        /// <param name="filePath">Path to save the .pow file</param>
        /// <returns>True if successful</returns>
        bool ExportPowerProfile(Guid profileGuid, string filePath);
    }
}