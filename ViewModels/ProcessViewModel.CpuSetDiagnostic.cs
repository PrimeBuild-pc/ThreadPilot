namespace ThreadPilot.ViewModels
{
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Platforms.Windows;

    public partial class ProcessViewModel
    {
        internal ICpuSetDiagnosticSource CpuSetDiagnosticSource { get; set; } = ProcessCpuSetDiagnosticReader.Instance;

        [RelayCommand]
        private async Task ReadCpuSetDiagnosticAsync()
        {
            var process = this.SelectedProcess;
            if (process == null)
            {
                this.CpuSetDiagnosticText = this.UiText(
                    "ProcessView_CpuSetDiag_NoProcess",
                    "Select a process to read its CPU Sets. The read does not change the process.");
                return;
            }

            var source = this.CpuSetDiagnosticSource;
            try
            {
                var diagnostic = await Task.Run(() => source.Read(process.ProcessId, process.Name)).ConfigureAwait(true);
                if (!ReferenceEquals(this.SelectedProcess, process))
                {
                    return;
                }

                this.CpuSetDiagnosticText = CpuSetDiagnosticFormatter.Format(diagnostic, this.UiText);
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "CPU Set diagnostic failed for {ProcessName}", process.Name);
                if (ReferenceEquals(this.SelectedProcess, process))
                {
                    this.CpuSetDiagnosticText = this.UiText(
                        "ProcessView_CpuSetDiag_Failed",
                        "Could not read CPU Sets.");
                }
            }
        }
    }
}
