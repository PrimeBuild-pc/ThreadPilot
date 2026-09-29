/*
 * ThreadPilot - path pattern matcher with wildcard support.
 */
namespace ThreadPilot.Helpers
{
    using System;
    using System.IO;

    public static class PathPatternMatcher
    {
        public static bool HasWildcard(string? pattern) =>
            !string.IsNullOrEmpty(pattern) && (pattern.Contains('*') || pattern.Contains('?'));

        public static bool IsPathMatch(string? pattern, string? path)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalizedPattern = Normalize(pattern);
            var normalizedPath = Normalize(path);

            return HasWildcard(normalizedPattern)
                ? MatchesWildcard(normalizedPattern, normalizedPath)
                : string.Equals(normalizedPattern, normalizedPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesWildcard(string pattern, string path)
        {
            var patternIndex = 0;
            var pathIndex = 0;
            var starIndex = -1;
            var retryPathIndex = -1;

            while (pathIndex < path.Length)
            {
                if (patternIndex < pattern.Length &&
                    (pattern[patternIndex] == '?' ||
                     pattern.AsSpan(patternIndex, 1).Equals(
                         path.AsSpan(pathIndex, 1),
                         StringComparison.OrdinalIgnoreCase)))
                {
                    patternIndex++;
                    pathIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    retryPathIndex = pathIndex;
                }
                else if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    pathIndex = ++retryPathIndex;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private static string Normalize(string path) =>
            path.Trim()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
