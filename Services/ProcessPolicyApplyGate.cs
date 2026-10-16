namespace ThreadPilot.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// One gate for everything that applies policies to running processes on its own (persistent rules,
    /// Game Boost), so the two never write CPU Sets or priorities to the same process at the same time.
    /// </summary>
    public sealed class ProcessPolicyApplyGate
    {
        private readonly SemaphoreSlim semaphore = new(1, 1);

        public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
        {
            await this.semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(this.semaphore);
        }

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
        {
            private int released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.released, 1) == 0)
                {
                    semaphore.Release();
                }
            }
        }
    }
}
