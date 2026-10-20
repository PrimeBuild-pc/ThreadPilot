namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Win32.SafeHandles;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Platforms.Windows;
    using ThreadPilot.Services;
    using Xunit.Abstractions;

    /// <summary>
    /// Real processes, real ThreadPilot services and real CPU Set / priority calls. Only the topology is
    /// simulated (this machine's CPU Set IDs split into two L3 groups), so the rollback can be exercised on
    /// CPUs where Game Boost itself would not act. The children are copies of PING.EXE.
    /// </summary>
    public sealed class GameSessionNativeIntegrationTests(ITestOutputHelper output) : IDisposable
    {
        private const ulong MB = 1024UL * 1024UL;

        private readonly string root = Path.Combine(Path.GetTempPath(), $"tp-gb-{Guid.NewGuid():N}");
        private readonly List<Process> children = new();
        private readonly ProcessMemoryPriorityService efficiency = new(ProcessMemoryPriorityNativeApi.Instance, NullLogger<ProcessMemoryPriorityService>.Instance);

        public void Dispose()
        {
            foreach (var child in this.children)
            {
                try
                {
                    child.Kill();
                    child.WaitForExit(5000);
                }
                catch
                {
                    // already gone
                }

                child.Dispose();
            }

            try
            {
                Directory.Delete(this.root, recursive: true);
            }
            catch
            {
                // best effort
            }
        }

        [Fact]
        public async Task RealProcesses_ApplyThenRestore_AndRecoverAfterCrash()
        {
            var real = await new WindowsCpuTopologyProvider().GetTopologySnapshotAsync();
            var processors = real.LogicalProcessors.ToList();
            var cores = processors.Select(p => real.TryGetCoreIndex(p, out var core) ? core : -1).ToList();
            var gameCount = processors.Count * 3 / 4;
            if (processors.Any(p => !real.TryGetCpuSetId(p, out _))
                || cores.Take(gameCount).Distinct().Count() < GameBoostPolicy.MinimumGamePhysicalCores
                || gameCount == processors.Count)
            {
                output.WriteLine($"Skipped: {processors.Count} logical CPUs are too few to split into a 6-core game group.");
                return;
            }

            var topology = CpuTopologySnapshot.Create(
                processors,
                cpuSetIds: processors.ToDictionary(p => p, p => real.TryGetCpuSetId(p, out var id) ? id : 0U),
                coreIndexes: processors.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => cores[x.i]),
                lastLevelCacheIndexes: processors.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i < gameCount ? 0 : 1),
                lastLevelCacheSizes: new Dictionary<int, ulong> { [0] = 96 * MB, [1] = 32 * MB });
            var gameCpus = Enumerable.Range(0, gameCount).Select(i => processors[i].GlobalIndex).ToList();
            var backgroundCpus = processors.Skip(gameCount).Select(p => p.GlobalIndex).ToList();

            var game = this.StartCopyOfPing(Path.Combine(this.root, "steamapps", "common", "FakeGame"), "FakeGame.exe");
            var browserPath = Path.Combine(this.root, "apps", "chrome.exe");
            var browser = this.StartCopyOfPing(Path.GetDirectoryName(browserPath)!, "chrome.exe");
            var journal = Path.Combine(this.root, "journal.json");
            // The exact path, so a real browser running on this machine is never touched.
            var settings = new ApplicationSettingsModel
            {
                GameBoostEnabled = true,
                GameBoostAboveNormalPriority = true,
                GameBoostMode = GameBoostMode.Isolation,
                GameBoostBackgroundProcesses = [browserPath],
            };

            // 1. Session: real CPU Sets and priority on both children.
            var first = this.CreateService(topology, settings, game.Id, journal);
            await first.TickAsync();
            Assert.Equal(gameCpus, ReadDefaultCpuSetIndexes(game, topology));
            Assert.Equal(backgroundCpus, ReadDefaultCpuSetIndexes(browser, topology));
            Assert.Equal(ProcessPriorityClass.AboveNormal, ReadPriority(game));
            Assert.Equal(ProcessEfficiencyMode.Off, await this.ReadEfficiencyAsync(game));
            Assert.Equal(ProcessEfficiencyMode.On, await this.ReadEfficiencyAsync(browser));
            Assert.True(File.Exists(journal));

            // 2. Turned off: everything restored without a restart.
            settings.GameBoostEnabled = false;
            await first.ReapplyAsync();
            Assert.Empty(ReadDefaultCpuSetIndexes(game, topology));
            Assert.Empty(ReadDefaultCpuSetIndexes(browser, topology));
            Assert.Equal(ProcessPriorityClass.Normal, ReadPriority(game));
            Assert.Equal(ProcessEfficiencyMode.SystemManaged, await this.ReadEfficiencyAsync(game));
            Assert.Equal(ProcessEfficiencyMode.SystemManaged, await this.ReadEfficiencyAsync(browser));
            Assert.False(File.Exists(journal));

            // 3. On again, then a "crash": the service is dropped without StopAsync.
            settings.GameBoostEnabled = true;
            await first.TickAsync();
            Assert.Equal(backgroundCpus, ReadDefaultCpuSetIndexes(browser, topology));
            first.Dispose();

            // 4. Next start reads the journal and undoes the crashed run.
            using var second = this.CreateService(topology, new ApplicationSettingsModel(), game.Id, journal);
            await second.StartAsync();
            Assert.Empty(ReadDefaultCpuSetIndexes(game, topology));
            Assert.Empty(ReadDefaultCpuSetIndexes(browser, topology));
            Assert.Equal(ProcessPriorityClass.Normal, ReadPriority(game));
            Assert.Equal(ProcessEfficiencyMode.SystemManaged, await this.ReadEfficiencyAsync(browser));
            Assert.False(File.Exists(journal));
            await second.StopAsync();
        }

        private GameSessionService CreateService(CpuTopologySnapshot topology, ApplicationSettingsModel settings, int gamePid, string journal)
        {
            var settingsService = new Mock<IApplicationSettingsService>();
            settingsService.SetupGet(s => s.Settings).Returns(settings);
            var topologyProvider = new Mock<ICpuTopologyProvider>();
            topologyProvider.Setup(p => p.GetTopologySnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(topology);
            var cpuTopologyService = new Mock<ICpuTopologyService>();
            cpuTopologyService.Setup(s => s.DetectTopologyAsync()).ReturnsAsync(new CpuTopologyModel { CpuBrand = "AMD (simulated dual L3)" });
            var foreground = new Mock<IForegroundProcessService>();
            foreground.Setup(f => f.TryGetForegroundProcessId()).Returns(gamePid);
            var rules = new Mock<IPersistentProcessRuleStore>();
            rules.Setup(s => s.LoadAsync()).ReturnsAsync(Array.Empty<PersistentProcessRule>());

            var processService = new ProcessService(NullLogger<ProcessService>.Instance);
            var affinityService = new AffinityApplyService(
                processService,
                new CpuTopologyService(NullLogger<CpuTopologyService>.Instance),
                NullLogger<AffinityApplyService>.Instance);

            return new GameSessionService(
                settingsService.Object,
                topologyProvider.Object,
                cpuTopologyService.Object,
                foreground.Object,
                processService,
                affinityService,
                rules.Object,
                new PersistentProcessRuleMatcher(),
                new ProcessFilterService(),
                new ProcessPolicyApplyGate(),
                new GameBoostProcessProbe(),
                this.efficiency,
                NullLogger<GameSessionService>.Instance,
                journal);
        }

        private Process StartCopyOfPing(string directory, string fileName)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), path);
            var process = Process.Start(new ProcessStartInfo(path, "-n 120 127.0.0.1")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            this.children.Add(process);
            return process;
        }

        private Task<ProcessEfficiencyMode?> ReadEfficiencyAsync(Process process) =>
            this.efficiency.GetEfficiencyModeAsync(new ProcessModel { ProcessId = process.Id, Name = process.ProcessName });

        private static ProcessPriorityClass ReadPriority(Process process)
        {
            process.Refresh();
            return process.PriorityClass;
        }

        // Read straight from Windows, not through ThreadPilot's handler.
        private static List<int> ReadDefaultCpuSetIndexes(Process process, CpuTopologySnapshot topology)
        {
            using var handle = OpenProcess(0x1000, false, process.Id); // PROCESS_QUERY_LIMITED_INFORMATION
            Assert.False(handle.IsInvalid);
            GetProcessDefaultCpuSets(handle, null, 0, out var count);
            var ids = new uint[count];
            if (count > 0)
            {
                Assert.True(GetProcessDefaultCpuSets(handle, ids, count, out count));
            }

            var byId = topology.LogicalProcessors.ToDictionary(p => topology.TryGetCpuSetId(p, out var id) ? id : 0U, p => p.GlobalIndex);
            return ids.Take((int)count).Select(id => byId[id]).OrderBy(i => i).ToList();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessDefaultCpuSets(SafeProcessHandle process, uint[]? cpuSetIds, uint cpuSetIdCount, out uint requiredIdCount);
    }
}
