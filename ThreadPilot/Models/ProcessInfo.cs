using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a process.
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Gets or sets the process ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the process name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path to the process executable.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process CPU utilization percentage.
        /// </summary>
        public float CpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets the process memory usage in MB.
        /// </summary>
        public float MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the process priority.
        /// </summary>
        public ProcessPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the process thread count.
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the process start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the process CPU time.
        /// </summary>
        public TimeSpan CpuTime { get; set; }

        /// <summary>
        /// Gets or sets the process affinity mask.
        /// </summary>
        public long AffinityMask { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is 64-bit.
        /// </summary>
        public bool Is64Bit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is elevated.
        /// </summary>
        public bool IsElevated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is responding.
        /// </summary>
        public bool IsResponding { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the process is a Windows Store app.
        /// </summary>
        public bool IsWindowsStoreApp { get; set; }

        /// <summary>
        /// Gets or sets the process window title.
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process company name.
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process threads.
        /// </summary>
        public List<ProcessThreadInfo> Threads { get; set; } = new List<ProcessThreadInfo>();

        /// <summary>
        /// Tries to set the process priority.
        /// </summary>
        /// <param name="priority">The priority to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool TrySetPriority(ProcessPriority priority)
        {
            try
            {
                using var process = Process.GetProcessById(Id);
                process.PriorityClass = MapToProcessPriorityClass(priority);
                Priority = priority;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tries to set the process affinity.
        /// </summary>
        /// <param name="affinityMask">The affinity mask to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool TrySetAffinity(long affinityMask)
        {
            try
            {
                using var process = Process.GetProcessById(Id);
                process.ProcessorAffinity = new IntPtr(affinityMask);
                AffinityMask = affinityMask;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Maps a ProcessPriority to a ProcessPriorityClass.
        /// </summary>
        /// <param name="priority">The ProcessPriority.</param>
        /// <returns>The corresponding ProcessPriorityClass.</returns>
        private static ProcessPriorityClass MapToProcessPriorityClass(ProcessPriority priority)
        {
            return priority switch
            {
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal,
            };
        }
    }

    /// <summary>
    /// Represents information about a process thread.
    /// </summary>
    public class ProcessThreadInfo
    {
        /// <summary>
        /// Gets or sets the thread ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ideal processor for the thread.
        /// </summary>
        public int IdealProcessor { get; set; }

        /// <summary>
        /// Gets or sets the thread priority.
        /// </summary>
        public ThreadPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the thread start address.
        /// </summary>
        public IntPtr StartAddress { get; set; }

        /// <summary>
        /// Gets or sets the thread CPU utilization percentage.
        /// </summary>
        public float CpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets the thread start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the thread CPU time.
        /// </summary>
        public TimeSpan CpuTime { get; set; }

        /// <summary>
        /// Gets or sets the thread state.
        /// </summary>
        public ThreadState ThreadState { get; set; }

        /// <summary>
        /// Gets or sets the thread wait reason.
        /// </summary>
        public ThreadWaitReason? WaitReason { get; set; }
    }

    /// <summary>
    /// Represents the priority of a process.
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle process priority.
        /// </summary>
        Idle,

        /// <summary>
        /// Below normal process priority.
        /// </summary>
        BelowNormal,

        /// <summary>
        /// Normal process priority.
        /// </summary>
        Normal,

        /// <summary>
        /// Above normal process priority.
        /// </summary>
        AboveNormal,

        /// <summary>
        /// High process priority.
        /// </summary>
        High,

        /// <summary>
        /// Real-time process priority.
        /// </summary>
        RealTime
    }

    /// <summary>
    /// Represents the priority of a thread.
    /// </summary>
    public enum ThreadPriority
    {
        /// <summary>
        /// Idle thread priority.
        /// </summary>
        Idle,

        /// <summary>
        /// Lowest thread priority.
        /// </summary>
        Lowest,

        /// <summary>
        /// Below normal thread priority.
        /// </summary>
        BelowNormal,

        /// <summary>
        /// Normal thread priority.
        /// </summary>
        Normal,

        /// <summary>
        /// Above normal thread priority.
        /// </summary>
        AboveNormal,

        /// <summary>
        /// Highest thread priority.
        /// </summary>
        Highest,

        /// <summary>
        /// Time-critical thread priority.
        /// </summary>
        TimeCritical
    }

    /// <summary>
    /// Represents the state of a thread.
    /// </summary>
    public enum ThreadState
    {
        /// <summary>
        /// Thread is initialized but not yet scheduled.
        /// </summary>
        Initialized,

        /// <summary>
        /// Thread is ready to run.
        /// </summary>
        Ready,

        /// <summary>
        /// Thread is running.
        /// </summary>
        Running,

        /// <summary>
        /// Thread is in standby (scheduled to run next).
        /// </summary>
        Standby,

        /// <summary>
        /// Thread is terminated.
        /// </summary>
        Terminated,

        /// <summary>
        /// Thread is waiting for an event to occur.
        /// </summary>
        Wait,

        /// <summary>
        /// Thread is in transition.
        /// </summary>
        Transition,

        /// <summary>
        /// Thread is in unknown state.
        /// </summary>
        Unknown
    }
}