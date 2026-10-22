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
        private readonly FakeClock clock = new();
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
        public async Task NonGameForeground_IsNotReexaminedEveryTick_ButIsAfterAWhile()
        {
            this.foregroundPid = ChromePid;
            using var service = this.CreateService();
            await service.TickAsync();

            // Changed behind the service's back (no settings event), so only a fresh look can notice it.
            this.settings.GameBoostManualGames.Add("chrome");
            this.clock.Advance(TimeSpan.FromSeconds(3));
            await service.TickAsync();
            Assert.Null(service.ActiveGameName);

            this.clock.Advance(TimeSpan.FromSeconds(30));
            await service.TickAsync();
            Assert.Equal("chrome", service.ActiveGameName);
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
        public async Task HeldGame_ExitEvent_EndsTheSessionAndRestores_WithoutATick()
        {
            this.processes.HandleAllowed = true;
            using var service = this.CreateService();
            await service.TickAsync();
            Assert.Equal(1, this.processes.OpenHandles);

            await this.processes.ExitWithEventAsync(GamePid);

            Assert.Null(service.ActiveGameName);
            Assert.Empty(this.processes.CpuSets[ChromePid]);
            Assert.False(File.Exists(this.journalPath));
            Assert.Equal(0, this.processes.OpenHandles);
            var messages = (await this.audit.GetEntriesAsync()).Select(entry => entry.Message).ToList();
            Assert.Contains("Session ended for Game: the game exited", messages);
        }

        [Fact]
        public async Task HeldGame_TicksDoNotLookItUp_AndItsLoadIsReadFromTheHandle()
        {
            this.processes.HandleAllowed = true;
            using var service = this.CreateService();
            await service.TickAsync();
            this.processes.Lookups.Clear();

            await this.RunLoadAsync(service, cpusBusy: 8, ticks: GameLoadTracker.ReleaseSamples);
            Assert.False(this.processes.Lookups.ContainsKey(GamePid));

            await this.RunLoadAsync(service, cpusBusy: 16, ticks: GameLoadTracker.ReleaseSamples + 1);
            Assert.Empty(this.processes.CpuSets[GamePid]);
        }

        [Fact]
        public async Task HeldGame_SettingsChange_KeepsWatchingTheSameGame()
        {
            this.processes.HandleAllowed = true;
            using var service = this.CreateService();
            await service.TickAsync();

            this.settings.GameBoostMode = GameBoostMode.Isolation;
            await service.ReapplyAsync();
            Assert.Equal(1, this.processes.OpenHandles);

            await this.processes.ExitWithEventAsync(GamePid);
            Assert.Null(service.ActiveGameName);
            Assert.False(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task HeldGame_HandleReleased_WhenDisposedOrStopped()
        {
            this.processes.HandleAllowed = true;
            using (var crashed = this.CreateService())
            {
                await crashed.TickAsync();
            }

            Assert.Equal(0, this.processes.OpenHandles);

            using var service = this.CreateService();
            await service.StartAsync();
            await service.TickAsync();
            Assert.Equal(1, this.processes.OpenHandles);
            await service.StopAsync();
            Assert.Equal(0, this.processes.OpenHandles);
            Assert.False(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task HeldGame_LateExitEventOfAnEndedSession_DoesNotEndTheNextOne()
        {
            this.processes.HandleAllowed = true;
            this.processes.Add(500, "Other", @"D:\SteamLibrary\steamapps\common\Other\Other.exe");
            using var service = this.CreateService();
            await service.TickAsync();
            var lateExit = this.processes.ExitCallbacks.Single();

            this.settings.GameBoostEnabled = false;
            await service.ReapplyAsync();
            this.settings.GameBoostEnabled = true;
            this.foregroundPid = 500;
            await service.TickAsync();
            await lateExit();

            Assert.Equal("Other", service.ActiveGameName);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[500]);
        }

        [Fact]
        public async Task HandleDenied_PidReusedDuringTheSession_EndsIt_WithoutTouchingTheNewProcess()
        {
            using var service = this.CreateService(); // the handle is denied, as by an anti-cheat: polled every tick
            await service.TickAsync();
            Assert.Equal(0, this.processes.OpenHandles);

            this.processes.Restart(GamePid);
            await service.TickAsync();

            Assert.Null(service.ActiveGameName);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]); // the new process is not ours
            Assert.Empty(this.processes.CpuSets[ChromePid]);
            Assert.False(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task Rescan_BuildsModelsOnlyForListedNames_ThenChecksTheExactPath()
        {
            this.settings.GameBoostBackgroundProcesses = ["chrome", @"C:\Tools\Sync\sync.exe"];
            this.processes.Add(600, "sync", @"C:\Tools\Sync\sync.exe");
            this.processes.Add(601, "sync", @"D:\Other\sync.exe"); // same name, another executable
            this.processes.Add(602, "notepad", @"C:\Windows\notepad.exe");
            using var service = this.CreateService();

            await service.TickAsync();

            Assert.Equal(["chrome", "sync", "sync"], this.processes.ScannedModels.Order());
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[ChromePid]);
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[600]);
            Assert.Empty(this.processes.CpuSets[601]);
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
                    "Game: high QoS, Windows will not power-throttle it",
                    "Background moved to 16 CPUs: chrome x2",
                    "Background left unchanged",
                    "Session ended for Game: the game exited",
                ],
                entries.Select(entry => entry.Message));
            Assert.Contains("AMD 3D V-Cache", entries[0].Details, StringComparison.Ordinal);
            Assert.Equal("steam (your rule)", entries[4].Details);
            Assert.Equal("Restored 2 change(s); 2 left as they are (changed by you or a rule since, or the process exited)", entries[5].Details);
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
            Assert.Contains("Restored 3 change(s) left by an unexpected close", messages);
        }

        [Fact]
        public async Task Game_GetsHighQos_RestoredWhenTheSessionEnds()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            Assert.Equal(ProcessEfficiencyMode.Off, this.processes.Efficiency[GamePid]);
            Assert.Equal(ProcessEfficiencyMode.SystemManaged, this.processes.Efficiency[ChromePid]); // Automatic: no EcoQoS

            this.settings.GameBoostEnabled = false;
            await service.TickAsync();

            Assert.Equal(ProcessEfficiencyMode.SystemManaged, this.processes.Efficiency[GamePid]);
        }

        [Fact]
        public async Task Game_HighQosEvenWhenTheCpuIsNotSupported()
        {
            using var service = this.CreateService(TopologyFixtures.Amd([8], [96 * MB]));

            await service.TickAsync();

            Assert.Empty(this.processes.CpuSets[GamePid]);
            Assert.Equal(ProcessEfficiencyMode.Off, this.processes.Efficiency[GamePid]);
        }

        [Fact]
        public async Task ExplicitQos_FromTheAppOrARule_NotTouched()
        {
            this.processes.Efficiency[GamePid] = ProcessEfficiencyMode.On;
            this.settings.GameBoostMode = GameBoostMode.Isolation;
            this.rules.Add(new PersistentProcessRule { IsEnabled = true, ProcessName = "chrome", EfficiencyMode = ProcessEfficiencyMode.Off });
            this.processes.Efficiency[ChromePid] = ProcessEfficiencyMode.Off;
            using var service = this.CreateService();

            await service.TickAsync();
            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Equal(ProcessEfficiencyMode.Off, this.processes.Efficiency[ChromePid]);
            Assert.Equal(ProcessEfficiencyMode.On, this.processes.Efficiency[GamePid]);
        }

        [Fact]
        public async Task Isolation_ListedBackgroundGetsEcoQos_EvenWithoutCpuSets_AndIsRestored()
        {
            this.settings.GameBoostMode = GameBoostMode.Isolation;
            using var service = this.CreateService(TopologyFixtures.Amd([8], [96 * MB]));

            await service.TickAsync();

            Assert.Equal(ProcessEfficiencyMode.On, this.processes.Efficiency[ChromePid]);
            Assert.Equal(ProcessEfficiencyMode.SystemManaged, this.processes.Efficiency[DiscordPid]);
            Assert.Empty(this.processes.CpuSets[ChromePid]);

            this.processes.Efficiency[ChromePid] = ProcessEfficiencyMode.Off; // the app changed it itself
            this.processes.Exit(GamePid);
            await service.TickAsync();

            Assert.Equal(ProcessEfficiencyMode.Off, this.processes.Efficiency[ChromePid]);
            Assert.False(File.Exists(this.journalPath));
        }

        [Fact]
        public async Task SaturatedGame_CpuSetsReleased_ThenConfinedAgainWhenLoadDrops()
        {
            using var service = this.CreateService();
            await service.TickAsync();
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);

            await this.RunLoadAsync(service, cpusBusy: 15, ticks: GameLoadTracker.ReleaseSamples); // 15 of 16 CPUs
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]); // the first tick only sets the baseline

            await this.RunLoadAsync(service, cpusBusy: 15, ticks: 1);
            Assert.Empty(this.processes.CpuSets[GamePid]);
            Assert.Equal(OtherCcdCpus, this.processes.CpuSets[ChromePid]); // background stays moved

            await this.RunLoadAsync(service, cpusBusy: 4, ticks: GameLoadTracker.ConfineSamples);
            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);

            this.processes.Exit(GamePid);
            await service.TickAsync();
            Assert.False(File.Exists(this.journalPath));
            var messages = (await this.audit.GetEntriesAsync()).Select(entry => entry.Message).ToList();
            Assert.Contains("Game: kept its 16 CPUs busy, CPU Sets released so Windows can use every core", messages);
            Assert.Contains("Game: load is low again, CPU Sets set back to 16 CPUs", messages);
        }

        [Fact]
        public async Task SaturatedGame_AfterTheSecondRelease_StaysOnAllCores()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            await this.RunLoadAsync(service, cpusBusy: 16, ticks: GameLoadTracker.ReleaseSamples + 1);
            await this.RunLoadAsync(service, cpusBusy: 2, ticks: GameLoadTracker.ConfineSamples);
            await this.RunLoadAsync(service, cpusBusy: 16, ticks: GameLoadTracker.ReleaseSamples);
            Assert.Empty(this.processes.CpuSets[GamePid]);

            await this.RunLoadAsync(service, cpusBusy: 1, ticks: GameLoadTracker.ConfineSamples * 2);
            Assert.Empty(this.processes.CpuSets[GamePid]);
        }

        [Fact]
        public async Task SaturatedGame_UserChangedItsCores_NotReleasedNorConfinedAgain()
        {
            using var service = this.CreateService();
            await service.TickAsync();
            this.processes.CpuSets[GamePid] = [0, 1, 2, 3];

            await this.RunLoadAsync(service, cpusBusy: 16, ticks: GameLoadTracker.ReleaseSamples + 1);
            await this.RunLoadAsync(service, cpusBusy: 1, ticks: GameLoadTracker.ConfineSamples + 1);

            Assert.Equal([0, 1, 2, 3], this.processes.CpuSets[GamePid]);
        }

        [Fact]
        public async Task ShortSpikes_DoNotReleaseTheGame()
        {
            using var service = this.CreateService();
            await service.TickAsync();

            for (var i = 0; i < 10; i++)
            {
                await this.RunLoadAsync(service, cpusBusy: 16, ticks: GameLoadTracker.ReleaseSamples - 1);
                await this.RunLoadAsync(service, cpusBusy: 3, ticks: 1);
            }

            Assert.Equal(VCacheCpus, this.processes.CpuSets[GamePid]);
        }

        // One tick every 3 s; over each one the game uses as much CPU time as cpusBusy fully busy CPUs.
        private async Task RunLoadAsync(GameSessionService service, double cpusBusy, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                var interval = TimeSpan.FromSeconds(3);
                this.clock.Advance(interval);
                this.processes.CpuTime[GamePid] += interval * cpusBusy;
                await service.TickAsync();
            }
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
                this.processes.EfficiencyService,
                NullLogger<GameSessionService>.Instance,
                this.journalPath,
                this.audit,
                this.clock);
        }

        /// <summary>Timestamps in TimeSpan ticks, moved by hand.</summary>
        private sealed class FakeClock : TimeProvider
        {
            public long Now { get; private set; }

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp() => this.Now;

            public void Advance(TimeSpan by) => this.Now += by.Ticks;
        }

        /// <summary>In-memory processes: CPU Sets, priorities and identities the service reads and writes.</summary>
        private sealed class FakeProcesses : IGameBoostProcessProbe
        {
            private readonly Dictionary<int, ProcessModel> models = new();
            private readonly Dictionary<int, long> startTimes = new();
            private readonly Dictionary<int, FakeHandle> handles = new();

            public FakeProcesses()
            {
                var processService = new Mock<IProcessService>();
                processService.Setup(s => s.GetProcessByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int pid) => this.startTimes.ContainsKey(pid) ? this.Snapshot(pid) : null);
                processService.Setup(s => s.GetProcessesWithPathsAsync(It.IsAny<Func<string, bool>>()))
                    .ReturnsAsync((Func<string, bool> nameFilter) =>
                    {
                        var listed = this.startTimes.Keys.Where(pid => nameFilter(this.models[pid].Name)).ToList();
                        this.ScannedModels.AddRange(listed.Select(pid => this.models[pid].Name));
                        return listed.Select(this.Snapshot).ToList();
                    });
                processService.Setup(s => s.GetCpuAssignmentLogicalProcessorIndexesAsync(It.IsAny<ProcessModel>(), CpuAssignmentMode.CpuSets))
                    .ReturnsAsync((ProcessModel p, CpuAssignmentMode _) => this.Unreadable.Contains(p.ProcessId) ? null : this.CpuSets[p.ProcessId].ToList());
                processService.Setup(s => s.ClearProcessCpuSetAsync(It.IsAny<ProcessModel>()))
                    .ReturnsAsync((ProcessModel p) =>
                    {
                        this.CpuSets[p.ProcessId] = [];
                        return true;
                    });
                processService.Setup(s => s.SetProcessPriority(It.IsAny<ProcessModel>(), It.IsAny<ProcessPriorityClass>(), It.IsAny<ProcessPriorityWriteSource>()))
                    .Callback((ProcessModel p, ProcessPriorityClass priority, ProcessPriorityWriteSource _) =>
                    {
                        // Like ProcessService: the write also updates the model it was given.
                        this.Priorities[p.ProcessId] = priority;
                        p.Priority = priority;
                    })
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

                var efficiencyService = new Mock<IProcessMemoryPriorityService>();
                efficiencyService.Setup(s => s.GetEfficiencyModeAsync(It.IsAny<ProcessModel>()))
                    .ReturnsAsync((ProcessModel p) => this.Unreadable.Contains(p.ProcessId) ? null : this.Efficiency[p.ProcessId]);
                efficiencyService.Setup(s => s.SetEfficiencyModeAsync(It.IsAny<ProcessModel>(), It.IsAny<ProcessEfficiencyMode>()))
                    .ReturnsAsync((ProcessModel p, ProcessEfficiencyMode mode) =>
                    {
                        this.Efficiency[p.ProcessId] = mode;
                        return ProcessOperationResult.Succeeded("ok", "ok");
                    });
                this.EfficiencyService = efficiencyService.Object;
            }

            public IProcessMemoryPriorityService EfficiencyService { get; }

            public Dictionary<int, ProcessEfficiencyMode> Efficiency { get; } = new();

            public Dictionary<int, TimeSpan> CpuTime { get; } = new();

            public IProcessService ProcessService { get; }

            public IAffinityApplyService AffinityService { get; }

            public Dictionary<int, List<int>> CpuSets { get; } = new();

            public Dictionary<int, ProcessPriorityClass> Priorities { get; } = new();

            public HashSet<int> Unreadable { get; } = new();

            public HashSet<int> FailButApply { get; } = new();

            public List<int> ApplyCalls { get; } = new();

            /// <summary>Gets the names the background rescans built a full model for.</summary>
            public List<string> ScannedModels { get; } = new();

            public void Add(int pid, string name, string path)
            {
                this.models[pid] = new ProcessModel { ProcessId = pid, Name = name, ExecutablePath = path };
                this.startTimes[pid] = 1000 + pid;
                this.CpuSets[pid] = [];
                this.Priorities[pid] = ProcessPriorityClass.Normal;
                this.Efficiency[pid] = ProcessEfficiencyMode.SystemManaged;
                this.CpuTime[pid] = TimeSpan.Zero;
            }

            /// <summary>Gets or sets a value indicating whether the game can be held open; false acts like an anti-cheat that denies the handle.</summary>
            public bool HandleAllowed { get; set; }

            public int OpenHandles => this.handles.Count;

            /// <summary>Gets every exit callback handed out, including those of handles already released.</summary>
            public List<Func<Task>> ExitCallbacks { get; } = new();

            public Dictionary<int, int> Lookups { get; } = new();

            public void Exit(int pid) => this.startTimes.Remove(pid);

            /// <summary>The game exits and its held handle raises the exit event; completes when the service has handled it.</summary>
            public Task ExitWithEventAsync(int pid)
            {
                this.Exit(pid);
                return this.handles.TryGetValue(pid, out var handle) ? handle.OnExited() : Task.CompletedTask;
            }

            public void Restart(int pid) => this.startTimes[pid] += 1;

            public bool TryGetIdentity(int processId, out long startTimeUtcTicks, out int sessionId)
            {
                this.Lookups[processId] = this.Lookups.GetValueOrDefault(processId) + 1;
                sessionId = 1;
                return this.startTimes.TryGetValue(processId, out startTimeUtcTicks);
            }

            public bool TryGetCpuTime(int processId, out TimeSpan totalProcessorTime)
            {
                this.Lookups[processId] = this.Lookups.GetValueOrDefault(processId) + 1;
                return this.CpuTime.TryGetValue(processId, out totalProcessorTime) && this.startTimes.ContainsKey(processId);
            }

            public IGameProcessHandle? TryOpen(int processId, long startTimeUtcTicks, Func<Task> onExited)
            {
                if (!this.HandleAllowed || !this.startTimes.TryGetValue(processId, out var current) || current != startTimeUtcTicks)
                {
                    return null;
                }

                var handle = new FakeHandle(this, processId, onExited);
                this.ExitCallbacks.Add(onExited);
                this.handles[processId] = handle;
                return handle;
            }

            private sealed class FakeHandle(FakeProcesses owner, int pid, Func<Task> onExited) : IGameProcessHandle
            {
                public Func<Task> OnExited { get; } = onExited;

                public bool TryGetCpuTime(out TimeSpan totalProcessorTime) => owner.CpuTime.TryGetValue(pid, out totalProcessorTime);

                public void Dispose()
                {
                    if (owner.handles.TryGetValue(pid, out var held) && held == this)
                    {
                        owner.handles.Remove(pid);
                    }
                }
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
