namespace ThreadPilot.Core.Tests
{
    using System.IO;
    using ThreadPilot.Services;

    [Collection("UiStateStore")]
    public sealed class UiStateStoreTests
    {
        [Fact]
        public void Update_SurvivesARestart()
        {
            UiStateStore.Update(s => s with { NavigationPaneOpen = false, HideSystemProcesses = true, WindowWidth = 1234 });

            UiStateStore.ResetForTests(); // next read comes from disk, as after a restart
            var state = UiStateStore.Current;

            Assert.False(state.NavigationPaneOpen);
            Assert.True(state.HideSystemProcesses);
            Assert.Equal(1234, state.WindowWidth);
        }

        [Fact]
        public void CorruptFile_FallsBackToDefaults()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UiStateStore.FilePath)!);
            File.WriteAllText(UiStateStore.FilePath, "{ not json");

            UiStateStore.ResetForTests();

            Assert.True(UiStateStore.Current.NavigationPaneOpen);
            File.Delete(UiStateStore.FilePath);
            UiStateStore.ResetForTests();
        }
    }
}
