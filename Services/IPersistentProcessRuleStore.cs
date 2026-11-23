/*
 * ThreadPilot - persistent process rule store contract.
 */
namespace ThreadPilot.Services
{
    using System.Runtime.CompilerServices;
    using ThreadPilot.Models;

    public interface IPersistentProcessRuleStore
    {
        private static readonly ConditionalWeakTable<IPersistentProcessRuleStore, SemaphoreSlim> EditLocks = new();

        Task<IReadOnlyList<PersistentProcessRule>> LoadAsync();

        Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules);

        /// <summary>Edits the current rules atomically. An unavailable rule set aborts the edit.</summary>
        Task<TResult> EditAsync<TResult>(
            Func<List<PersistentProcessRule>, Task<TResult>> edit,
            CancellationToken cancellationToken = default) =>
            this.RunTransactionAsync(
                async () =>
                {
                    var previous = await this.LoadForEditAsync();
                    var draft = previous.ToList();
                    var result = await edit(draft);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!previous.SequenceEqual(draft))
                    {
                        await this.SaveAsync(draft);
                    }

                    return result;
                },
                cancellationToken);

        /// <summary>Replaces rules during an import and restores them if the remaining import fails.</summary>
        Task ReplaceForImportAsync(IReadOnlyList<PersistentProcessRule>? replacement, Func<Task> completeImport) =>
            this.RunTransactionAsync(
                async () =>
                {
                    var previous = replacement == null ? null : await this.LoadForEditAsync();
                    if (replacement != null)
                    {
                        await this.SaveAsync(replacement);
                    }

                    try
                    {
                        await completeImport();
                    }
                    catch (Exception importError) when (previous != null)
                    {
                        try
                        {
                            await this.SaveAsync(previous);
                        }
                        catch (Exception rollbackError)
                        {
                            throw new AggregateException("Import failed and the rule set could not be restored.", importError, rollbackError);
                        }

                        throw;
                    }

                    return true;
                },
                CancellationToken.None);

        protected Task<IReadOnlyList<PersistentProcessRule>> LoadForEditAsync() => this.LoadAsync();

        private async Task<TResult> RunTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            var gate = EditLocks.GetValue(this, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await operation();
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
