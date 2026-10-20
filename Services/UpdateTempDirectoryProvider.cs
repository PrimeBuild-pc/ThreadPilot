/*
 * ThreadPilot - safe temporary directory management for update downloads.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;

    /// <summary>
    /// Each download gets its own admin-only folder directly under the root, with an unguessable name, so a
    /// non-admin process cannot swap the verified installer (or plant DLLs beside it) before it runs elevated.
    /// </summary>
    public sealed class UpdateTempDirectoryProvider : IUpdateTempDirectoryProvider
    {
        private const string FolderPrefix = "ThreadPilot-update-";

        private readonly string rootDirectory;

        public UpdateTempDirectoryProvider()
            : this(AdminOnlyDirectory.DefaultRoot)
        {
        }

        public UpdateTempDirectoryProvider(string rootDirectory)
        {
            this.rootDirectory = Path.GetFullPath(rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory)));
        }

        public string CreateUpdateTempDirectory(SemanticVersion version)
        {
            return AdminOnlyDirectory.Create(Path.Combine(this.rootDirectory, $"{FolderPrefix}{version}-{Guid.NewGuid():N}"));
        }

        public bool IsSafeUpdateTempPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            var prefix = Path.TrimEndingDirectorySeparator(this.rootDirectory) + Path.DirectorySeparatorChar + FolderPrefix;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        public void Cleanup(string path)
        {
            if (!this.IsSafeUpdateTempPath(path) || !Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
        }
    }
}
