/*
 * ThreadPilot - secure update installer download and verification.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class UpdateDownloadService : IUpdateDownloadService
    {
        private readonly IUpdateDownloadClient downloadClient;
        private readonly IUpdateTempDirectoryProvider tempDirectoryProvider;
        private readonly IUpdateSignatureVerifier signatureVerifier;
        private readonly ILogger<UpdateDownloadService> logger;

        public UpdateDownloadService(
            IUpdateDownloadClient downloadClient,
            IUpdateTempDirectoryProvider tempDirectoryProvider,
            IUpdateSignatureVerifier signatureVerifier,
            ILogger<UpdateDownloadService> logger)
        {
            this.downloadClient = downloadClient ?? throw new ArgumentNullException(nameof(downloadClient));
            this.tempDirectoryProvider = tempDirectoryProvider ?? throw new ArgumentNullException(nameof(tempDirectoryProvider));
            this.signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UpdateDownloadResult> DownloadInstallerAsync(UpdateReleaseInfo release, CancellationToken cancellationToken = default)
        {
            if (!UpdateAssetSelector.TrySelectInstaller(release, out var installerAsset))
            {
                throw new InvalidOperationException("Release does not contain a ThreadPilot installer asset.");
            }

            // The installer runs elevated and silent: never without a checksum to check it against.
            var checksumAsset = UpdateAssetSelector.SelectChecksumAsset(release)
                ?? throw new InvalidOperationException("Release has no SHA256SUMS.txt, so the installer cannot be verified.");

            var tempDirectory = this.tempDirectoryProvider.CreateUpdateTempDirectory(release.Version);
            try
            {
                var installerPath = Path.Combine(tempDirectory, installerAsset.Name);
                await this.downloadClient.DownloadFileAsync(installerAsset.DownloadUrl, installerPath, cancellationToken)
                    .ConfigureAwait(false);

                var checksumText = await this.downloadClient.TryDownloadStringAsync(checksumAsset.DownloadUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(checksumText) ||
                    !UpdateChecksumVerifier.TryFindExpectedHash(checksumText, installerAsset.Name, out var expectedHash))
                {
                    throw new InvalidOperationException("SHA256SUMS.txt did not contain the installer checksum.");
                }

                if (!UpdateChecksumVerifier.Verify(installerPath, expectedHash))
                {
                    throw new InvalidOperationException("Installer SHA256 checksum did not match SHA256SUMS.txt.");
                }

                var signatureStatus = this.signatureVerifier.Verify(installerPath);
                if (signatureStatus == UpdateSignatureStatus.Invalid)
                {
                    throw new InvalidOperationException("Installer Authenticode signature is invalid.");
                }

                this.logger.LogInformation(
                    "Downloaded ThreadPilot update installer {InstallerName}; checksum verified; signature: {SignatureStatus}",
                    installerAsset.Name,
                    signatureStatus);

                return new UpdateDownloadResult(
                    installerPath,
                    tempDirectory,
                    true,
                    signatureStatus,
                    "Installer checksum verified.");
            }
            catch
            {
                this.TryCleanup(tempDirectory);
                throw;
            }
        }

        private void TryCleanup(string tempDirectory)
        {
            try
            {
                this.tempDirectoryProvider.Cleanup(tempDirectory);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to clean update temp directory {TempDirectory}", tempDirectory);
            }
        }
    }
}
