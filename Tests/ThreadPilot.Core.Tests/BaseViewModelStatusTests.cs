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

        private sealed class TestViewModel : BaseViewModel
        {
            public TestViewModel()
                : base(NullLogger<TestViewModel>.Instance)
            {
            }

            public void SetCritical(string message) => this.SetCriticalStatus(message);

            public void Clear() => this.ClearStatus();
        }
    }
}
