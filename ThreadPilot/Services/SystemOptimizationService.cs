using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ThreadPilot.Helpers;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class SystemOptimizationService
    {
        public SystemSettings GetCurrentSystemSettings()
        {
            var settings = new SystemSettings();

            try
            {
                // Get Core Parking settings
                settings.CoreParkingEnabled = IsCoreParking();

                // Get Processor Performance Boost Mode
                settings.PerformanceBoostMode = GetPerformanceBoostMode();

                // Get System Responsiveness
                settings.SystemResponsiveness = GetSystemResponsiveness();

                // Get Network Throttling Index
                settings.NetworkThrottlingIndex = GetNetworkThrottlingIndex();

                // Get Priority Separation
                settings.PrioritySeparation = GetPrioritySeparation();

                // Get Game Mode settings
                settings.GameModeEnabled = IsGameModeEnabled();

                // Get GameBar settings
                settings.GameBarEnabled = IsGameBarEnabled();

                // Get GameDVR settings
                settings.GameDVREnabled = IsGameDVREnabled();

                // Get Hibernation settings
                settings.HibernationEnabled = IsHibernationEnabled();

                // Get Visual Effects Level
                settings.VisualEffectsLevel = GetVisualEffectsLevel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system settings: {ex.Message}");
                throw;
            }

            return settings;
        }

        public void ApplySystemOptimizations(SystemSettings settings)
        {
            try
            {
                // Apply Core Parking settings
                SetCoreParking(settings.CoreParkingEnabled);

                // Apply Processor Performance Boost Mode
                SetPerformanceBoostMode(settings.PerformanceBoostMode);

                // Apply System Responsiveness
                SetSystemResponsiveness(settings.SystemResponsiveness);

                // Apply Network Throttling Index
                SetNetworkThrottlingIndex(settings.NetworkThrottlingIndex);

                // Apply Priority Separation
                SetPrioritySeparation(settings.PrioritySeparation);

                // Apply Game Mode settings
                SetGameModeEnabled(settings.GameModeEnabled);

                // Apply GameBar settings
                SetGameBarEnabled(settings.GameBarEnabled);

                // Apply GameDVR settings
                SetGameDVREnabled(settings.GameDVREnabled);

                // Apply Hibernation settings
                SetHibernationEnabled(settings.HibernationEnabled);

                // Apply Visual Effects Level
                SetVisualEffectsLevel(settings.VisualEffectsLevel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying system optimizations: {ex.Message}");
                throw;
            }
        }

        public void ResetToDefaultSettings()
        {
            try
            {
                // Reset Core Parking to default (enabled)
                SetCoreParking(true);

                // Reset Processor Performance Boost Mode to default (1 - Enabled)
                SetPerformanceBoostMode(1);

                // Reset System Responsiveness to default (20)
                SetSystemResponsiveness(20);

                // Reset Network Throttling Index to default (10)
                SetNetworkThrottlingIndex(10);

                // Reset Priority Separation to default (2)
                SetPrioritySeparation(2);

                // Reset Game Mode to default (enabled)
                SetGameModeEnabled(true);

                // Reset GameBar to default (enabled)
                SetGameBarEnabled(true);

                // Reset GameDVR to default (enabled)
                SetGameDVREnabled(true);

                // Reset Hibernation to default (enabled)
                SetHibernationEnabled(true);

                // Reset Visual Effects to default (0 - Let Windows decide)
                SetVisualEffectsLevel(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting to default settings: {ex.Message}");
                throw;
            }
        }

        #region Core Parking

        private bool IsCoreParking()
        {
            try
            {
                // Run PowerCfg command to get the current setting
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/qh SCHEME_CURRENT SUB_PROCESSOR CPMINCORES",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse the output
                // If the value is 100, core parking is disabled, otherwise it's enabled
                return !output.Contains("0x00000064") && !output.Contains("100 %");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking core parking: {ex.Message}");
                return true; // Assume enabled by default
            }
        }

        private void SetCoreParking(bool enabled)
        {
            try
            {
                // Set core parking (100 = disabled, less than 100 = enabled)
                int value = enabled ? 50 : 100;
                
                // Run PowerCfg command to set the value
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES {value}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }

                // Apply the changes
                processInfo.Arguments = "/setactive SCHEME_CURRENT";
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting core parking: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Processor Performance Boost Mode

        private int GetPerformanceBoostMode()
        {
            try
            {
                // Run PowerCfg command to get the current setting
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/qh SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse the output
                if (output.Contains("0x00000000"))
                    return 0; // Disabled
                else if (output.Contains("0x00000001"))
                    return 1; // Enabled
                else if (output.Contains("0x00000002"))
                    return 2; // Aggressive
                else if (output.Contains("0x00000003"))
                    return 3; // Efficient Aggressive
                else
                    return 1; // Default is Enabled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting performance boost mode: {ex.Message}");
                return 1; // Assume enabled by default
            }
        }

        private void SetPerformanceBoostMode(int mode)
        {
            try
            {
                // Validate the mode
                if (mode < 0 || mode > 3)
                    mode = 1; // Default to Enabled

                // Run PowerCfg command to set the value
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {mode}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }

                // Apply the changes
                processInfo.Arguments = "/setactive SCHEME_CURRENT";
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting performance boost mode: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region System Responsiveness

        private int GetSystemResponsiveness()
        {
            try
            {
                // Check registry value
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
                if (key != null)
                {
                    var value = key.GetValue("SystemResponsiveness");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue;
                    }
                }

                return 20; // Default value
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system responsiveness: {ex.Message}");
                return 20; // Default value
            }
        }

        private void SetSystemResponsiveness(int value)
        {
            try
            {
                // Validate the value (0-100)
                if (value < 0 || value > 100)
                    value = 20; // Default value

                // Set registry value
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
                if (key != null)
                {
                    key.SetValue("SystemResponsiveness", value, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting system responsiveness: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Network Throttling Index

        private int GetNetworkThrottlingIndex()
        {
            try
            {
                // Check registry value
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
                if (key != null)
                {
                    var value = key.GetValue("NetworkThrottlingIndex");
                    if (value != null)
                    {
                        if (value is int intValue)
                        {
                            if (intValue == unchecked((int)0xFFFFFFFF))
                                return -1; // Disabled
                            return intValue;
                        }
                    }
                }

                return 10; // Default value
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting network throttling index: {ex.Message}");
                return 10; // Default value
            }
        }

        private void SetNetworkThrottlingIndex(int value)
        {
            try
            {
                // Convert -1 to 0xFFFFFFFF (disabled)
                int regValue = value < 0 ? unchecked((int)0xFFFFFFFF) : value;

                // Set registry value
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
                if (key != null)
                {
                    key.SetValue("NetworkThrottlingIndex", regValue, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting network throttling index: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Priority Separation

        private int GetPrioritySeparation()
        {
            try
            {
                // Check registry value
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                if (key != null)
                {
                    var value = key.GetValue("Win32PrioritySeparation");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue;
                    }
                }

                return 2; // Default value
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting priority separation: {ex.Message}");
                return 2; // Default value
            }
        }

        private void SetPrioritySeparation(int value)
        {
            try
            {
                // Set registry value
                using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                if (key != null)
                {
                    key.SetValue("Win32PrioritySeparation", value, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting priority separation: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Game Mode

        private bool IsGameModeEnabled()
        {
            try
            {
                // Check registry value
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                if (key != null)
                {
                    var value = key.GetValue("AllowAutoGameMode");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue == 1;
                    }
                }

                return true; // Default is enabled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking game mode: {ex.Message}");
                return true; // Default is enabled
            }
        }

        private void SetGameModeEnabled(bool enabled)
        {
            try
            {
                // Set registry value
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                if (key != null)
                {
                    key.SetValue("AllowAutoGameMode", enabled ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting game mode: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region GameBar

        private bool IsGameBarEnabled()
        {
            try
            {
                // Check registry value
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                if (key != null)
                {
                    var value = key.GetValue("UseGameBar");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue == 1;
                    }
                }

                return true; // Default is enabled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking GameBar: {ex.Message}");
                return true; // Default is enabled
            }
        }

        private void SetGameBarEnabled(bool enabled)
        {
            try
            {
                // Set registry value
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar");
                if (key != null)
                {
                    key.SetValue("UseGameBar", enabled ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting GameBar: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region GameDVR

        private bool IsGameDVREnabled()
        {
            try
            {
                // Check registry value
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR");
                if (key != null)
                {
                    var value = key.GetValue("AppCaptureEnabled");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue == 1;
                    }
                }

                return true; // Default is enabled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking GameDVR: {ex.Message}");
                return true; // Default is enabled
            }
        }

        private void SetGameDVREnabled(bool enabled)
        {
            try
            {
                // Set registry value
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR");
                if (key != null)
                {
                    key.SetValue("AppCaptureEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting GameDVR: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Hibernation

        private bool IsHibernationEnabled()
        {
            try
            {
                // Run PowerCfg command to check hibernation
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/a",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // If hibernation is enabled, the output will contain "Hibernation" in the list of available sleep states
                return output.Contains("Hibernation");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking hibernation: {ex.Message}");
                return true; // Assume enabled by default
            }
        }

        private void SetHibernationEnabled(bool enabled)
        {
            try
            {
                // Run PowerCfg command to enable/disable hibernation
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = enabled ? "/h on" : "/h off",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting hibernation: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Visual Effects

        private int GetVisualEffectsLevel()
        {
            try
            {
                // Check registry value
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                if (key != null)
                {
                    var value = key.GetValue("VisualFXSetting");
                    if (value != null)
                    {
                        if (value is int intValue)
                            return intValue;
                    }
                }

                return 0; // Default is Let Windows decide
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting visual effects level: {ex.Message}");
                return 0; // Default is Let Windows decide
            }
        }

        private void SetVisualEffectsLevel(int level)
        {
            try
            {
                // Validate the level (0-3)
                if (level < 0 || level > 3)
                    level = 0; // Default to Let Windows decide

                // Set registry value
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                if (key != null)
                {
                    key.SetValue("VisualFXSetting", level, RegistryValueKind.DWord);
                }

                // For level 1 (Best performance), disable all visual effects
                if (level == 1)
                {
                    using var advKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualEffectsAdvanced");
                    if (advKey != null)
                    {
                        // Set all visual effects to 0 (disabled)
                        foreach (var name in advKey.GetValueNames())
                        {
                            advKey.SetValue(name, 0, RegistryValueKind.DWord);
                        }
                    }
                }

                // For level 3 (Best appearance), enable all visual effects
                else if (level == 3)
                {
                    using var advKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualEffectsAdvanced");
                    if (advKey != null)
                    {
                        // Set all visual effects to 1 (enabled)
                        foreach (var name in advKey.GetValueNames())
                        {
                            advKey.SetValue(name, 1, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting visual effects level: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}
