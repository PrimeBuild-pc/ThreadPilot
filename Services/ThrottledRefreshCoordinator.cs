namespace ThreadPilot.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ThrottledRefreshCoordinator : IDisposable
    {
        private readonly Func<Task> callback;
        private readonly Action<Exception>? onError;
        private readonly object lockObject = new();
        private readonly TimeSpan defaultDelay;
        private System.Threading.Timer? timer;
        private int isExecuting;
        private int disposedFlag;

        public ThrottledRefreshCoordinator(TimeSpan defaultDelay, Func<Task> callback, Action<Exception>? onError = null)
        {
            this.defaultDelay = defaultDelay;
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            this.onError = onError;
        }

        public void Schedule()
        {
            this.Schedule(this.defaultDelay);
        }

        public void Schedule(TimeSpan delay)
        {
            if (Interlocked.CompareExchange(ref this.disposedFlag, 0, 0) == 1)
            {
                return;
            }

            lock (this.lockObject)
            {
                this.timer?.Dispose();
                this.timer = new System.Threading.Timer(this.OnTimerTick, null, delay, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnTimerTick(object? state)
        {
            if (Interlocked.CompareExchange(ref this.disposedFlag, 0, 0) == 1)
            {
                return;
            }

            if (Interlocked.Exchange(ref this.isExecuting, 1) == 1)
            {
                return;
            }

            TaskSafety.FireAndForget(this.ExecuteCallbackAsync(), ex => this.onError?.Invoke(ex));
        }

        private async Task ExecuteCallbackAsync()
        {
            try
            {
                await this.callback().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref this.isExecuting, 0);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposedFlag, 1) == 1)
            {
                return;
            }

            lock (this.lockObject)
            {
                this.timer?.Dispose();
                this.timer = null;
            }
        }
    }
}
