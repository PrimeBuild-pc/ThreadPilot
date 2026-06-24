/*
 * ThreadPilot - safe temporary directory management for update downloads.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;

    public sealed class UpdateTempDirectoryProvider : IUpdateTempDirectoryProvider
    {
        private readonly string rootDirectory;

        public UpdateTempDirectoryProvider()
            : this(Path.Combine(Path.GetTempPath(), "ThreadPilot", "Updates"))
        {
        }

        public UpdateTempDirectoryProvider(string rootDirectory)
        {
            this.rootDirectory = Path.GetFullPath(rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory)));
        }

        public string CreateUpdateTempDirectory(SemanticVersion version)
        {
            var directory = Path.Combine(this.rootDirectory, version.ToString(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        public bool IsSafeUpdateTempPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            var rootWithSeparator = this.rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        public void Cleanup(string path)
        {
            if (!this.IsSafeUpdateTempPath(path) || !Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
            this.DeleteEmptyParentsUntilRoot(Path.GetDirectoryName(Path.GetFullPath(path)));
        }

        private void DeleteEmptyParentsUntilRoot(string? directory)
        {
            while (!string.IsNullOrWhiteSpace(directory) && this.IsSafeUpdateTempPath(directory))
            {
                if (Directory.GetFileSystemEntries(directory).Length > 0)
                {
                    return;
                }

                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
    }
}
