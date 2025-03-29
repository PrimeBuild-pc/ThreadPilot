using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public class PowerProfileService : IPowerProfileService
    {
        public List<PowerProfile> GetPowerProfiles()
        {
            var profiles = new List<PowerProfile>();
            
            try
            {
                // Run PowerCfg command to list all power schemes
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse the output using regex
                var regex = new Regex(@"Power Scheme GUID: ([\w\-]+)\s+\((.*?)\)(?:\s+\*)?", RegexOptions.Multiline);
                var matches = regex.Matches(output);

                foreach (Match match in matches)
                {
                    var guid = match.Groups[1].Value.Trim();
                    var name = match.Groups[2].Value.Trim();
                    var isActive = match.Value.Contains("*");

                    var profile = new PowerProfile
                    {
                        Name = name,
                        Guid = guid,
                        IsActive = isActive,
                        IsBuiltIn = IsBuiltInPowerProfile(guid)
                    };

                    profiles.Add(profile);
                }

                // Sort by active first, then by name
                profiles.Sort((a, b) =>
                {
                    if (a.IsActive && !b.IsActive) return -1;
                    if (!a.IsActive && b.IsActive) return 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power profiles: {ex.Message}");
                throw;
            }

            return profiles;
        }

        public void ApplyPowerProfile(string guidOrName)
        {
            try
            {
                // Check if this is a GUID
                bool isGuid = Guid.TryParse(guidOrName, out _);
                string argument = isGuid ? $"/setactive {guidOrName}" : $"/setactive scheme_current";

                // Run PowerCfg command to set the active power scheme
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = argument,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to apply power profile. PowerCfg returned exit code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying power profile: {ex.Message}");
                throw;
            }
        }

        public string ImportPowerProfile(string filePath, string profileName)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Power profile file not found.", filePath);
                }

                // Run PowerCfg command to import the power scheme
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/import \"{filePath}\" {Guid.NewGuid()}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to import power profile. PowerCfg returned exit code {process.ExitCode}.");
                }

                // Extract the GUID from the output
                var regex = new Regex(@"Power Scheme GUID: ([\w\-]+)", RegexOptions.Multiline);
                var match = regex.Match(output);

                if (match.Success)
                {
                    string guid = match.Groups[1].Value.Trim();

                    // Rename the imported profile if a name was provided
                    if (!string.IsNullOrEmpty(profileName))
                    {
                        var renameProcessInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg.exe",
                            Arguments = $"/changename {guid} \"{profileName}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var renameProcess = Process.Start(renameProcessInfo);
                        renameProcess.WaitForExit();
                    }

                    return guid;
                }
                else
                {
                    throw new InvalidOperationException("Failed to extract GUID from the imported power profile.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing power profile: {ex.Message}");
                throw;
            }
        }

        public void ExportPowerProfile(string guid, string filePath)
        {
            try
            {
                // Create the directory if it doesn't exist
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Run PowerCfg command to export the power scheme
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/export \"{filePath}\" {guid}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to export power profile. PowerCfg returned exit code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting power profile: {ex.Message}");
                throw;
            }
        }

        public void DeletePowerProfile(string guid)
        {
            try
            {
                // Run PowerCfg command to delete the power scheme
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/delete {guid}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to delete power profile. PowerCfg returned exit code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting power profile: {ex.Message}");
                throw;
            }
        }

        private bool IsBuiltInPowerProfile(string guid)
        {
            // List of built-in power profile GUIDs
            var builtInGuids = new List<string>
            {
                "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance
                "a1841308-3541-4fab-bc81-f71556f20b4a"  // Power Saver
            };

            return builtInGuids.Contains(guid.ToLower());
        }
    }
}
