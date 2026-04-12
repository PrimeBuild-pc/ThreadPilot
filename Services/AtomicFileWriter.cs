/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
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
