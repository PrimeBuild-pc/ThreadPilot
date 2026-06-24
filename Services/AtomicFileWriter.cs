namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    internal static class AtomicFileWriter
    {
        public static async Task WriteAllTextAsync(string filePath, string content, Encoding? encoding = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            var targetDirectory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(targetDirectory))
            {
                throw new InvalidOperationException($"Cannot determine target directory for path '{filePath}'.");
            }

            Directory.CreateDirectory(targetDirectory);

            var tempFilePath = Path.Combine(targetDirectory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            var backupFilePath = filePath + ".bak";

            try
            {
                encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                await File.WriteAllTextAsync(tempFilePath, content, encoding);

                if (File.Exists(filePath))
                {
                    File.Replace(tempFilePath, filePath, backupFilePath, ignoreMetadataErrors: true);

                    if (File.Exists(backupFilePath))
                    {
                        File.Delete(backupFilePath);
                    }
                }
                else
                {
                    File.Move(tempFilePath, filePath);
                }
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
    }
}
