using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using Microsoft.Win32;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.xml";
        private const string ProfilesFileName = "profiles.xml";
        private readonly string _settingsFolder;
        private WindowSettings _windowSettings;
        
        // App settings
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool CheckUpdatesAutomatically { get; set; } = true;
        public bool ShowProcessNotifications { get; set; } = true;
        public bool IsDarkTheme { get; set; } = true;
        public int RefreshInterval { get; set; } = 10;
        
        public WindowSettings WindowSettings
        {
            get => _windowSettings;
            set => _windowSettings = value;
        }

        public SettingsService()
        {
            // Create settings folder in %AppData%
            _settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ThreadPilot");
                
            if (!Directory.Exists(_settingsFolder))
            {
                Directory.CreateDirectory(_settingsFolder);
            }
            
            // Initialize window settings
            _windowSettings = new WindowSettings();
            
            // Load settings
            LoadSettings();
        }

        public void LoadSettings()
        {
            var settingsPath = Path.Combine(_settingsFolder, SettingsFileName);
            
            if (File.Exists(settingsPath))
            {
                try
                {
                    using var stream = new FileStream(settingsPath, FileMode.Open);
                    var serializer = new XmlSerializer(typeof(Settings));
                    var settings = (Settings)serializer.Deserialize(stream);
                    
                    // Apply loaded settings
                    StartWithWindows = settings.StartWithWindows;
                    StartMinimized = settings.StartMinimized;
                    MinimizeToTray = settings.MinimizeToTray;
                    CloseToTray = settings.CloseToTray;
                    CheckUpdatesAutomatically = settings.CheckUpdatesAutomatically;
                    ShowProcessNotifications = settings.ShowProcessNotifications;
                    IsDarkTheme = settings.IsDarkTheme;
                    RefreshInterval = settings.RefreshInterval;
                    WindowSettings = settings.WindowSettings ?? new WindowSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    // Use default settings
                }
            }
        }

        public void SaveSettings()
        {
            var settingsPath = Path.Combine(_settingsFolder, SettingsFileName);
            
            try
            {
                // Create settings object
                var settings = new Settings
                {
                    StartWithWindows = StartWithWindows,
                    StartMinimized = StartMinimized,
                    MinimizeToTray = MinimizeToTray,
                    CloseToTray = CloseToTray,
                    CheckUpdatesAutomatically = CheckUpdatesAutomatically,
                    ShowProcessNotifications = ShowProcessNotifications,
                    IsDarkTheme = IsDarkTheme,
                    RefreshInterval = RefreshInterval,
                    WindowSettings = WindowSettings
                };
                
                // Save to file
                using var stream = new FileStream(settingsPath, FileMode.Create);
                var serializer = new XmlSerializer(typeof(Settings));
                serializer.Serialize(stream, settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        public List<Profile> LoadProfiles()
        {
            var profilesPath = Path.Combine(_settingsFolder, ProfilesFileName);
            
            if (File.Exists(profilesPath))
            {
                try
                {
                    using var stream = new FileStream(profilesPath, FileMode.Open);
                    var serializer = new XmlSerializer(typeof(List<Profile>));
                    return (List<Profile>)serializer.Deserialize(stream);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading profiles: {ex.Message}");
                }
            }
            
            return new List<Profile>();
        }

        public void SaveProfiles(List<Profile> profiles)
        {
            var profilesPath = Path.Combine(_settingsFolder, ProfilesFileName);
            
            try
            {
                // Save to file
                using var stream = new FileStream(profilesPath, FileMode.Create);
                var serializer = new XmlSerializer(typeof(List<Profile>));
                serializer.Serialize(stream, profiles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving profiles: {ex.Message}");
                throw;
            }
        }

        public void ResetToDefaults()
        {
            StartWithWindows = false;
            StartMinimized = false;
            MinimizeToTray = true;
            CloseToTray = true;
            CheckUpdatesAutomatically = true;
            ShowProcessNotifications = true;
            IsDarkTheme = true;
            RefreshInterval = 10;
            WindowSettings = new WindowSettings();
            
            // Update startup registry setting
            SetStartWithWindows(false);
            
            // Save settings
            SaveSettings();
            
            // Apply theme
            ApplyTheme();
        }

        public void SetStartWithWindows(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                
                if (key != null)
                {
                    if (enable)
                    {
                        // Get the executable path
                        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        exePath = exePath.Replace(".dll", ".exe");
                        
                        // Add to startup
                        key.SetValue("ThreadPilot", $"\"{exePath}\"");
                    }
                    else
                    {
                        // Remove from startup
                        key.DeleteValue("ThreadPilot", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting startup: {ex.Message}");
            }
        }

        public void ApplyTheme()
        {
            // Update the application theme
            var app = System.Windows.Application.Current;
            var appResources = app.Resources;
            
            // Clear any previous theme dictionaries
            var themesToRemove = new List<ResourceDictionary>();
            foreach (ResourceDictionary dict in appResources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.ToString().Contains("LightTheme.xaml") || 
                     dict.Source.ToString().Contains("DarkTheme.xaml")))
                {
                    themesToRemove.Add(dict);
                }
            }
            
            foreach (var dict in themesToRemove)
            {
                appResources.MergedDictionaries.Remove(dict);
            }
            
            // Add the selected theme
            var themeUri = new Uri(
                IsDarkTheme 
                    ? "/Resources/DarkTheme.xaml" 
                    : "/Resources/LightTheme.xaml", 
                UriKind.Relative);
            
            appResources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        }

        public void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme();
            SaveSettings();
        }

        [Serializable]
        public class Settings
        {
            public bool StartWithWindows { get; set; }
            public bool StartMinimized { get; set; }
            public bool MinimizeToTray { get; set; }
            public bool CloseToTray { get; set; }
            public bool CheckUpdatesAutomatically { get; set; }
            public bool ShowProcessNotifications { get; set; }
            public bool IsDarkTheme { get; set; }
            public int RefreshInterval { get; set; }
            public WindowSettings WindowSettings { get; set; }
        }
    }
}
