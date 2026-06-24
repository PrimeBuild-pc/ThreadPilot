namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface ICpuTopologyService
    {
        event EventHandler<CpuTopologyDetectedEventArgs>? TopologyDetected;

        CpuTopologyModel? CurrentTopology { get; }

        Task<CpuTopologyModel> DetectTopologyAsync();

        IEnumerable<CpuAffinityPreset> GetAffinityPresets();

        bool IsAffinityMaskValid(long affinityMask);

        int GetMaxLogicalCores();

        Task RefreshTopologyAsync();
    }

    public class CpuTopologyDetectedEventArgs : EventArgs
    {
        public CpuTopologyModel Topology { get; }

        public bool DetectionSuccessful { get; }

        public string? ErrorMessage { get; }

        public DateTime DetectionTime { get; }

        public CpuTopologyDetectedEventArgs(CpuTopologyModel topology, bool successful, string? errorMessage = null)
        {
            this.Topology = topology;
            this.DetectionSuccessful = successful;
            this.ErrorMessage = errorMessage;
            this.DetectionTime = DateTime.Now;
        }
    }
}

