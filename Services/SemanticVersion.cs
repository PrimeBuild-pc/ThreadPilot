/*
 * ThreadPilot - semantic version parsing for updater decisions.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Globalization;

    public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? Prerelease = null)
        : IComparable<SemanticVersion>
    {
        public bool IsPrerelease => !string.IsNullOrWhiteSpace(this.Prerelease);

        public static bool TryParse(string? value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var sanitized = value.Trim();
            if (sanitized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized[1..];
            }

            sanitized = sanitized.Split('+')[0];
            var versionAndPrerelease = sanitized.Split('-', 2);
            var parts = versionAndPrerelease[0].Split('.');
            if (parts.Length < 2 || parts.Length > 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
            {
                return false;
            }

            var patch = 0;
            if (parts.Length == 3 &&
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch))
            {
                return false;
            }

            version = new SemanticVersion(
                major,
                minor,
                patch,
                versionAndPrerelease.Length == 2 ? versionAndPrerelease[1] : null);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            var major = this.Major.CompareTo(other.Major);
            if (major != 0)
            {
                return major;
            }

            var minor = this.Minor.CompareTo(other.Minor);
            if (minor != 0)
            {
                return minor;
            }

            var patch = this.Patch.CompareTo(other.Patch);
            if (patch != 0)
            {
                return patch;
            }

            if (!this.IsPrerelease && other.IsPrerelease)
            {
                return 1;
            }

            if (this.IsPrerelease && !other.IsPrerelease)
            {
                return -1;
            }

            return ComparePrerelease(this.Prerelease ?? string.Empty, other.Prerelease ?? string.Empty);
        }

        // SemVer: dot-separated parts, numeric parts by value (beta.10 > beta.9), more parts wins a tie.
        private static int ComparePrerelease(string left, string right)
        {
            var a = left.Split('.');
            var b = right.Split('.');
            for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                var result = long.TryParse(a[i], out var x) && long.TryParse(b[i], out var y)
                    ? x.CompareTo(y)
                    : string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
                if (result != 0)
                {
                    return result;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        public override string ToString()
        {
            var version = $"{this.Major}.{this.Minor}.{this.Patch}";
            return this.IsPrerelease ? $"{version}-{this.Prerelease}" : version;
        }

        public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

        public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

        public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

        public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    }
}
