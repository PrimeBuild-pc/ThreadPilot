/*
 * ThreadPilot - SHA256SUMS parsing and verification.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    public static class UpdateChecksumVerifier
    {
        public static bool TryFindExpectedHash(string checksumsText, string fileName, out string expectedHash)
        {
            expectedHash = string.Empty;
            foreach (var rawLine in checksumsText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("SHA256(", StringComparison.OrdinalIgnoreCase))
                {
                    var close = line.IndexOf(')');
                    var equals = line.IndexOf('=', StringComparison.Ordinal);
                    if (close > 7 && equals > close)
                    {
                        var listedName = line[7..close];
                        var hash = line[(equals + 1)..].Trim();
                        if (IsHash(hash) && string.Equals(listedName, fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            expectedHash = hash.ToUpperInvariant();
                            return true;
                        }
                    }
                }

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && IsHash(parts[0]))
                {
                    var listedName = parts[^1].TrimStart('*');
                    if (string.Equals(listedName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        expectedHash = parts[0].ToUpperInvariant();
                        return true;
                    }
                }
            }

            return false;
        }

        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            return string.Concat(hash.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        }

        public static bool Verify(string filePath, string expectedHash)
        {
            return string.Equals(ComputeSha256(filePath), expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHash(string value)
        {
            return value.Length == 64 && value.All(Uri.IsHexDigit);
        }
    }
}
