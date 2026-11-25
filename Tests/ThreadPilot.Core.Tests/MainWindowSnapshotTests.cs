namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using ThreadPilot.Platforms.Windows;

    public sealed class MainWindowSnapshotTests
    {
        [Fact]
        public void Snapshot_PicksTheSameMainWindowAsProcessMainWindowHandle()
        {
            // Windows can open or close between the two reads, so allow a stray difference or two.
            var snapshot = MainWindowSnapshot.Capture();
            var compared = 0;
            var mismatches = 0;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    nint expected;
                    try
                    {
                        expected = process.MainWindowHandle;
                    }
                    catch (InvalidOperationException)
                    {
                        continue; // exited
                    }

                    compared++;
                    if (snapshot.GetValueOrDefault(process.Id) != expected)
                    {
                        mismatches++;
                    }
                }
            }

            Assert.True(compared > 10);
            Assert.True(mismatches <= 2, $"{mismatches} of {compared} processes disagreed");
        }

        [Fact]
        public void Title_MatchesProcessMainWindowTitle()
        {
            // Some titles change on their own (terminals, players), so a stray difference is allowed here too.
            var mismatches = 0;
            foreach (var (processId, window) in MainWindowSnapshot.Capture())
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    if (process.MainWindowHandle == window && process.MainWindowTitle != MainWindowSnapshot.GetTitle(window))
                    {
                        mismatches++;
                    }
                }
                catch (ArgumentException)
                {
                    // exited
                }
            }

            Assert.True(mismatches <= 2, $"{mismatches} titles disagreed");
        }

        [Fact]
        public void Title_OfNoWindow_IsEmpty()
        {
            Assert.Equal(string.Empty, MainWindowSnapshot.GetTitle(0));
        }
    }
}
