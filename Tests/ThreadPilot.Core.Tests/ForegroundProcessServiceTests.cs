namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    public sealed class ForegroundProcessServiceTests
    {
        [Fact]
        public void TryGetForegroundProcessId_ReturnsPidFromVisibleForegroundWindow()
        {
            var provider = new FakeForegroundWindowProvider(
                new ForegroundWindowSnapshot(new IntPtr(42), 1234, true, false));
            var service = new ForegroundProcessService(provider, NullLogger<ForegroundProcessService>.Instance);

            var result = service.TryGetForegroundProcessId();

            Assert.Equal(1234, result);
        }

        [Theory]
        [InlineData(0, true, false)]
        [InlineData(1234, false, false)]
        [InlineData(1234, true, true)]
        public void TryGetForegroundProcessId_ReturnsNullForInvalidForegroundWindow(int processId, bool isVisible, bool isCloaked)
        {
            var provider = new FakeForegroundWindowProvider(
                new ForegroundWindowSnapshot(new IntPtr(42), processId, isVisible, isCloaked));
            var service = new ForegroundProcessService(provider, NullLogger<ForegroundProcessService>.Instance);

            var result = service.TryGetForegroundProcessId();

            Assert.Null(result);
        }

        [Fact]
        public void TryGetForegroundProcessId_ReturnsNullWhenProviderFails()
        {
            var provider = new FakeForegroundWindowProvider(null);
            var service = new ForegroundProcessService(provider, NullLogger<ForegroundProcessService>.Instance);

            var result = service.TryGetForegroundProcessId();

            Assert.Null(result);
        }

        private sealed class FakeForegroundWindowProvider : IForegroundWindowProvider
        {
            private readonly ForegroundWindowSnapshot? snapshot;

            public FakeForegroundWindowProvider(ForegroundWindowSnapshot? snapshot)
            {
                this.snapshot = snapshot;
            }

            public bool TryGetForegroundWindow(out ForegroundWindowSnapshot snapshot)
            {
                if (this.snapshot == null)
                {
                    snapshot = default;
                    return false;
                }

                snapshot = this.snapshot.Value;
                return true;
            }
        }
    }
}
