using System;
using System.IO;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a bundled power profile (.pow file)
    /// </summary>
    public class BundledPowerProfile
    {
        /// <summary>
        /// Gets or sets the name of the profile
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the path to the profile file
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Gets or sets whether the profile is currently active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Gets or sets a description of the profile
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier of the profile (used to check if it's imported into Windows)
        /// </summary>
        public Guid? GuidInSystem { get; set; }
        
        /// <summary>
        /// Gets the filename of the profile without the path
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);
        
        /// <summary>
        /// Gets the name of the profile without the extension
        /// </summary>
        public string ProfileName => Path.GetFileNameWithoutExtension(FilePath);
        
        /// <summary>
        /// Creates a new bundled power profile
        /// </summary>
        /// <param name="filePath">The path to the .pow file</param>
        /// <param name="description">An optional description</param>
        public BundledPowerProfile(string filePath, string description = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
                
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Power profile file not found", filePath);
                
            FilePath = filePath;
            Name = Path.GetFileNameWithoutExtension(filePath);
            Description = description ?? $"Bundled profile: {Name}";
            IsActive = false;
            GuidInSystem = null;
        }
    }
}