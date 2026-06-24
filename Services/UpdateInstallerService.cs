/*
 * ThreadPilot - elevated update installer launch.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class UpdateInstallerService : IUpdateInstallerService
    {
        private readonly IUpdateTempDirectoryProvider tempDirectoryProvider;
        private readonly IUpdateProcessLauncher processLauncher;

        public UpdateInstallerService(
            IUpdateTempDirectoryProvider tempDirectoryProvider,
            IUpdateProcessLauncher processLauncher)
        {
            this.tempDirectoryProvider = tempDirectoryProvider ?? throw new ArgumentNullException(nameof(tempDirectoryProvider));
            this.processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        }

        public Task LaunchInstallerElevatedAsync(string installerPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Update installer was not found.", installerPath);
            }

            if (!string.Equals(Path.GetExtension(installerPath), ".exe", StringComparison.OrdinalIgnoreCase) ||
                !this.tempDirectoryProvider.IsSafeUpdateTempPath(installerPath))
            {
                throw new InvalidOperationException("Update installer path is not trusted.");
            }

            return this.processLauncher.LaunchElevatedAsync(installerPath, Array.Empty<string>(), cancellationToken);
        }
    }

    public sealed class ShellUpdateProcessLauncher : IUpdateProcessLauncher
    {
        public Task LaunchElevatedAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);
            return Task.CompletedTask;
        }
    }
}
