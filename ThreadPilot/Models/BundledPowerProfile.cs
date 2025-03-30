using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Bundled power profile class
    /// </summary>
    public class BundledPowerProfile
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
        /// Profile file name
        /// </summary>
        public string FileName { get; set; } = string.Empty;
    }
}