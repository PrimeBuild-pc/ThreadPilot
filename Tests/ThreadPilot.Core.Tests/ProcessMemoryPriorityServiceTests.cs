/*
 * ThreadPilot - process memory priority service tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.Services;

    public sealed class ProcessMemoryPriorityServiceTests
    {
        [Fact]
        public async Task SetMemoryPriorityAsync_WithValidProcess_CallsNativeApi()
        {
            var nativeApi = new FakeProcessMemoryPriorityNativeApi();
            var service = CreateService(nativeApi);
            var process = CreateProcess();

            var result = await service.SetMemoryPriorityAsync(process, ProcessMemoryPriority.Low);

            Assert.True(result.Success);
            Assert.Equal(ProcessMemoryPriority.Low, nativeApi.LastSetPriority);
            Assert.Equal(ProcessAccessFlags.PROCESS_SET_INFORMATION, nativeApi.LastOpenAccess);
            Assert.Equal("Memory priority applied.", result.UserMessage);
        }

        [Fact]
        public async Task GetMemoryPriorityAsync_WithValidProcess_ReadsNativeApi()
        {
            var nativeApi = new FakeProcessMemoryPriorityNativeApi
            {
                PriorityToReturn = ProcessMemoryPriority.BelowNormal,
            };
            var service = CreateService(nativeApi);

            var priority = await service.GetMemoryPriorityAsync(CreateProcess());

            Assert.Equal(ProcessMemoryPriority.BelowNormal, priority);
            Assert.Equal(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, nativeApi.LastOpenAccess);
        }

        [Theory]
        [InlineData(1, ProcessMemoryPriority.VeryLow)]
        [InlineData(2, ProcessMemoryPriority.Low)]
        [InlineData(3, ProcessMemoryPriority.Medium)]
        [InlineData(4, ProcessMemoryPriority.BelowNormal)]
        [InlineData(5, ProcessMemoryPriority.Normal)]
        public void ProcessMemoryPriority_UsesDocumentedWindowsLevels(int windowsLevel, ProcessMemoryPriority priority)
        {
            Assert.Equal(windowsLevel, (int)priority);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WithNullProcess_ReturnsControlledFailure()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi());

            var result = await service.SetMemoryPriorityAsync(null!, ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.Equal("InvalidProcess", result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.ProcessExited, result.UserMessage);
            Assert.NotEqual(ProcessMemoryPriorityService.UnsupportedUserMessage, result.UserMessage);
            Assert.False(result.IsAccessDenied);
            Assert.False(result.IsProcessExited);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WithInvalidProcess_DoesNotReturnUnsupportedWindowsMessage()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi());

            var result = await service.SetMemoryPriorityAsync(new ProcessModel { ProcessId = 0 }, ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.Equal("InvalidProcess", result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.ProcessExited, result.UserMessage);
            Assert.NotEqual(ProcessMemoryPriorityService.UnsupportedUserMessage, result.UserMessage);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WithInvalidPriority_ReturnsInvalidPriorityMessage()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi());

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), (ProcessMemoryPriority)99);

            Assert.False(result.Success);
            Assert.Equal("InvalidMemoryPriority", result.ErrorCode);
            Assert.Equal("This memory priority value is not supported.", result.UserMessage);
            Assert.NotEqual(ProcessMemoryPriorityService.UnsupportedUserMessage, result.UserMessage);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WhenProcessExited_ReturnsProcessExitedFailure()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi
            {
                OpenException = new InvalidOperationException("The process has exited."),
            });

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.True(result.IsProcessExited);
            Assert.Equal(AffinityApplyErrorCodes.ProcessExited, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.ProcessExited, result.UserMessage);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WhenAccessDenied_ReturnsSafeAccessDeniedFailure()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi
            {
                SetException = new Win32Exception(5, "Access is denied."),
            });

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.True(result.IsAccessDenied);
            Assert.Equal(AffinityApplyErrorCodes.AccessDenied, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.AccessDenied, result.UserMessage);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WhenProtectedByAntiCheat_ReturnsMessageWithoutBypassPromise()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi
            {
                SetException = new UnauthorizedAccessException("Protected anti-cheat process."),
            });

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.True(result.IsAccessDenied);
            Assert.True(result.IsAntiCheatLikely);
            Assert.Equal(AffinityApplyErrorCodes.AntiCheatOrProtectedProcessLikely, result.ErrorCode);
            Assert.Equal(ProcessOperationUserMessages.AntiCheatProtectedLikely, result.UserMessage);
            Assert.Equal(
                "The process appears protected by anti-cheat or process protection. ThreadPilot will not try to bypass it.",
                ProcessOperationUserMessages.AntiCheatProtectedLikely);
            Assert.DoesNotContain("disable anti-cheat", result.UserMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("administrator", result.UserMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cannot bypass anti-cheat", ProcessOperationUserMessages.AdminClarification);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WhenUnsupported_ReturnsControlledFailure()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi { IsSupported = false });

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.Equal("Unsupported", result.ErrorCode);
            Assert.Equal(ProcessMemoryPriorityService.UnsupportedUserMessage, result.UserMessage);
        }

        [Fact]
        public async Task SetMemoryPriorityAsync_WhenNativeCallFails_ReturnsControlledFailure()
        {
            var service = CreateService(new FakeProcessMemoryPriorityNativeApi
            {
                SetResult = false,
                LastError = 31,
            });

            var result = await service.SetMemoryPriorityAsync(CreateProcess(), ProcessMemoryPriority.Normal);

            Assert.False(result.Success);
            Assert.Equal(AffinityApplyErrorCodes.NativeApplyFailed, result.ErrorCode);
            Assert.Contains("SetProcessInformation(ProcessMemoryPriority) failed", result.TechnicalMessage);
        }

        private static ProcessMemoryPriorityService CreateService(IProcessMemoryPriorityNativeApi nativeApi) =>
            new(nativeApi, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessMemoryPriorityService>.Instance);

        private static ProcessModel CreateProcess() =>
            new()
            {
                ProcessId = 42,
                Name = "game.exe",
                ExecutablePath = @"C:\Games\Game.exe",
            };

        private sealed class FakeProcessMemoryPriorityNativeApi : IProcessMemoryPriorityNativeApi
        {
            public bool IsSupported { get; init; } = true;

            public ProcessAccessFlags LastOpenAccess { get; private set; }

            public ProcessMemoryPriority? LastSetPriority { get; private set; }

            public ProcessMemoryPriority PriorityToReturn { get; init; } = ProcessMemoryPriority.Normal;

            public Exception? OpenException { get; init; }

            public Exception? SetException { get; init; }

            public bool SetResult { get; init; } = true;

            public int LastError { get; init; }

            public SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId)
            {
                this.LastOpenAccess = access;
                if (this.OpenException != null)
                {
                    throw this.OpenException;
                }

                return new SafeProcessHandle(new IntPtr(1), ownsHandle: false);
            }

            public bool GetProcessInformation(
                SafeProcessHandle process,
                ProcessInformationClass processInformationClass,
                ref MemoryPriorityInformation processInformation,
                uint processInformationSize)
            {
                processInformation.MemoryPriority = (uint)this.PriorityToReturn;
                return true;
            }

            public bool SetProcessInformation(
                SafeProcessHandle process,
                ProcessInformationClass processInformationClass,
                ref MemoryPriorityInformation processInformation,
                uint processInformationSize)
            {
                if (this.SetException != null)
                {
                    throw this.SetException;
                }

                this.LastSetPriority = (ProcessMemoryPriority)processInformation.MemoryPriority;
                return this.SetResult;
            }

            public int GetLastWin32Error() => this.LastError;
        }
    }
}
