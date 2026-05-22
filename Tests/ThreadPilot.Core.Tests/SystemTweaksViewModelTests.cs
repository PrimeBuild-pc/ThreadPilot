namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public sealed class SystemTweaksViewModelTests
    {
        [Theory]
        [InlineData(SystemTweak.CoreParking, "Core Parking")]
        [InlineData(SystemTweak.CStates, "C-States")]
        [InlineData(SystemTweak.SysMain, "SysMain Service")]
        [InlineData(SystemTweak.Prefetch, "Prefetch")]
        [InlineData(SystemTweak.PowerThrottling, "Power Throttling")]
        [InlineData(SystemTweak.Hpet, "HPET")]
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
            Assert.True(viewModel.HasError);
            Assert.Equal("Failed to toggle Core Parking", viewModel.ErrorMessage);
        }

        private sealed class Harness
        {
            public Mock<ISystemTweaksService> Tweaks { get; } = new(MockBehavior.Loose);

            public Mock<INotificationService> Notifications { get; } = new(MockBehavior.Loose);

            public Mock<IEnhancedLoggingService> Logging { get; } = new(MockBehavior.Loose);

            public void SetupTweak(SystemTweak tweakType, bool setResult)
            {
                switch (tweakType)
                {
                    case SystemTweak.CoreParking:
                        this.Tweaks.Setup(service => service.SetCoreParkingAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetCoreParkingStatusAsync()).ReturnsAsync(CreateEnabledStatus());
                        break;
                    case SystemTweak.CStates:
                        this.Tweaks.Setup(service => service.SetCStatesAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetCStatesStatusAsync()).ReturnsAsync(CreateEnabledStatus());
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
                    case SystemTweak.Hpet:
                        this.Tweaks.Setup(service => service.SetHpetAsync(true)).ReturnsAsync(setResult);
                        this.Tweaks.Setup(service => service.GetHpetStatusAsync()).ReturnsAsync(CreateEnabledStatus());
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
                    case SystemTweak.CoreParking:
                        this.Tweaks.Verify(service => service.SetCoreParkingAsync(true), Times.Once);
                        break;
                    case SystemTweak.CStates:
                        this.Tweaks.Verify(service => service.SetCStatesAsync(true), Times.Once);
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
                    case SystemTweak.Hpet:
                        this.Tweaks.Verify(service => service.SetHpetAsync(true), Times.Once);
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
                    NullLogger<SystemTweaksViewModel>.Instance,
                    this.Logging.Object);

            private static TweakStatus CreateEnabledStatus() =>
                new() { IsEnabled = true, IsAvailable = true };
        }
    }
}
