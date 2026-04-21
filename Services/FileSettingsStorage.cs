namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using ThreadPilot.Services.Abstractions;

    /// <summary>
    /// Default filesystem-backed settings storage.
    /// </summary>
    public sealed class FileSettingsStorage : ISettingsStorage
    {
        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public void EnsureDirectoryForFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public async Task<string?> ReadAsync(string path)
        {
            return this.Exists(path)
                ? await File.ReadAllTextAsync(path)
                : null;
        }

        public Task WriteAsync(string path, string content)
        {
            this.EnsureDirectoryForFile(path);
            return AtomicFileWriter.WriteAllTextAsync(path, content, Encoding.UTF8);
        }
    }
}
