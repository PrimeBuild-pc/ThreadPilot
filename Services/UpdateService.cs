/*
 * ThreadPilot - safe in-app update orchestration.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class UpdateService : IUpdateService
    {
        private const string OfficialOwner = "PrimeBuild-pc";
        private const string OfficialRepository = "ThreadPilot";

        private readonly GitHubUpdateChecker updateChecker;
        private readonly IApplicationSettingsService settingsService;
        private readonly IApplicationVersionProvider versionProvider;
        private readonly IUpdateDownloadService downloadService;
        private readonly IUpdateInstallerService installerService;
        private readonly IUpdateTempDirectoryProvider tempDirectoryProvider;
        private readonly IApplicationShutdownService shutdownService;
        private readonly IUpdateClock clock;
        private readonly ILogger<UpdateService> logger;
        private readonly SemaphoreSlim checkGate = new(1, 1);
        private readonly SemaphoreSlim installGate = new(1, 1);

        public UpdateService(
            GitHubUpdateChecker updateChecker,
            IApplicationSettingsService settingsService,
            IApplicationVersionProvider versionProvider,
            IUpdateDownloadService downloadService,
            IUpdateInstallerService installerService,
            IUpdateTempDirectoryProvider tempDirectoryProvider,
            IApplicationShutdownService shutdownService,
            IUpdateClock clock,
            ILogger<UpdateService> logger)
        {
            this.updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
            this.downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            this.installerService = installerService ?? throw new ArgumentNullException(nameof(installerService));
            this.tempDirectoryProvider = tempDirectoryProvider ?? throw new ArgumentNullException(nameof(tempDirectoryProvider));
            this.shutdownService = shutdownService ?? throw new ArgumentNullException(nameof(shutdownService));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateCheckRequest request, CancellationToken cancellationToken = default)
        {
            var currentVersion = this.versionProvider.CurrentVersion;
            var settings = this.settingsService.Settings;

            if (request.Trigger == UpdateCheckTrigger.Startup)
            {
                if (!settings.EnableAutomaticUpdateChecks)
                {
                    return new UpdateCheckResult(UpdateCheckStatus.Skipped, currentVersion, null, "Automatic update checks are disabled.");
                }

                var intervalDays = Math.Max(1, settings.UpdateCheckIntervalDays);
                if (settings.LastUpdateCheckUtc.HasValue &&
                    this.clock.UtcNow - settings.LastUpdateCheckUtc.Value < TimeSpan.FromDays(intervalDays))
                {
                    return new UpdateCheckResult(UpdateCheckStatus.Skipped, currentVersion, null, "Startup update check throttled.");
                }
            }

            await this.checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await this.MarkUpdateCheckAttemptAsync(cancellationToken).ConfigureAwait(false);

                var release = await this.updateChecker.GetLatestReleaseInfoAsync(
                    OfficialOwner,
                    OfficialRepository,
                    settings.IncludePrereleaseUpdates,
                    cancellationToken).ConfigureAwait(false);

                if (release == null)
                {
                    return new UpdateCheckResult(UpdateCheckStatus.Failed, currentVersion, null, "Unable to determine the latest ThreadPilot release.");
                }

                if (release.Version > currentVersion)
                {
                    this.logger.LogInformation(
                        "ThreadPilot update available: current {CurrentVersion}, latest {LatestVersion}",
                        currentVersion,
                        release.Version);
                    return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, currentVersion, release, "A newer ThreadPilot version is available.");
                }

                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, currentVersion, release, "ThreadPilot is up to date.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "ThreadPilot update check failed");
                return new UpdateCheckResult(UpdateCheckStatus.Failed, currentVersion, null, ex.Message);
            }
            finally
            {
                this.checkGate.Release();
            }
        }

        public async Task<UpdateInstallResult> DownloadAndInstallAsync(UpdateReleaseInfo release, CancellationToken cancellationToken = default)
        {
            if (!await this.installGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return new UpdateInstallResult(UpdateInstallStatus.Failed, "Another update is already in progress.");
            }

            UpdateDownloadResult? download = null;
            try
            {
                download = await this.downloadService.DownloadInstallerAsync(release, cancellationToken).ConfigureAwait(false);
                await this.installerService.LaunchInstallerElevatedAsync(download.InstallerPath, cancellationToken).ConfigureAwait(false);
                this.shutdownService.RequestShutdownForUpdate();
                return new UpdateInstallResult(UpdateInstallStatus.Started, "Update installer started.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "ThreadPilot update install failed");
                return new UpdateInstallResult(UpdateInstallStatus.Failed, ex.Message);
            }
            finally
            {
                if (download != null)
                {
                    this.TryCleanup(download.TempDirectory);
                }

                this.installGate.Release();
            }
        }

        private async Task MarkUpdateCheckAttemptAsync(CancellationToken cancellationToken)
        {
            var settings = this.settingsService.Settings;
            settings.LastUpdateCheckUtc = this.clock.UtcNow;
            await this.settingsService.UpdateSettingsAsync(settings).ConfigureAwait(false);
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
