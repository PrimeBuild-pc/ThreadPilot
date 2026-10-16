namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class GameSessionServiceTests : IDisposable
    {
        private const int GamePid = 100;
        private const int ChromePid = 200;
        private const int DiscordPid = 300;
        private const ulong MB = 1024UL * 1024UL;

        private static readonly int[] VCacheCpus = Enumerable.Range(0, 16).ToArray();
        private static readonly int[] OtherCcdCpus = Enumerable.Range(16, 16).ToArray();

        private readonly string journalPath = Path.Combine(Path.GetTempPath(), $"tp-gameboost-{Guid.NewGuid():N}.json");
        private readonly ApplicationSettingsModel settings = new() { GameBoostEnabled = true };
        private readonly Mock<IApplicationSettingsService> settingsService = new();
        private readonly FakeProcesses processes = new();
        private readonly List<PersistentProcessRule> rules = new();
        private readonly ActivityAuditService audit = new(NullLogger<ActivityAuditService>.Instance);
        private int? foregroundPid = GamePid;

        public GameSessionServiceTests()
        {
            this.settingsService.SetupGet(s => s.Settings).Returns(() => this.settings);
            this.processes.Add(GamePid, "Game", @"D:\SteamLibrary\steamapps\common\Game\Game.exe");
            this.processes.Add(ChromePid, "chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
            this.processes.Add(DiscordPid, "Discord", @"C:\Users\u\AppData\Local\Discord\app-1\Discord.exe");
        }

        public void Dispose() => File.Delete(this.journalPath);

        [Fact]
        public async Task GameInLibrary_GameOnVCache_ListedBackgroundOnOtherCcd()
        {
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[ChromePid]);
            Assert.Empty(this.processes.CpuSets[DiscordPid]);
            Assert.Equal("Game", service.ActiveGameName);
            Assert.True(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task NotAGame_NoSession()
        {
            this.foregroundPid = ChromePid;
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.Null(service.ActiveGameName);
            Assert.All(this.processes.CpuSets.Values, Assert.Empty);
        }

        [Fact]
        public async Task GameExit_RestoresBackground_AndDeletesJournal()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Empty(this.processes.CpuSets[ChromePid]);
            Assert.Null(service.ActiveGameName);
            Assert.False(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task AltTab_KeepsTheSession()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.foregroundPid = ChromePid;
            await service.TickAsync();

            Assert.Equal("Game", service.ActiveGameName);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);
        }

        [Fact]
        public async Task ExistingCpuSet_IsSomeoneElsesChoice_NotTouched()
        {
            this.processes.CpuSets[ChromePid] = [2, 3];
            using var service = this.CreateService();

            await service.TickAsync();
            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Equal([2, 3], this.processes.CpuSets[ChromePid]);
        }

        [Fact]
        public async Task UnreadableCpuSet_NotTouched()
        {
            this.processes.Unreadable.Add(ChromePid);
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.DoesNotContain(ChromePid, this.processes.ApplyCalls);
        }

        [Fact]
        public async Task UserRuleWithCores_WinsOverGameBoost()
        {
            this.rules.Add(new PersistentProcessRule { IsEnabled = true, ProcessName = "Game", LegacyAffinityMask = 0xF });
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.Empty(this.processes.CpuSets[GamePid]);
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[ChromePid]);
        }

        [Fact]
        public async Task ManualChangeDuringSession_IsNotUndone()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.processes.CpuSets[ChromePid] = [4, 5];
            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Equal([4, 5], this.processes.CpuSets[ChromePid]);
        }

        [Fact]
        public async Task RuleSavedDuringSession_IsNotUndone()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.rules.Add(new PersistentProcessRule { IsEnabled = true, ProcessName = "chrome", LegacyAffinityMask = 0xFFFF0000 });
            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[ChromePid]);
        }

        [Fact]
        public async Task TurnedOff_RestoresImmediately()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.settings.GameBoostEnabled = false;
            await service.ReapplyAsync();

            Assert.Empty(this.processes.CpuSets[GamePid]);
            Assert.Empty(this.processes.CpuSets[ChromePid]);
            Assert.Null(service.ActiveGameName);
        }

        [Fact]
        public async Task ModeChange_RestoresThenReappliesToTheSameGame()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            this.settings.GameBoostMode = GameBoostMode.Isolation;
            this.processes.Add(400, "Spotify", @"C:\Users\u\AppData\Roaming\Spotify\Spotify.exe");
            await service.ReapplyAsync();

            Assert.Equal("Game", service.ActiveGameName);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[400]);
        }

        [Fact]
        public async Task CrashRecovery_RestoresSameProcess_SkipsReusedPid()
        {
            using (var crashed = this.CreateService())
            {
                await crashed.TickAsync();
            }

            this.processes.Restart(GamePid); // same PID, new process instance
            using var service = this.CreateService();
            await service.StartAsync();

            Assert.Empty(this.processes.CpuSets[ChromePid]);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]); // not ours anymore, left alone
            Assert.False(File.Exists(this.journalPath));
            await service.StopAsync();
        }

        [Fact]
        public async Task FailedApply_StaysInJournal_AndIsUndoneIfItHalfSucceeded()
        {
            this.processes.FailButApply.Add(ChromePid);
            using var service = this.CreateService();
            await service.TickAsync();

            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Empty(this.processes.CpuSets[ChromePid]);
        }

        [Fact]
        public async Task AboveNormal_AppliedToGame_RestoredOnlyIfStillOurs()
        {
            this.settings.GameBoostAboveNormalPriority = true;
            using var service = this.CreateService();
            await service.TickAsync();
            Assert.Equal(ProcessPriorityClass.AboveNormal, this.processes.Priorities[GamePid]);

            this.processes.Exit(ChromePid);
            await service.ReapplyAsync(); // settings change: restore, then apply again
            Assert.Equal(ProcessPriorityClass.AboveNormal, this.processes.Priorities[GamePid]);

            this.processes.Priorities[GamePid] = ProcessPriorityClass.High; // user change
            this.settings.GameBoostEnabled = false;
            await service.ReapplyAsync();
            Assert.Equal(ProcessPriorityClass.High, this.processes.Priorities[GamePid]);
        }

        [Fact]
        public async Task AboveNormalOff_PriorityUntouched()
        {
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.Equal(ProcessPriorityClass.Normal, this.processes.Priorities[GamePid]);
        }

        [Fact]
        public async Task UnsupportedCpu_NoCpuSets()
        {
            using var service = this.CreateService(TopologyFixtures.Amd([8], [96 * MB]));

            await service.TickAsync();

            Assert.Equal("Game", service.ActiveGameName);
            Assert.Empty(this.processes.ApplyCalls);
        }

        [Fact]
        public async Task SecondGame_IgnoredWhileASessionRuns()
        {
            this.processes.Add(500, "Other", @"D:\SteamLibrary\steamapps\common\Other\Other.exe");
            using var service = this.CreateService();
            await service.TickAsync();

            this.foregroundPid = 500;
            await service.TickAsync();

            Assert.Equal("Game", service.ActiveGameName);
            Assert.Empty(this.processes.CpuSets[500]);
        }

        [Fact]
        public async Task Activity_ShowsSessionChangesAndRestore_WithoutRepeatingOnRescans()
        {
            this.processes.Add(201, "chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
            this.rules.Add(new PersistentProcessRule { IsEnabled = true, ProcessName = "steam", LegacyAffinityMask = 0xF });
            this.processes.Add(202, "steam", @"C:\Program Files (x86)\Steam\steam.exe");
            using var service = this.CreateService();

            await service.TickAsync();
            for (var i = 0; i < 10; i++)
            {
                await service.TickAsync(); // background rescans with nothing new
            }

            this.processes.Exit(GamePid);
            await service.TickAsync();

            var entries = (await this.audit.GetEntriesAsync()).Reverse().ToList();
            Assert.All(entries, entry => Assert.Equal("Game Boost", entry.Category));
            Assert.Equal(
                [
                    "Session started: Game (Automatic)",
                    "Game: CPU Sets set to 16 CPUs",
                    "Background moved to 16 CPUs: chrome x2",
                    "Background left unchanged",
                    "Session ended for Game: the game exited",
                ],
                entries.Select(entry => entry.Message));
            Assert.Contains("AMD 3D V-Cache", entries[0].Details, StringComparison.Ordinal);
            Assert.Equal("steam (your rule)", entries[3].Details);
            Assert.Equal("Restored 2 change(s); 1 left as they are (changed by you or a rule since, or the process exited)", entries[4].Details);
        }

        [Fact]
        public async Task Activity_CrashRecoveryAndSkippedPriority()
        {
            this.processes.Priorities[GamePid] = ProcessPriorityClass.BelowNormal;
            this.settings.GameBoostAboveNormalPriority = true;
            using (var crashed = this.CreateService())
            {
                await crashed.TickAsync();
            }

            using var service = this.CreateService();
            await service.StartAsync();
            await service.StopAsync();

            var messages = (await this.audit.GetEntriesAsync()).Select(entry => entry.Message).ToList();
            Assert.Contains("Game: priority left at BelowNormal", messages);
            Assert.Contains("Restored 2 change(s) left by an unexpected close", messages);
        }

        private GameSessionService CreateService(CpuTopologySnapshot? topology = null)
        {
            var snapshot = topology ?? TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB]);
            var topologyProvider = new Mock<ICpuTopologyProvider>();
            topologyProvider.Setup(p => p.GetTopologySnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
            var cpuTopologyService = new Mock<ICpuTopologyService>();
            cpuTopologyService.Setup(s => s.DetectTopologyAsync()).ReturnsAsync(new CpuTopologyModel { CpuBrand = "AMD Ryzen 9 7950X3D" });
            var foreground = new Mock<IForegroundProcessService>();
            foreground.Setup(f => f.TryGetForegroundProcessId()).Returns(() => this.foregroundPid);
            var ruleStore = new Mock<IPersistentProcessRuleStore>();
            ruleStore.Setup(s => s.LoadAsync()).ReturnsAsync(() => this.rules.ToList());

            return new GameSessionService(
                this.settingsService.Object,
                topologyProvider.Object,
                cpuTopologyService.Object,
                foreground.Object,
                this.processes.ProcessService,
                this.processes.AffinityService,
                ruleStore.Object,
                new PersistentProcessRuleMatcher(),
                new ProcessFilterService(),
                new ProcessPolicyApplyGate(),
                this.processes,
                NullLogger<GameSessionService>.Instance,
                this.journalPath,
                this.audit);
        }

        /// <summary>In-memory processes: CPU Sets, priorities and identities the service reads and writes.</summary>
        private sealed class FakeProcesses : IGameBoostProcessProbe
        {
            private readonly Dictionary<int, ProcessModel> models = new();
            private readonly Dictionary<int, long> startTimes = new();

            public FakeProcesses()
            {
                var processService = new Mock<IProcessService>();
                processService.Setup(s => s.GetProcessByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int pid) => this.startTimes.ContainsKey(pid) ? this.Snapshot(pid) : null);
                processService.Setup(s => s.GetProcessesWithPathsAsync())
                    .ReturnsAsync(() => this.startTimes.Keys.Select(this.Snapshot).ToList());
                processService.Setup(s => s.GetCpuAssignmentLogicalProcessorIndexesAsync(It.IsAny<ProcessModel>(), CpuAssignmentMode.CpuSets))
                    .ReturnsAsync((ProcessModel p, CpuAssignmentMode _) => this.Unreadable.Contains(p.ProcessId) ? null : this.CpuSets[p.ProcessId].ToList());
                processService.Setup(s => s.ClearProcessCpuSetAsync(It.IsAny<ProcessModel>()))
                    .ReturnsAsync((ProcessModel p) =>
                    {
                        this.CpuSets[p.ProcessId] = [];
                        return true;
                    });
                processService.Setup(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()))
                    .Callback((ProcessModel p, ProcessPriorityClass priority, ProcessPriorityWriteSource _) => this.Priorities[p.ProcessId] = priority)
                    .Returns(Task.CompletedTask);
                this.ProcessService = processService.Object;

                var affinityService = new Mock<IAffinityApplyService>();
                affinityService.Setup(s => s.ApplyAsync(It.IsAny<ProcessModel>(), It.IsAny<CpuSelection>(), CpuAssignmentMode.CpuSets))
                    .ReturnsAsync((ProcessModel p, CpuSelection selection, CpuAssignmentMode _) =>
                    {
                        this.ApplyCalls.Add(p.ProcessId);
                        this.CpuSets[p.ProcessId] = selection.GlobalLogicalProcessorIndexes.ToList();
                        return this.FailButApply.Contains(p.ProcessId)
                            ? AffinityApplyResult.Failed("NativeApplyFailed", "failed", "verification mismatch")
                            : AffinityApplyResult.SucceededWithCpuSets("ok");
                    });
                this.AffinityService = affinityService.Object;
            }

            public IProcessService ProcessService { get; }

            public IAffinityApplyService AffinityService { get; }

            public Dictionary<int, List<int>> CpuSets { get; } = new();

            public Dictionary<int, ProcessPriorityClass> Priorities { get; } = new();

            public HashSet<int> Unreadable { get; } = new();

            public HashSet<int> FailButApply { get; } = new();

            public List<int> ApplyCalls { get; } = new();

            public void Add(int pid, string name, string path)
            {
                this.models[pid] = new ProcessModel { ProcessId = pid, Name = name, ExecutablePath = path };
                this.startTimes[pid] = 1000 + pid;
                this.CpuSets[pid] = [];
                this.Priorities[pid] = ProcessPriorityClass.Normal;
            }

            public void Exit(int pid) => this.startTimes.Remove(pid);

            public void Restart(int pid) => this.startTimes[pid] += 1;

            public bool TryGetIdentity(int processId, out long startTimeUtcTicks, out int sessionId)
            {
                sessionId = 1;
                return this.startTimes.TryGetValue(processId, out startTimeUtcTicks);
            }

            private ProcessModel Snapshot(int pid)
            {
                var model = this.models[pid];
                return new ProcessModel
                {
                    ProcessId = pid,
                    Name = model.Name,
                    ExecutablePath = model.ExecutablePath,
                    Priority = this.Priorities[pid],
                };
            }
        }
    }
}
