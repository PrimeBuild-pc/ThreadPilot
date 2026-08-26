namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.ViewModels;

    public sealed class BaseViewModelStatusTests
    {
        [Fact]
        public void ClearStatus_DoesNotClearCriticalStatus()
        {
            var viewModel = new TestViewModel();

            viewModel.SetCritical("Realtime priority is blocked.");
            viewModel.Clear();

            Assert.Equal("Realtime priority is blocked.", viewModel.StatusMessage);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public void ClearStatus_KeepsACompletionMessage()
        {
            // The regression this covers: an apply succeeded, set its status, and the operation's
            // own finally wiped it before the user could read it - while failures stayed up.
            var viewModel = new TestViewModel();

            viewModel.SetCompletion("CPU assignment applied successfully to cs2 using AffinityMask.");
            viewModel.Clear();

            Assert.Equal("CPU assignment applied successfully to cs2 using AffinityMask.", viewModel.StatusMessage);
            Assert.False(viewModel.IsBusy);
        }

        [Fact]
        public void ClearStatus_StillClearsAProgressMessage()
        {
            var viewModel = new TestViewModel();

            viewModel.SetProgress("Setting affinity for cs2...");
            viewModel.Clear();

            Assert.Equal(string.Empty, viewModel.StatusMessage);
            Assert.False(viewModel.IsBusy);
        }

        private sealed class TestViewModel : BaseViewModel
        {
            public TestViewModel()
                : base(NullLogger<TestViewModel>.Instance)
            {
            }

            public void SetCritical(string message) => this.SetCriticalStatus(message);

            public void SetCompletion(string message) => this.SetStatus(message, false);

            public void SetProgress(string message) => this.SetStatus(message);

            public void Clear() => this.ClearStatus();
        }
    }
}
