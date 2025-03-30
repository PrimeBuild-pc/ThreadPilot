using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model class for bundled power profiles
    /// </summary>
    public class BundledPowerProfile
    {
        /// <summary>
        /// Profile ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Profile name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// File path for the profile
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if the profile is read-only
        /// </summary>
        public bool IsReadOnly { get; set; }
        
        /// <summary>
        /// Indicates if the profile is a system profile
        /// </summary>
        public bool IsSystemProfile { get; set; }
        
        /// <summary>
        /// Profile creation date
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Profile last modified date
        /// </summary>
        public DateTime ModifiedOn { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Profile category
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile GUID in Windows
        /// </summary>
        public Guid WindowsGuid { get; set; } = Guid.Empty;
        
        /// <summary>
        /// Indicates if the profile is active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Profile icon
        /// </summary>
        public string Icon { get; set; } = string.Empty;
    }
}