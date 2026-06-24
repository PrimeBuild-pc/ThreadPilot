/*
 * ThreadPilot - async safety unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class TaskSafetyTests
    {
        [Fact]
        public async Task FireAndForget_InvokesErrorCallback_ForFaultedTask()
        {
            var completion = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var expected = new InvalidOperationException("boom");

            TaskSafety.FireAndForget(Task.FromException(expected), ex => completion.TrySetResult(ex));

            var finishedTask = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(completion.Task, finishedTask);
            var observed = await completion.Task;
            Assert.IsType<InvalidOperationException>(observed);
            Assert.Equal("boom", observed.Message);
        }

        [Fact]
        public async Task FireAndForget_DoesNotInvokeErrorCallback_ForCanceledTask()
        {
            var callbackInvoked = false;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            TaskSafety.FireAndForget(Task.FromCanceled(cancellation.Token), _ => callbackInvoked = true);

            await Task.Delay(150);
            Assert.False(callbackInvoked);
        }
    }
}
