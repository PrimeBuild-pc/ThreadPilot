namespace ThreadPilot.Services.Abstractions
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a seam for reading and writing persisted settings.
    /// </summary>
    public interface ISettingsStorage
    {
        bool Exists(string path);

        Task<string?> ReadAsync(string path);

        Task WriteAsync(string path, string content);

        void EnsureDirectoryForFile(string path);

        void Copy(string sourcePath, string destinationPath, bool overwrite);
    }
}
