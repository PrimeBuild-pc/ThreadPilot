using System;
using System.IO;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a bundled power profile included with the application
    /// </summary>
    public class BundledPowerProfile
    {
        /// <summary>
        /// Gets or sets the GUID of the power profile
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the power profile
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of the power profile
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the filename of the power profile
        /// </summary>
        public string Filename { get; set; }
        
        /// <summary>
        /// Gets or sets the category of the power profile
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// Gets or sets whether the profile is installed on the system
        /// </summary>
        public bool IsInstalled { get; set; }
        
        /// <summary>
        /// Gets the full path to the power profile file
        /// </summary>
        public string FilePath => Path.Combine("PowerProfiles", Filename);
        
        /// <summary>
        /// Gets whether the power profile file exists
        /// </summary>
        public bool FileExists => File.Exists(FilePath);
        
        /// <summary>
        /// Creates a new instance of the BundledPowerProfile class
        /// </summary>
        public BundledPowerProfile()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Description = string.Empty;
            Filename = string.Empty;
            Category = string.Empty;
            IsInstalled = false;
        }
        
        /// <summary>
        /// Creates a new instance of the BundledPowerProfile class with the specified parameters
        /// </summary>
        public BundledPowerProfile(string name, string description, string filename, string category, bool isInstalled = false)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Filename = filename;
            Category = category;
            IsInstalled = isInstalled;
        }
    }
}