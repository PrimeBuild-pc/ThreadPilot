namespace ThreadPilot.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface ICpuTopologyProvider
    {
        Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default);
    }
}
