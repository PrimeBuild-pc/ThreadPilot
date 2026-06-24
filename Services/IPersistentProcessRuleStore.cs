/*
 * ThreadPilot - persistent process rule store contract.
 */
namespace ThreadPilot.Services
{
    using ThreadPilot.Models;

    public interface IPersistentProcessRuleStore
    {
        Task<IReadOnlyList<PersistentProcessRule>> LoadAsync();

        Task SaveAsync(IReadOnlyList<PersistentProcessRule> rules);
    }
}
