/*
 * ThreadPilot - path pattern matcher with wildcard support.
 */
namespace ThreadPilot.Helpers
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    public static class PathPatternMatcher
    {
        private static readonly RegexOptions MatchOptions =
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        public static bool HasWildcard(string? pattern)
        {
            if (string.IsnullOrEmpty(pattern))
            {
                return false;
            }

            return pattern.IndexOfAny(new[] { '*', '?' }) >= 0;
        }

        public static bool IsPathMatch(string? pattern, string? path)
        {
            if (string.IsnullOrWhiteSpace(pattern) || string.IsnullOrWhiteSpace(path))
            {
                return false;
            }

            var trimmedPattern = pattern.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var trimmedPath = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var normalizedPattern = trimmedPattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var normalizedPath = trimmedPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (!HasWildcard(normalizedPattern))
            {
                return string.Equals(normalizedPattern, normalizedPath, StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                var regexPattern = "^" + Regex.Escape(normalizedPattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";

                return Regex.IsMatch(normalizedPath, regexPattern, MatchOptions);
            }
            catch (ArgumentException)
            {
                return string.Equals(normalizedPattern, normalizedPath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
