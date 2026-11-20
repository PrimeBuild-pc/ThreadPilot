/*
 * ThreadPilot - persistent process rule store contract.
 */
namespace ThreadPilot.Services
{
    using System.Runtime.CompilerServices;
    using ThreadPilot.Models;

    public interface IPersistentProcessRuleStore
    {
        Task<IReadOnlyList<PersistentProcessRule>> LoadAsync();

        Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules);
    }

    // Writers use the same gate for the entire read/edit/save operation on a store.
    internal static class PersistentRuleEditGate
    {
        private static readonly ConditionalWeakTable<IPersistentProcessRuleStore, SemaphoreSlim> Gates = new();

        public static async Task<IDisposable> EnterAsync(
            IPersistentProcessRuleStore store, CancellationToken cancellationToken = default)
        {
            var gate = Gates.GetValue(store, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Lease(gate);
        }

        private sealed class Lease(SemaphoreSlim gate) : IDisposable
        {
            private SemaphoreSlim? heldGate = gate;

            public void Dispose() => Interlocked.Exchange(ref this.heldGate, null)?.Release();
        }
    }
}
