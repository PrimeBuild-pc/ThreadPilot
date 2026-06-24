/*
 * ThreadPilot - release asset selection for safe installer updates.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Linq;

    public static class UpdateAssetSelector
    {
        public static bool TrySelectInstaller(UpdateReleaseInfo release, out UpdateAsset asset)
        {
            var selected = release.Assets
                .Where(IsInstallerAsset)
                .OrderByDescending(candidate => candidate.Name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            asset = selected!;
            return selected != null;
        }

        public static UpdateAsset? SelectChecksumAsset(UpdateReleaseInfo release)
        {
            return release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsSafeGitHubAssetUrl(Uri uri)
        {
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            return string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSafeAssetFileName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            if (!string.Equals(Path.GetFileName(assetName), assetName, StringComparison.Ordinal))
            {
                return false;
            }

            return assetName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static bool IsInstallerAsset(UpdateAsset asset)
        {
            if (!IsSafeGitHubAssetUrl(asset.DownloadUrl) || !IsSafeAssetFileName(asset.Name))
            {
                return false;
            }

            if (!asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!asset.Name.StartsWith("ThreadPilot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (asset.Name.Contains("portable", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return asset.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
                   asset.Name.Contains("installer", StringComparison.OrdinalIgnoreCase);
        }
    }
}
