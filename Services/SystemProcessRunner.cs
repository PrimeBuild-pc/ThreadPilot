/*
 * ThreadPilot - default process runner.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using ThreadPilot.Services.Abstractions;

    /// <summary>
    /// Production implementation of <see cref="IProcessRunner"/> backed by <see cref="Process"/>.
    /// </summary>
    public sealed class SystemProcessRunner : IProcessRunner
    {
        /// <inheritdoc/>
        public async Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            if (!File.Exists(fileName))
            {
                return new ProcessRunResult(-1, string.Empty, $"Executable not found: {fileName}");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
            {
                processInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new ProcessRunResult(-1, string.Empty, $"Could not start process: {fileName}");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var exitTask = process.WaitForExitAsync();
            var completedTask = await Task.WhenAny(exitTask, Task.Delay(timeout)).ConfigureAwait(false);

            if (completedTask != exitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best-effort kill for timed-out processes.
                }

                return new ProcessRunResult(-1, await outputTask.ConfigureAwait(false), $"Process timeout after {timeout.TotalSeconds} seconds");
            }

            await exitTask.ConfigureAwait(false);
            return new ProcessRunResult(process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
        }
    }
}
