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
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> created = new(StringComparer.OrdinalIgnoreCase);

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
            var directory = AdminOnlyDirectory.Create(Path.Combine(this.rootDirectory, $"{FolderPrefix}{version}-{Guid.NewGuid():N}"));
            this.created[directory] = 0;
            return directory;
        }

        public bool IsSafeUpdateTempPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Only folders this instance created: anyone may create a ThreadPilot-update-* folder in the root.
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return this.created.Keys.Any(directory =>
                string.Equals(fullPath, directory, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        public void Cleanup(string path)
        {
            if (!this.IsSafeUpdateTempPath(path) || !Directory.Exists(path))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
            this.created.TryRemove(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), out _);
        }
    }
}
