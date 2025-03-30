using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a power profile with performance and power settings
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Gets or sets the profile GUID
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Gets or sets the profile name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the profile description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the profile creator
        /// </summary>
        public string Creator { get; set; }
        
        /// <summary>
        /// Gets or sets the profile category
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// Gets or sets the profile creation date
        /// </summary>
        public DateTime CreationDate { get; set; }
        
        /// <summary>
        /// Gets or sets the profile version
        /// </summary>
        public string Version { get; set; }
        
        /// <summary>
        /// Gets or sets whether the profile is a system profile
        /// </summary>
        public bool IsSystemProfile { get; set; }
        
        /// <summary>
        /// Gets or sets whether the profile is hidden
        /// </summary>
        public bool IsHidden { get; set; }
        
        /// <summary>
        /// Gets or sets whether the profile is the active profile
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Gets or sets the profile icon name
        /// </summary>
        public string IconName { get; set; }
        
        /// <summary>
        /// Gets or sets the profile settings (key-value pairs)
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets or sets the binary data for the profile
        /// </summary>
        public byte[] BinaryData { get; set; }
        
        /// <summary>
        /// Gets or sets the profile file path
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets whether the profile is a bundled profile
        /// </summary>
        public bool IsBundled { get; set; }

        /// <summary>
        /// Gets or sets whether the profile is modified
        /// </summary>
        public bool IsModified { get; set; }
    }
}