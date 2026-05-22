namespace ThreadPilot.Core.Tests
{
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class LogViewerActivityAuditTests
    {
        [Fact]
        public async Task InitializeAsync_LoadsVisibleThreadPilotActivityEntries()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            await audit.LogSuccessAsync("Power Plans", "Applied power plan Gaming", "Guid: game");
            var viewModel = CreateViewModel(audit);

            await viewModel.InitializeAsync();

            var entry = Assert.Single(viewModel.LogEntries);
            Assert.Equal("Power Plans", entry.Category);
            Assert.Equal("Applied power plan Gaming", entry.Message);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("Guid: game", entry.Details);
        }

        [Fact]
        public async Task ClearLogsCommand_ClearsOnlyVisibleActivityDisplayWithoutAddingNoise()
        {
            var audit = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);
            await audit.LogSuccessAsync("Power Plans", "Applied power plan Gaming");
            var viewModel = CreateViewModel(audit);
            await viewModel.InitializeAsync();

            await ((IAsyncRelayCommand)viewModel.ClearLogsCommand).ExecuteAsync(null);

            Assert.Empty(viewModel.LogEntries);
            Assert.Single(await audit.GetEntriesAsync());
        }

        private static LogViewerViewModel CreateViewModel(IActivityAuditService audit)
        {
            var logging = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            logging
                .Setup(service => service.GetLogStatisticsAsync())
                .ReturnsAsync(new LogFileStatistics());
            var settings = new Mock<IApplicationSettingsService>(MockBehavior.Loose);
            settings
                .SetupGet(service => service.Settings)
                .Returns(new ApplicationSettingsModel());

            return new LogViewerViewModel(
                audit,
                logging.Object,
                settings.Object,
                NullLogger<LogViewerViewModel>.Instance);
        }
    }
}
