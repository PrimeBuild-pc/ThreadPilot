using System;
using System.Collections.Generic;
using System.IO;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a Windows power profile/plan with settings
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Profile name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Profile GUID (for Windows power plans)
        /// </summary>
        public Guid Guid { get; set; } = Guid.Empty;
        
        /// <summary>
        /// Power profile settings key-value pairs
        /// </summary>
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// File path to the .pow file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Category/tag for the profile (e.g., Gaming, Productivity, etc.)
        /// </summary>
        public string Category { get; set; } = string.Empty;
        
        /// <summary>
        /// The author/creator of the profile
        /// </summary>
        public string Author { get; set; } = string.Empty;
        
        /// <summary>
        /// Creation date of the profile
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Last modified date of the profile
        /// </summary>
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Whether this profile is a system default one
        /// </summary>
        public bool IsSystemDefault { get; set; }
        
        /// <summary>
        /// Whether this is currently the active profile
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Whether this profile is a built-in Windows power plan
        /// </summary>
        public bool IsBuiltIn { get; set; }
        
        /// <summary>
        /// Whether core parking is disabled in this profile
        /// </summary>
        public bool IsCoreParkingDisabled 
        { 
            get 
            {
                if (Settings.TryGetValue("CoreParkingDisabled", out string value) && 
                    bool.TryParse(value, out bool result))
                {
                    return result;
                }
                
                return false;
            }
            set
            {
                Settings["CoreParkingDisabled"] = value.ToString();
            }
        }
        
        /// <summary>
        /// Processor performance boost mode setting
        /// 0 = Disabled, 1 = Enabled, 2 = Aggressive, 3 = Efficient Aggressive
        /// </summary>
        public int ProcessorBoostMode
        {
            get
            {
                if (Settings.TryGetValue("ProcessorBoostMode", out string value) && 
                    int.TryParse(value, out int result))
                {
                    return result;
                }
                
                return 1; // Default to Enabled
            }
            set 
            {
                Settings["ProcessorBoostMode"] = value.ToString();
            }
        }
        
        /// <summary>
        /// System responsiveness value (0-100)
        /// 0 = Optimize for foreground, 100 = Optimize for background
        /// </summary>
        public int SystemResponsiveness
        {
            get
            {
                if (Settings.TryGetValue("SystemResponsiveness", out string value) && 
                    int.TryParse(value, out int result))
                {
                    return result;
                }
                
                return 20; // Windows default
            }
            set
            {
                Settings["SystemResponsiveness"] = value.ToString();
            }
        }
        
        /// <summary>
        /// Network throttling index (0 - disabled, 10-70 normal values)
        /// </summary>
        public int NetworkThrottlingIndex
        {
            get
            {
                if (Settings.TryGetValue("NetworkThrottlingIndex", out string value) && 
                    int.TryParse(value, out int result))
                {
                    return result;
                }
                
                return 10; // Some reasonable default
            }
            set
            {
                Settings["NetworkThrottlingIndex"] = value.ToString();
            }
        }
        
        /// <summary>
        /// Gets the filename that should be used when saving this profile
        /// </summary>
        public string GetSafeFileName()
        {
            return SanitizeFileName(Name) + ".pow";
        }
        
        /// <summary>
        /// Creates a sanitized filename from the provided input
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            // Replace invalid characters with underscores
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            // Remove any leading or trailing periods or spaces
            sanitized = sanitized.Trim('.', ' ');
            
            // Ensure we have a valid filename by providing a default if empty
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "PowerProfile";
            }
            
            return sanitized;
        }
    }
}