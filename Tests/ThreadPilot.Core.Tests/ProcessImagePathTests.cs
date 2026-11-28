namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using ThreadPilot.Platforms.Windows;

    public sealed class ProcessImagePathTests
    {
        [Fact]
        public void CurrentProcess_MatchesMainModulePath()
        {
            using var self = Process.GetCurrentProcess();

            Assert.Equal(self.MainModule!.FileName, ProcessImagePath.TryGet(Environment.ProcessId), ignoreCase: true);
        }

        [Fact]
        public void ChildProcess_IsReadWithLimitedRightsOnly()
        {
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            using var child = Process.Start(new ProcessStartInfo(cmd, "/c ping -n 3 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false })!;
            try
            {
                Assert.Equal(cmd, ProcessImagePath.TryGet(child.Id), ignoreCase: true);
            }
            finally
            {
                child.Kill();
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MaxValue - 3)]
        public void MissingProcess_ReturnsNull(int processId)
        {
            Assert.Null(ProcessImagePath.TryGet(processId));
        }
    }
}
