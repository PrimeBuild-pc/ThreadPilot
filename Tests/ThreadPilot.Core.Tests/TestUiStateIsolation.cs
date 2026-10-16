namespace ThreadPilot.Core.Tests
{
    using System.IO;
    using System.Runtime.CompilerServices;
    using ThreadPilot.Services;

    internal static class TestUiStateIsolation
    {
        // View models read and write UI layout state; keep tests away from the real user's file.
        [ModuleInitializer]
        internal static void Initialize() =>
            UiStateStore.FilePath = Path.Combine(Path.GetTempPath(), $"threadpilot-tests-{Environment.ProcessId}", "ui-state.json");
    }
}
