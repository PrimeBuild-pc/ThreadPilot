/*
 * ThreadPilot - persistent process rule matcher.
 */
namespace ThreadPilot.Services
{
    using System.IO;
    using ThreadPilot.Models;

    public interface IPersistentProcessRuleMatcher
    {
        bool IsMatch(PersistentProcessRule rule, ProcessModel process);
    }

    public sealed class PersistentProcessRuleMatcher : IPersistentProcessRuleMatcher
    {
        public bool IsMatch(PersistentProcessRule rule, ProcessModel process)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentNullException.ThrowIfNull(process);

            if (!rule.IsEnabled)
            {
                return false;
            }

            var rulePath = NormalizePath(rule.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(rulePath))
            {
                var processPath = NormalizePath(process.ExecutablePath);
                return !string.IsNullOrWhiteSpace(processPath) &&
                    string.Equals(rulePath, processPath, StringComparison.OrdinalIgnoreCase);
            }

            return !string.IsNullOrWhiteSpace(rule.ProcessName) &&
                string.Equals(rule.ProcessName.Trim(), process.Name?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var trimmed = path.Trim();
            try
            {
                trimmed = Path.GetFullPath(trimmed);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Keep matching best-effort for inaccessible or malformed process paths.
            }

            return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
