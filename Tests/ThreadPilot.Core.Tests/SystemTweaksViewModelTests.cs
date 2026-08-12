namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class SystemTweaksViewModelTests
    {
        [Fact]
        public async Task LoadAsync_QueriesEverySupportedTweakExactlyOnce()
        {
            var harness = new Harness();
            var status = new TweakStatus { IsEnabled = true, IsAvailable = true };
            harness.Tweaks.Setup(service => service.GetGameModeStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetCoreParkingStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetCStatesStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetMemoryIntegrityStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetUsbSelectiveSuspendStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetPointerPrecisionStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetEthernetPowerSavingStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetInterruptModerationStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetGpuMsiModeStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetSysMainStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetPrefetchStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetPowerThrottlingStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetHighSchedulingCategoryStatusAsync()).ReturnsAsync(status);
            harness.Tweaks.Setup(service => service.GetMenuShowDelayStatusAsync()).ReturnsAsync(status);
            var viewModel = harness.CreateViewModel();

            await viewModel.LoadAsync();

            Assert.Equal(Enum.GetValues<SystemTweak>().Length, viewModel.TweakItems.Count);
            Assert.All(viewModel.TweakItems.Where(item => !item.IsGuidedAction), item => Assert.True(item.IsEnabled));
            Assert.All(viewModel.TweakItems, item => Assert.True(item.IsAvailable));
            harness.Tweaks.Verify(service => service.GetGameModeStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetCoreParkingStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetCStatesStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetMemoryIntegrityStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetUsbSelectiveSuspendStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetPointerPrecisionStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetEthernetPowerSavingStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetInterruptModerationStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetGpuMsiModeStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetSysMainStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetPrefetchStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetPowerThrottlingStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetHighSchedulingCategoryStatusAsync(), Times.Once);
            harness.Tweaks.Verify(service => service.GetMenuShowDelayStatusAsync(), Times.Once);
        }

        [Theory]
        [InlineData(SystemTweak.GameMode, "Game Mode")]
        [InlineData(SystemTweak.CoreParking, "Core Parking")]
        [InlineData(SystemTweak.CStates, "C-States")]
        [InlineData(SystemTweak.UsbSelectiveSuspend, "USB Selective Suspend")]
        [InlineData(SystemTweak.PointerPrecision, "Enhance pointer precision")]
        [InlineData(SystemTweak.EthernetPowerSaving, "Disable Ethernet power saving")]
        [InlineData(SystemTweak.InterruptModeration, "Disable interrupt moderation")]
        [InlineData(SystemTweak.GpuMsiMode, "GPU MSI Mode")]
        [InlineData(SystemTweak.SysMain, "SysMain Service")]
        [InlineData(SystemTweak.Prefetch, "Prefetch")]
        [InlineData(SystemTweak.PowerThrottling, "Power Throttling")]
        [InlineData(SystemTweak.HighSchedulingCategory, "High Scheduling Category")]
        [InlineData(SystemTweak.MenuShowDelay, "Menu Show Delay")]
        public async Task ToggleTweakCommand_CallsExpectedServiceAndLogsSuccess(SystemTweak tweakType, string name)
        {
            var harness = new Harness();
            harness.SetupTweak(tweakType, setResult: true);
            var viewModel = harness.CreateViewModel();
            var item = viewModel.TweakItems.Single(tweak => tweak.TweakType == tweakType);

            Assert.NotNull(item.ToggleCommand);
            await item.ToggleCommand.ExecuteAsync(item);

            harness.VerifySetCalled(tweakType);
            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "SystemTweakApplied",
                    $"{name} enabled",
                    tweakType.ToString()),
                Times.Once);
            var entry = Assert.Single(await harness.Audit.GetEntriesAsync());
            Assert.Equal("Tweaks", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Success, entry.Severity);
            Assert.Equal($"{name} enabled", entry.Message);
            Assert.Equal($"{name} enabled successfully", viewModel.StatusMessage);
        }

        [Fact]
        public async Task ToggleTweakCommand_WhenServiceFails_LogsFailureAndShowsSafeStatus()
        {
            var harness = new Harness();
            harness.Tweaks
                .Setup(service => service.SetCoreParkingAsync(true))
                .ReturnsAsync(false);
            var viewModel = harness.CreateViewModel();
            var item = viewModel.TweakItems.Single(tweak => tweak.TweakType == SystemTweak.CoreParking);

            Assert.NotNull(item.ToggleCommand);
            await item.ToggleCommand.ExecuteAsync(item);

            harness.Logging.Verify(
                service => service.LogUserActionAsync(
                    "SystemTweakFailed",
                    "Failed to enable Core Parking",
                    "CoreParking"),
                Times.Once);
            var entry = Assert.Single(await harness.Audit.GetEntriesAsync());
            Assert.Equal("Tweaks", entry.Category);
            Assert.Equal(ActivityAuditSeverity.Error, entry.Severity);
            Assert.Equal("Failed to enable Core Parking", entry.Message);
            Assert.True(viewModel.HasError);
            Assert.Equal("Failed to toggle Core Parking", viewModel.ErrorMessage);
        }

        private sealed class Harness
        {
            public Mock<ISystemTweaksService> Tweaks { get; } = new(MockBehavior.Loose);

            public Mock<INotificationService> Notifications { get; } = new(MockBehavior.Loose);

            public Mock<IEnhancedLoggingService> Logging { get; } = new(MockBehavior.Loose);

            public Mock<ILocalizationService> Localization { get; } = new(MockBehavior.Loose);

            public ActivityAuditService Audit { get; } = new(NullLogger<ActivityAuditService>.Instance);

            public void SetupTweak(SystemTweak tweakType, bool setResult)
            {
                switch (tweakType)
                {
                    case SystemTweak.GameMode:
                        this.Tweaks.Setup(service => service.SetGameModeAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetGameModeStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.CoreParking:
                        this.Tweaks.Setup(service => service.SetCoreParkingAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetCoreParkingStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.CStates:
                        this.Tweaks.Setup(service => service.SetCStatesAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetCStatesStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.UsbSelectiveSuspend:
                        this.Tweaks.Setup(service => service.SetUsbSelectiveSuspendAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetUsbSelectiveSuspendStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.PointerPrecision:
                        this.Tweaks.Setup(service => service.SetPointerPrecisionAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetPointerPrecisionStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.EthernetPowerSaving:
                        this.Tweaks.Setup(service => service.SetEthernetPowerSavingAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetEthernetPowerSavingStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.InterruptModeration:
                        this.Tweaks.Setup(service => service.SetInterruptModerationAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetInterruptModerationStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.GpuMsiMode:
                        this.Tweaks.Setup(service => service.SetGpuMsiModeAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetGpuMsiModeStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.SysMain:
                        this.Tweaks.Setup(service => service.SetSysMainAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetSysMainStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.Prefetch:
                        this.Tweaks.Setup(service => service.SetPrefetchAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetPrefetchStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.PowerThrottling:
                        this.Tweaks.Setup(service => service.SetPowerThrottlingAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetPowerThrottlingStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.HighSchedulingCategory:
                        this.Tweaks.Setup(service => service.SetHighSchedulingCategoryAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetHighSchedulingCategoryStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.MenuShowDelay:
                        this.Tweaks.Setup(service => service.SetMenuShowDelayAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetMenuShowDelayStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(tweakType), tweakType, null);
                }
            }

            public void VerifySetCalled(SystemTweak tweakType)
            {
                switch (tweakType)
                {
                    case SystemTweak.GameMode:
                        this.Tweaks.Verify(service => service.SetGameModeAsync(true), Times.Once);
                        break;
                    case SystemTweak.CoreParking:
                        this.Tweaks.Verify(service => service.SetCoreParkingAsync(true), Times.Once);
                        break;
                    case SystemTweak.CStates:
                        this.Tweaks.Verify(service => service.SetCStatesAsync(true), Times.Once);
                        break;
                    case SystemTweak.UsbSelectiveSuspend:
                        this.Tweaks.Verify(service => service.SetUsbSelectiveSuspendAsync(true), Times.Once);
                        break;
                    case SystemTweak.PointerPrecision:
                        this.Tweaks.Verify(service => service.SetPointerPrecisionAsync(true), Times.Once);
                        break;
                    case SystemTweak.EthernetPowerSaving:
                        this.Tweaks.Verify(service => service.SetEthernetPowerSavingAsync(true), Times.Once);
                        break;
                    case SystemTweak.InterruptModeration:
                        this.Tweaks.Verify(service => service.SetInterruptModerationAsync(true), Times.Once);
                        break;
                    case SystemTweak.GpuMsiMode:
                        this.Tweaks.Verify(service => service.SetGpuMsiModeAsync(true), Times.Once);
                        break;
                    case SystemTweak.SysMain:
                        this.Tweaks.Verify(service => service.SetSysMainAsync(true), Times.Once);
                        break;
                    case SystemTweak.Prefetch:
                        this.Tweaks.Verify(service => service.SetPrefetchAsync(true), Times.Once);
                        break;
                    case SystemTweak.PowerThrottling:
                        this.Tweaks.Verify(service => service.SetPowerThrottlingAsync(true), Times.Once);
                        break;
                    case SystemTweak.HighSchedulingCategory:
                        this.Tweaks.Verify(service => service.SetHighSchedulingCategoryAsync(true), Times.Once);
                        break;
                    case SystemTweak.MenuShowDelay:
                        this.Tweaks.Verify(service => service.SetMenuShowDelayAsync(true), Times.Once);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(tweakType), tweakType, null);
                }
            }

            public SystemTweaksViewModel CreateViewModel() =>
                new(
                    this.Tweaks.Object,
                    this.Notifications.Object,
                    this.Localization.Object,
                    NullLogger<SystemTweaksViewModel>.Instance,
                    this.Logging.Object,
                    this.Audit);

            private static TweakStatus CreateEnabledStatus() =>
                new() { IsEnabled = true, IsAvailable = true };
        }
    }
}
