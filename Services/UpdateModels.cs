/*
 * ThreadPilot - updater models and abstractions.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public enum UpdateCheckTrigger
    {
        Startup,
        Manual,
    }

    public enum UpdateCheckStatus
    {
        Skipped,
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    public enum UpdateInstallStatus
    {
        Started,
        Failed,
    }

    public enum UpdateSignatureStatus
    {
        Valid,
        Invalid,
        Unknown,
    }

    public sealed record UpdateCheckRequest(UpdateCheckTrigger Trigger);

    public sealed record UpdateAsset(string Name, Uri DownloadUrl, long Size);

    public sealed record UpdateReleaseInfo(
        SemanticVersion Version,
        string TagName,
        Uri ReleasePageUrl,
        bool IsPrerelease,
        IReadOnlyList<UpdateAsset> Assets);

    public sealed record UpdateCheckResult(
        UpdateCheckStatus Status,
        SemanticVersion CurrentVersion,
        UpdateReleaseInfo? Release,
        string Message)
    {
        public bool IsUpdateAvailable => this.Status == UpdateCheckStatus.UpdateAvailable && this.Release != null;
    }

    public sealed record UpdateDownloadResult(
        string InstallerPath,
        string TempDirectory,
        bool ChecksumVerified,
        UpdateSignatureStatus SignatureStatus,
        string Message);

    public sealed record UpdateInstallResult(UpdateInstallStatus Status, string Message);

    public interface IApplicationVersionProvider
    {
        SemanticVersion CurrentVersion { get; }

        string DisplayVersion { get; }
    }

    public interface IUpdateClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateCheckRequest request, CancellationToken cancellationToken = default);

        Task<UpdateInstallResult> DownloadAndInstallAsync(UpdateReleaseInfo release, CancellationToken cancellationToken = default);
    }

    public interface IUpdateDownloadService
    {
        Task<UpdateDownloadResult> DownloadInstallerAsync(UpdateReleaseInfo release, CancellationToken cancellationToken = default);
    }

    public interface IUpdateInstallerService
    {
        Task LaunchInstallerElevatedAsync(string installerPath, CancellationToken cancellationToken = default);
    }

    public interface IUpdateDownloadClient
    {
        Task DownloadFileAsync(Uri uri, string destinationPath, CancellationToken cancellationToken = default);

        Task<string?> TryDownloadStringAsync(Uri uri, CancellationToken cancellationToken = default);
    }

    public interface IUpdateTempDirectoryProvider
    {
        string CreateUpdateTempDirectory(SemanticVersion version);

        bool IsSafeUpdateTempPath(string path);

        void Cleanup(string path);
    }

    public interface IUpdateSignatureVerifier
    {
        UpdateSignatureStatus Verify(string installerPath);
    }

    public interface IUpdateProcessLauncher
    {
        Task LaunchElevatedAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
    }

    public interface IApplicationShutdownService
    {
        void RequestShutdownForUpdate();
    }
}
