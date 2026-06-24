namespace ThreadPilot.Models
{
    using System;
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;

    public enum ProcessClassification
    {
        ForegroundApp,
        VisibleWindowApp,
        BackgroundUser,
        System,
        ProtectedOrAccessDenied,
        Terminated,
        Unknown,
    }

    public partial class ProcessModel : ObservableObject
    {
        [ObservableProperty]
        private int processId;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string executablePath = string.Empty;

        [ObservableProperty]
        private double cpuUsage;

        [ObservableProperty]
        private long memoryUsage;

        [ObservableProperty]
        private ProcessPriorityClass priority;

        [ObservableProperty]
        private long processorAffinity;

        [ObservableProperty]
        private IntPtr mainWindowHandle;

        [ObservableProperty]
        private string mainWindowTitle = string.Empty;

        [ObservableProperty]
        private bool hasVisibleWindow;

        [ObservableProperty]
        private bool isForeground;

        [ObservableProperty]
        private ProcessClassification classification = ProcessClassification.Unknown;

        [ObservableProperty]
        private bool isIdleServerDisabled;

        [ObservableProperty]
        private bool isRegistryPriorityEnabled;

        public void ForceNotifyProcessorAffinityChanged()
        {
            this.OnPropertyChanged(nameof(this.ProcessorAffinity));
        }
    }
}
