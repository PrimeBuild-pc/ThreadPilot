namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using ThreadPilot.Models;

    public enum GameBoostSetting
    {
        CpuSets = 0,
        Priority = 1,
        EfficiencyMode = 2,
    }

    /// <summary>
    /// One change Game Boost made (or is about to make). Written before the native call and confirmed
    /// after it, so a crash in between still leaves enough to undo the change.
    /// </summary>
    public sealed record GameBoostJournalEntry
    {
        public int ProcessId { get; init; }

        public long StartTimeUtcTicks { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public GameBoostSetting Setting { get; init; }

        public List<int> AppliedCpuIndexes { get; init; } = new();

        public ProcessPriorityClass? OriginalPriority { get; init; }

        public ProcessPriorityClass? AppliedPriority { get; init; }

        public ProcessEfficiencyMode? AppliedEfficiencyMode { get; init; }

        public bool Confirmed { get; init; }
    }

    public interface IGameBoostProcessProbe
    {
        /// <summary>Start time and Windows session of a running process; false when it is gone or unreadable.</summary>
        bool TryGetIdentity(int processId, out long startTimeUtcTicks, out int sessionId);

        /// <summary>Total CPU time a running process has used; false when it is gone or unreadable.</summary>
        bool TryGetCpuTime(int processId, out TimeSpan totalProcessorTime);
    }

    public sealed class GameBoostProcessProbe : IGameBoostProcessProbe
    {
        public bool TryGetIdentity(int processId, out long startTimeUtcTicks, out int sessionId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                sessionId = process.SessionId;
                return !process.HasExited;
            }
            catch
            {
                startTimeUtcTicks = 0;
                sessionId = 0;
                return false;
            }
        }

        public bool TryGetCpuTime(int processId, out TimeSpan totalProcessorTime)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                totalProcessorTime = process.TotalProcessorTime;
                return true;
            }
            catch
            {
                totalProcessorTime = TimeSpan.Zero;
                return false;
            }
        }
    }

    public interface IGameSessionService : IDisposable
    {
        /// <summary>Gets the game of the running session, or null.</summary>
        string? ActiveGameName { get; }

        Task<GameBoostDecision> GetDecisionAsync();

        /// <summary>Undoes what a previous run left behind (crash), then starts watching for games.</summary>
        Task StartAsync();

        /// <summary>Stops watching and restores everything Game Boost changed.</summary>
        Task StopAsync();
    }

    /// <summary>
    /// Applies the Game Boost policy for one game session at a time. Constraints: the game's and the listed
    /// background processes' default CPU Sets, released while the game keeps its own cores saturated. Hints:
    /// high QoS for the game, EcoQoS for the listed background in Isolation, optionally Above Normal priority.
    /// Each change is journaled and restored when the game exits, Game Boost is turned off or ThreadPilot
    /// closes. User rules and selections made by anyone else always win.
    /// </summary>
    public sealed class GameSessionService : IGameSessionService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RejectedForegroundMemory = TimeSpan.FromSeconds(30);

        // Background is rescanned every few ticks (about 15 s), not on every one.
        private const int BackgroundRescanTicks = 5;

        private static readonly JsonSerializerOptions JournalJsonOptions = new() { WriteIndented = true };

        private readonly IApplicationSettingsService settingsService;
        private readonly ICpuTopologyProvider topologyProvider;
        private readonly ICpuTopologyService cpuTopologyService;
        private readonly IForegroundProcessService foregroundProcessService;
        private readonly IProcessService processService;
        private readonly IAffinityApplyService affinityApplyService;
        private readonly IPersistentProcessRuleStore ruleStore;
        private readonly IPersistentProcessRuleMatcher ruleMatcher;
        private readonly ProcessFilterService processFilterService;
        private readonly ProcessPolicyApplyGate applyGate;
        private readonly IGameBoostProcessProbe processProbe;
        private readonly IProcessMemoryPriorityService efficiencyService;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<GameSessionService> logger;
        private readonly IActivityAuditService? activityAuditService;
        private readonly string journalPath;
        private readonly SemaphoreSlim stateLock = new(1, 1);
        private readonly HashSet<(int ProcessId, long StartTimeUtcTicks, GameBoostSetting Setting)> attempted = new();
        private readonly object timerGate = new();

        private List<GameBoostJournalEntry> journal = new();
        private Timer? timer;
        private bool started;
        private (int ProcessId, long Timestamp)? rejectedForeground;
        private GameSession? session;
        private GameBoostDecision? cachedDecision;
        private GameBoostMode cachedDecisionMode;
        private int tickCount;
        private GameLoadTracker? loadTracker;
        private (TimeSpan CpuTime, long Timestamp)? lastLoadSample;
        private bool disposed;

        public GameSessionService(
            IApplicationSettingsService settingsService,
            ICpuTopologyProvider topologyProvider,
            ICpuTopologyService cpuTopologyService,
            IForegroundProcessService foregroundProcessService,
            IProcessService processService,
            IAffinityApplyService affinityApplyService,
            IPersistentProcessRuleStore ruleStore,
            IPersistentProcessRuleMatcher ruleMatcher,
            ProcessFilterService processFilterService,
            ProcessPolicyApplyGate applyGate,
            IGameBoostProcessProbe processProbe,
            IProcessMemoryPriorityService efficiencyService,
            ILogger<GameSessionService> logger,
            string? journalPath = null,
            IActivityAuditService? activityAuditService = null,
            TimeProvider? timeProvider = null)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.topologyProvider = topologyProvider ?? throw new ArgumentNullException(nameof(topologyProvider));
            this.cpuTopologyService = cpuTopologyService ?? throw new ArgumentNullException(nameof(cpuTopologyService));
            this.foregroundProcessService = foregroundProcessService ?? throw new ArgumentNullException(nameof(foregroundProcessService));
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.affinityApplyService = affinityApplyService ?? throw new ArgumentNullException(nameof(affinityApplyService));
            this.ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
            this.ruleMatcher = ruleMatcher ?? throw new ArgumentNullException(nameof(ruleMatcher));
            this.processFilterService = processFilterService ?? throw new ArgumentNullException(nameof(processFilterService));
            this.applyGate = applyGate ?? throw new ArgumentNullException(nameof(applyGate));
            this.processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
            this.efficiencyService = efficiencyService ?? throw new ArgumentNullException(nameof(efficiencyService));
            this.timeProvider = timeProvider ?? TimeProvider.System;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.journalPath = journalPath ?? Path.Combine(StoragePaths.AppDataRoot, "game_boost_journal.json");
            this.activityAuditService = activityAuditService;

            this.settingsService.SettingsChanged += this.OnSettingsChanged;
        }

        public string? ActiveGameName => this.session?.Game.Name;

        private ApplicationSettingsModel Settings => this.settingsService.Settings;

        public async Task<GameBoostDecision> GetDecisionAsync()
        {
            var mode = this.Settings.GameBoostMode;
            if (this.cachedDecision != null && this.cachedDecisionMode == mode)
            {
                return this.cachedDecision;
            }

            try
            {
                var snapshot = await this.topologyProvider.GetTopologySnapshotAsync().ConfigureAwait(false);
                var brand = (await this.cpuTopologyService.DetectTopologyAsync().ConfigureAwait(false)).CpuBrand;
                this.cachedDecision = GameBoostPolicy.Decide(snapshot, brand, mode);
                this.cachedDecisionMode = mode;
                this.logger.LogInformation(
                    "Game Boost policy for {CpuBrand} in {Mode}: {Kind}, game CPUs {GameCpus}, background CPUs {BackgroundCpus}",
                    brand,
                    mode,
                    this.cachedDecision.Kind,
                    this.cachedDecision.GameSelection?.GlobalLogicalProcessorIndexes.Count ?? 0,
                    this.cachedDecision.BackgroundSelection?.GlobalLogicalProcessorIndexes.Count ?? 0);
                LogAmdVCacheDriver(this.logger);
                return this.cachedDecision;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Game Boost could not read the CPU topology; it will not change CPU Sets");
                return GameBoostDecision.None;
            }
        }

        public async Task StartAsync()
        {
            await this.stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                this.journal = await this.LoadJournalAsync().ConfigureAwait(false);
                if (this.journal.Count > 0)
                {
                    this.logger.LogInformation("Game Boost: restoring {Count} change(s) left by a previous run", this.journal.Count);
                    var (restored, left, failed) = await this.RestoreAllAsync().ConfigureAwait(false);
                    await this.AuditAsync(
                        failed.Count > 0 ? ActivityAuditSeverity.Warning : ActivityAuditSeverity.Success,
                        $"Restored {restored} change(s) left by an unexpected close",
                        DescribeRestore(restored, left, failed)).ConfigureAwait(false);
                }

                this.started = true;
                this.SyncTimer();
            }
            finally
            {
                this.stateLock.Release();
            }
        }

        public async Task StopAsync()
        {
            lock (this.timerGate)
            {
                this.started = false;
                this.timer?.Dispose();
                this.timer = null;
            }

            await this.stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await this.EndSessionAsync("ThreadPilot is closing").ConfigureAwait(false);
            }
            finally
            {
                this.stateLock.Release();
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.settingsService.SettingsChanged -= this.OnSettingsChanged;
            lock (this.timerGate)
            {
                this.timer?.Dispose();
                this.timer = null;
            }
        }

        // Ticks only while Game Boost is on (it is off by default); turning it off restores through ReapplyAsync.
        private void SyncTimer()
        {
            lock (this.timerGate)
            {
                if (this.started && !this.disposed && this.Settings.GameBoostEnabled)
                {
                    this.timer ??= new Timer(_ => TaskSafety.FireAndForget(this.TickAsync(), ex => this.logger.LogWarning(ex, "Game Boost tick failed")), null, TickInterval, TickInterval);
                    return;
                }

                this.timer?.Dispose();
                this.timer = null;
            }
        }

        internal async Task TickAsync()
        {
            if (!await this.stateLock.WaitAsync(0).ConfigureAwait(false))
            {
                return; // the previous tick is still running
            }

            try
            {
                if (!this.Settings.GameBoostEnabled)
                {
                    await this.EndSessionAsync("Game Boost turned off").ConfigureAwait(false);
                    return;
                }

                if (this.session == null)
                {
                    await this.TryBeginSessionAsync().ConfigureAwait(false);
                    return;
                }

                if (!this.IsSameProcessAlive(this.session.Game.ProcessId, this.session.GameStartTimeUtcTicks))
                {
                    await this.EndSessionAsync("the game exited").ConfigureAwait(false);
                    return;
                }

                await this.AdaptGameCpuSetsAsync().ConfigureAwait(false);

                if (++this.tickCount % BackgroundRescanTicks == 0)
                {
                    await this.ApplyToSessionAsync(includeGame: false).ConfigureAwait(false);
                }
            }
            finally
            {
                this.stateLock.Release();
            }
        }

        private async Task TryBeginSessionAsync()
        {
            var foregroundId = this.foregroundProcessService.TryGetForegroundProcessId();
            if (foregroundId is not int processId || processId == Environment.ProcessId)
            {
                return;
            }

            // The browser or desktop usually stays in front for minutes: building its full process model every
            // 3 s only to reject it again costs ~8 process queries and a window enumeration per tick.
            if (this.rejectedForeground is { } rejected
                && rejected.ProcessId == processId
                && this.timeProvider.GetElapsedTime(rejected.Timestamp) < RejectedForegroundMemory)
            {
                return;
            }

            var game = await this.processService.GetProcessByIdAsync(processId).ConfigureAwait(false);
            if (game != null
                && !GameBoostProcessLists.IsGame(game.Name, game.ExecutablePath, this.Settings.GameBoostManualGames, this.Settings.GameBoostExcludedGames))
            {
                this.rejectedForeground = (processId, this.timeProvider.GetTimestamp());
                return;
            }

            if (game == null || !this.processProbe.TryGetIdentity(processId, out var startTicks, out var sessionId))
            {
                return;
            }

            this.session = new GameSession(game, startTicks, sessionId, Path.GetDirectoryName(game.ExecutablePath));
            this.ResetSessionState();
            this.logger.LogInformation("Game Boost session started for {Game} (PID {ProcessId})", game.Name, game.ProcessId);
            await this.AuditSessionStartAsync(game).ConfigureAwait(false);
            await this.ApplyToSessionAsync(includeGame: true).ConfigureAwait(false);
        }

        private async Task AuditSessionStartAsync(ProcessModel game)
        {
            var decision = await this.GetDecisionAsync().ConfigureAwait(false);
            var mode = this.Settings.GameBoostMode == GameBoostMode.Isolation ? "Isolation" : "Automatic";
            var gameCpus = decision.GameSelection?.GlobalLogicalProcessorIndexes.Count ?? 0;
            var background = decision.BackgroundSelection?.GlobalLogicalProcessorIndexes.Count ?? 0;
            var policy = decision.Kind switch
            {
                GameBoostTopologyKind.AmdAsymmetricCache => $"AMD 3D V-Cache: game on {gameCpus} CPUs, listed background on {background} CPUs",
                GameBoostTopologyKind.AmdSymmetricMultiL3 when decision.GameSelection != null => $"AMD equal CCDs: game on CCD0 ({gameCpus} CPUs), listed background on {background} CPUs",
                GameBoostTopologyKind.AmdSymmetricMultiL3 => $"AMD equal CCDs: game left to Windows, listed background on {background} CPUs",
                GameBoostTopologyKind.IntelHybrid => $"Intel hybrid: game on {gameCpus} P-core CPUs, listed background on {background} E-core CPUs",
                _ => "CPU Sets unchanged: this CPU topology is not supported, Windows decides the cores",
            };
            if (this.Settings.GameBoostMode == GameBoostMode.Isolation)
            {
                policy += "; listed background in efficiency mode (EcoQoS)";
            }

            await this.AuditAsync(ActivityAuditSeverity.Info, $"Session started: {game.Name} ({mode})", policy).ConfigureAwait(false);
        }

        private async Task ApplyToSessionAsync(bool includeGame)
        {
            var currentSession = this.session;
            if (currentSession == null)
            {
                return;
            }

            var decision = await this.GetDecisionAsync().ConfigureAwait(false);
            var rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);

            // Inside the shared gate: persistent rules and Game Boost never write to processes concurrently.
            using var gate = await this.applyGate.EnterAsync().ConfigureAwait(false);

            if (includeGame)
            {
                var game = currentSession.Game;
                var startTicks = currentSession.GameStartTimeUtcTicks;
                if (decision.GameSelection != null)
                {
                    var (outcome, reason) = await this.TryApplyCpuSetsAsync(game, startTicks, decision.GameSelection, rules).ConfigureAwait(false);
                    await this.AuditGameCpuSetsAsync(game.Name, outcome, reason, decision.GameSelection.GlobalLogicalProcessorIndexes.Count).ConfigureAwait(false);
                    if (outcome == ApplyOutcome.Applied)
                    {
                        this.loadTracker = new GameLoadTracker(decision.GameSelection.GlobalLogicalProcessorIndexes.Count);
                    }
                }

                // A hint, not a constraint: Windows never power-throttles the game, even while it is not in the foreground.
                var (qosOutcome, qosReason) = await this.TryApplyEfficiencyModeAsync(game, startTicks, ProcessEfficiencyMode.Off, rules).ConfigureAwait(false);
                await this.AuditGameQosAsync(game.Name, qosOutcome, qosReason).ConfigureAwait(false);

                if (this.Settings.GameBoostAboveNormalPriority)
                {
                    await this.TryApplyAboveNormalAsync(game, startTicks, rules).ConfigureAwait(false);
                }
            }

            var useEcoQos = this.Settings.GameBoostMode == GameBoostMode.Isolation;
            if (decision.BackgroundSelection == null && !useEcoQos)
            {
                return;
            }

            var moved = new List<string>();
            var eco = new List<string>();
            var notMoved = new List<string>();
            var processes = await this.processService.GetProcessesWithPathsAsync().ConfigureAwait(false);
            foreach (var process in processes)
            {
                if (!this.IsBackgroundCandidate(process, currentSession)
                    || !this.processProbe.TryGetIdentity(process.ProcessId, out var startTicks, out var sessionId)
                    || sessionId == 0
                    || sessionId != currentSession.GameSessionId)
                {
                    continue;
                }

                if (decision.BackgroundSelection != null)
                {
                    var (outcome, reason) = await this.TryApplyCpuSetsAsync(process, startTicks, decision.BackgroundSelection, rules).ConfigureAwait(false);
                    Collect(outcome, reason, "CPU Sets", process.Name, moved, notMoved);
                }

                if (useEcoQos)
                {
                    var (outcome, reason) = await this.TryApplyEfficiencyModeAsync(process, startTicks, ProcessEfficiencyMode.On, rules).ConfigureAwait(false);

                    // Browsers set their own QoS on new processes all the time: expected, so not logged on every rescan.
                    if (outcome != ApplyOutcome.ExistingSelection)
                    {
                        Collect(outcome, reason, "efficiency mode", process.Name, eco, notMoved);
                    }
                }
            }

            // One line per pass and only when something changed, so the 15 s rescan stays quiet.
            if (moved.Count > 0)
            {
                await this.AuditAsync(
                    ActivityAuditSeverity.Success,
                    $"Background moved to {decision.BackgroundSelection!.GlobalLogicalProcessorIndexes.Count} CPUs: {Summarize(moved)}").ConfigureAwait(false);
            }

            if (eco.Count > 0)
            {
                await this.AuditAsync(ActivityAuditSeverity.Success, $"Background in efficiency mode: {Summarize(eco)}").ConfigureAwait(false);
            }

            if (notMoved.Count > 0)
            {
                await this.AuditAsync(ActivityAuditSeverity.Info, "Background left unchanged", Summarize(notMoved)).ConfigureAwait(false);
            }
        }

        private static void Collect(ApplyOutcome outcome, string reason, string what, string name, List<string> applied, List<string> notApplied)
        {
            switch (outcome)
            {
                case ApplyOutcome.Applied:
                    applied.Add(name);
                    break;
                case ApplyOutcome.UserRule:
                    notApplied.Add($"{name} (your rule)");
                    break;
                case ApplyOutcome.ExistingSelection:
                    notApplied.Add($"{name} ({what} already set)");
                    break;
                case ApplyOutcome.Unreadable:
                case ApplyOutcome.Failed:
                    notApplied.Add($"{name} ({reason})");
                    break;
            }
        }

        private void ResetSessionState()
        {
            this.tickCount = 0;
            this.loadTracker = null;
            this.lastLoadSample = null;
        }

        // The dynamic part: a confined game that keeps its cores saturated gets every core back from Windows,
        // and is confined again once its load has stayed low for a while (see GameLoadTracker).
        private async Task AdaptGameCpuSetsAsync()
        {
            var currentSession = this.session;
            if (currentSession == null || this.loadTracker == null)
            {
                return;
            }

            var game = currentSession.Game;
            var now = this.timeProvider.GetTimestamp();
            if (!this.processProbe.TryGetCpuTime(game.ProcessId, out var cpuTime))
            {
                return;
            }

            var previous = this.lastLoadSample;
            this.lastLoadSample = (cpuTime, now);
            if (previous is not var (previousCpu, previousStamp))
            {
                return;
            }

            var elapsed = this.timeProvider.GetElapsedTime(previousStamp, now);
            if (elapsed <= TimeSpan.Zero)
            {
                return;
            }

            var action = this.loadTracker.Observe((cpuTime - previousCpu) / elapsed);
            if (action == GameLoadAction.None)
            {
                return;
            }

            var decision = await this.GetDecisionAsync().ConfigureAwait(false);
            var cpuCount = decision.GameSelection?.GlobalLogicalProcessorIndexes.Count ?? 0;
            var rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
            using var gate = await this.applyGate.EnterAsync().ConfigureAwait(false);

            if (action == GameLoadAction.Release)
            {
                var entry = this.journal.FirstOrDefault(e => e.ProcessId == game.ProcessId
                    && e.StartTimeUtcTicks == currentSession.GameStartTimeUtcTicks
                    && e.Setting == GameBoostSetting.CpuSets);
                var outcome = entry == null ? RestoreOutcome.LeftAlone : await this.RestoreAsync(entry, rules).ConfigureAwait(false);
                if (outcome != RestoreOutcome.Failed && entry != null)
                {
                    this.journal.Remove(entry);
                    await this.SaveJournalAsync().ConfigureAwait(false);
                }

                if (outcome != RestoreOutcome.Restored)
                {
                    // Someone else owns the cores now, or the call failed: stop adapting for this session.
                    this.loadTracker = null;
                    return;
                }

                var final = this.loadTracker.Releases >= GameLoadTracker.MaxReleases;
                await this.AuditAsync(
                    ActivityAuditSeverity.Info,
                    $"{game.Name}: kept its {cpuCount} CPUs busy, CPU Sets released so Windows can use every core",
                    final ? "It stays on all cores for the rest of the session" : "They are set again once its load stays low for about a minute").ConfigureAwait(false);
                return;
            }

            if (decision.GameSelection == null)
            {
                this.loadTracker = null;
                return;
            }

            this.attempted.Remove((game.ProcessId, currentSession.GameStartTimeUtcTicks, GameBoostSetting.CpuSets));
            var (applied, reason) = await this.TryApplyCpuSetsAsync(game, currentSession.GameStartTimeUtcTicks, decision.GameSelection, rules).ConfigureAwait(false);
            if (applied != ApplyOutcome.Applied)
            {
                this.loadTracker = null;
                await this.AuditGameCpuSetsAsync(game.Name, applied, reason, cpuCount).ConfigureAwait(false);
                return;
            }

            await this.AuditAsync(ActivityAuditSeverity.Info, $"{game.Name}: load is low again, CPU Sets set back to {cpuCount} CPUs").ConfigureAwait(false);
        }

        private Task AuditGameCpuSetsAsync(string game, ApplyOutcome outcome, string reason, int cpuCount) => outcome switch
        {
            ApplyOutcome.Applied => this.AuditAsync(ActivityAuditSeverity.Success, $"{game}: CPU Sets set to {cpuCount} CPUs"),
            ApplyOutcome.UserRule => this.AuditAsync(ActivityAuditSeverity.Info, $"{game}: cores left to your rule"),
            ApplyOutcome.ExistingSelection => this.AuditAsync(ActivityAuditSeverity.Info, $"{game}: already has CPU Sets, left unchanged"),
            ApplyOutcome.Unreadable or ApplyOutcome.Failed => this.AuditAsync(ActivityAuditSeverity.Warning, $"{game}: CPU Sets not changed", reason),
            _ => Task.CompletedTask,
        };

        private Task AuditGameQosAsync(string game, ApplyOutcome outcome, string reason) => outcome switch
        {
            ApplyOutcome.Applied => this.AuditAsync(ActivityAuditSeverity.Success, $"{game}: high QoS, Windows will not power-throttle it"),
            ApplyOutcome.UserRule => this.AuditAsync(ActivityAuditSeverity.Info, $"{game}: efficiency mode left to your rule"),
            ApplyOutcome.ExistingSelection => this.AuditAsync(ActivityAuditSeverity.Info, $"{game}: efficiency mode already set by the game or you, left unchanged"),
            ApplyOutcome.Unreadable or ApplyOutcome.Failed => this.AuditAsync(ActivityAuditSeverity.Warning, $"{game}: QoS not changed", reason),
            _ => Task.CompletedTask,
        };

        private bool IsBackgroundCandidate(ProcessModel process, GameSession currentSession) =>
            process.ProcessId != currentSession.Game.ProcessId
            && process.ProcessId != Environment.ProcessId
            && GameBoostProcessLists.IsListedBackground(process.Name, process.ExecutablePath, this.Settings.GameBoostMode, this.Settings.GameBoostBackgroundProcesses)
            && !this.processFilterService.IsSystemProcess(process)
            && !IsUnderDirectory(process.ExecutablePath, currentSession.GameDirectory);

        private async Task<(ApplyOutcome Outcome, string Reason)> TryApplyCpuSetsAsync(
            ProcessModel process,
            long startTicks,
            CpuSelection selection,
            IReadOnlyList<PersistentProcessRule> rules)
        {
            // Applied at most once per process: a later manual change is the user's and is never undone or redone.
            if (!this.attempted.Add((process.ProcessId, startTicks, GameBoostSetting.CpuSets)))
            {
                return (ApplyOutcome.AlreadyAttempted, string.Empty);
            }

            if (this.HasUserRule(rules, process, GameBoostSetting.CpuSets))
            {
                return (ApplyOutcome.UserRule, string.Empty);
            }

            // null: unreadable, so unknown; non-empty: someone else's selection. Only an empty set is ours to change.
            var current = await this.processService.GetCpuAssignmentLogicalProcessorIndexesAsync(process, CpuAssignmentMode.CpuSets).ConfigureAwait(false);
            if (current == null)
            {
                return (ApplyOutcome.Unreadable, "CPU Sets unreadable: access denied or protected process");
            }

            if (current.Count > 0)
            {
                return (ApplyOutcome.ExistingSelection, string.Empty);
            }

            var entry = new GameBoostJournalEntry
            {
                ProcessId = process.ProcessId,
                StartTimeUtcTicks = startTicks,
                ProcessName = process.Name,
                Setting = GameBoostSetting.CpuSets,
                AppliedCpuIndexes = selection.GlobalLogicalProcessorIndexes.ToList(),
            };
            await this.AddJournalEntryAsync(entry).ConfigureAwait(false);

            var result = await this.affinityApplyService.ApplyAsync(process, selection, CpuAssignmentMode.CpuSets).ConfigureAwait(false);
            if (result.Success)
            {
                await this.ConfirmJournalEntryAsync(entry).ConfigureAwait(false);
                this.logger.LogInformation(
                    "Game Boost set CPU Sets of {ProcessName} (PID {ProcessId}) to {CpuCount} CPUs",
                    process.Name,
                    process.ProcessId,
                    entry.AppliedCpuIndexes.Count);
                return (ApplyOutcome.Applied, string.Empty);
            }
            else
            {
                // Left in the journal unconfirmed: if the call half-succeeded, the restore still finds and undoes it.
                this.logger.LogInformation(
                    "Game Boost skipped {ProcessName} (PID {ProcessId}): {Reason}",
                    process.Name,
                    process.ProcessId,
                    result.TechnicalMessage);
                return (ApplyOutcome.Failed, string.IsNullOrWhiteSpace(result.UserMessage) ? "not applied" : result.UserMessage.TrimEnd('.'));
            }
        }

        // Same ownership rules as CPU Sets: only a process Windows still manages (no explicit QoS from the app,
        // the user or a rule) is changed, once, and restored only if the value is still Game Boost's.
        private async Task<(ApplyOutcome Outcome, string Reason)> TryApplyEfficiencyModeAsync(
            ProcessModel process,
            long startTicks,
            ProcessEfficiencyMode mode,
            IReadOnlyList<PersistentProcessRule> rules)
        {
            if (!this.attempted.Add((process.ProcessId, startTicks, GameBoostSetting.EfficiencyMode)))
            {
                return (ApplyOutcome.AlreadyAttempted, string.Empty);
            }

            if (this.HasUserRule(rules, process, GameBoostSetting.EfficiencyMode))
            {
                return (ApplyOutcome.UserRule, string.Empty);
            }

            var current = await this.efficiencyService.GetEfficiencyModeAsync(process).ConfigureAwait(false);
            if (current == null)
            {
                return (ApplyOutcome.Unreadable, "efficiency mode unreadable: access denied or protected process");
            }

            if (current != ProcessEfficiencyMode.SystemManaged)
            {
                return (ApplyOutcome.ExistingSelection, string.Empty);
            }

            var entry = new GameBoostJournalEntry
            {
                ProcessId = process.ProcessId,
                StartTimeUtcTicks = startTicks,
                ProcessName = process.Name,
                Setting = GameBoostSetting.EfficiencyMode,
                AppliedEfficiencyMode = mode,
            };
            await this.AddJournalEntryAsync(entry).ConfigureAwait(false);

            var result = await this.efficiencyService.SetEfficiencyModeAsync(process, mode).ConfigureAwait(false);
            if (!result.Success)
            {
                this.logger.LogInformation("Game Boost could not set efficiency mode {Mode} on {ProcessName} (PID {ProcessId})", mode, process.Name, process.ProcessId);
                return (ApplyOutcome.Failed, "efficiency mode not applied");
            }

            await this.ConfirmJournalEntryAsync(entry).ConfigureAwait(false);
            return (ApplyOutcome.Applied, string.Empty);
        }

        private async Task TryApplyAboveNormalAsync(ProcessModel game, long startTicks, IReadOnlyList<PersistentProcessRule> rules)
        {
            if (this.journal.Any(e => e.ProcessId == game.ProcessId && e.StartTimeUtcTicks == startTicks && e.Setting == GameBoostSetting.Priority))
            {
                return;
            }

            if (this.HasUserRule(rules, game, GameBoostSetting.Priority))
            {
                await this.AuditAsync(ActivityAuditSeverity.Info, $"{game.Name}: priority left to your rule").ConfigureAwait(false);
                return;
            }

            if (game.Priority != ProcessPriorityClass.Normal)
            {
                await this.AuditAsync(ActivityAuditSeverity.Info, $"{game.Name}: priority left at {game.Priority}", "Game Boost only raises Normal to Above Normal").ConfigureAwait(false);
                return;
            }

            var entry = new GameBoostJournalEntry
            {
                ProcessId = game.ProcessId,
                StartTimeUtcTicks = startTicks,
                ProcessName = game.Name,
                Setting = GameBoostSetting.Priority,
                OriginalPriority = ProcessPriorityClass.Normal,
                AppliedPriority = ProcessPriorityClass.AboveNormal,
            };
            await this.AddJournalEntryAsync(entry).ConfigureAwait(false);

            try
            {
                await this.processService.SetProcessPriority(game, ProcessPriorityClass.AboveNormal, ProcessPriorityWriteSource.GameBoost).ConfigureAwait(false);
                await this.ConfirmJournalEntryAsync(entry).ConfigureAwait(false);
                await this.AuditAsync(ActivityAuditSeverity.Success, $"{game.Name}: priority set to Above Normal").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogInformation(ex, "Game Boost could not raise the priority of {Game}", game.Name);
                await this.AuditAsync(ActivityAuditSeverity.Warning, $"{game.Name}: priority not changed", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task EndSessionAsync(string reason)
        {
            if (this.session == null && this.journal.Count == 0)
            {
                return;
            }

            var gameName = this.session?.Game.Name;
            if (gameName != null)
            {
                this.logger.LogInformation("Game Boost session for {Game} ended: {Reason}", gameName, reason);
            }

            this.session = null;
            this.attempted.Clear();
            var (restored, left, failed) = await this.RestoreAllAsync().ConfigureAwait(false);
            await this.AuditAsync(
                failed.Count > 0 ? ActivityAuditSeverity.Warning : ActivityAuditSeverity.Info,
                gameName == null ? $"Changes restored: {reason}" : $"Session ended for {gameName}: {reason}",
                DescribeRestore(restored, left, failed)).ConfigureAwait(false);
        }

        private async Task<(int Restored, int LeftAlone, List<string> Failed)> RestoreAllAsync()
        {
            var restored = 0;
            var left = 0;
            var failed = new List<string>();
            IReadOnlyList<PersistentProcessRule> rules;
            try
            {
                rules = await this.ruleStore.LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Game Boost could not read persistent rules before restoring; restoring anyway");
                rules = [];
            }

            using var gate = await this.applyGate.EnterAsync().ConfigureAwait(false);
            foreach (var entry in this.journal.ToList())
            {
                RestoreOutcome outcome;
                try
                {
                    outcome = await this.RestoreAsync(entry, rules).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Game Boost could not restore {Setting} of {ProcessName} (PID {ProcessId})", entry.Setting, entry.ProcessName, entry.ProcessId);
                    outcome = RestoreOutcome.Failed;
                }

                switch (outcome)
                {
                    case RestoreOutcome.Restored:
                        restored++;
                        break;
                    case RestoreOutcome.LeftAlone:
                        left++;
                        break;
                    default:
                        failed.Add($"{entry.ProcessName} {entry.Setting switch { GameBoostSetting.CpuSets => "CPU Sets", GameBoostSetting.EfficiencyMode => "efficiency mode", _ => "priority" }}");
                        break;
                }

                this.journal.Remove(entry);
            }

            await this.SaveJournalAsync().ConfigureAwait(false);
            return (restored, left, failed);
        }

        // Restores only a value that is still exactly what Game Boost set, on the same process instance,
        // and that no user rule has claimed since.
        private async Task<RestoreOutcome> RestoreAsync(GameBoostJournalEntry entry, IReadOnlyList<PersistentProcessRule> rules)
        {
            if (!this.IsSameProcessAlive(entry.ProcessId, entry.StartTimeUtcTicks))
            {
                return RestoreOutcome.LeftAlone;
            }

            var process = await this.processService.GetProcessByIdAsync(entry.ProcessId).ConfigureAwait(false);
            if (process == null || this.HasUserRule(rules, process, entry.Setting))
            {
                return RestoreOutcome.LeftAlone;
            }

            if (entry.Setting == GameBoostSetting.CpuSets)
            {
                var current = await this.processService.GetCpuAssignmentLogicalProcessorIndexesAsync(process, CpuAssignmentMode.CpuSets).ConfigureAwait(false);
                if (current == null || !current.SequenceEqual(entry.AppliedCpuIndexes.OrderBy(index => index)))
                {
                    return RestoreOutcome.LeftAlone;
                }

                if (!await this.processService.ClearProcessCpuSetAsync(process).ConfigureAwait(false))
                {
                    return RestoreOutcome.Failed;
                }

                this.logger.LogInformation("Game Boost restored CPU Sets of {ProcessName} (PID {ProcessId})", process.Name, process.ProcessId);
                return RestoreOutcome.Restored;
            }

            if (entry.Setting == GameBoostSetting.EfficiencyMode)
            {
                if (entry.AppliedEfficiencyMode == null
                    || await this.efficiencyService.GetEfficiencyModeAsync(process).ConfigureAwait(false) != entry.AppliedEfficiencyMode)
                {
                    return RestoreOutcome.LeftAlone;
                }

                var result = await this.efficiencyService.SetEfficiencyModeAsync(process, ProcessEfficiencyMode.SystemManaged).ConfigureAwait(false);
                return result.Success ? RestoreOutcome.Restored : RestoreOutcome.Failed;
            }

            if (entry.OriginalPriority is not ProcessPriorityClass original || process.Priority != entry.AppliedPriority)
            {
                return RestoreOutcome.LeftAlone;
            }

            await this.processService.SetProcessPriority(process, original, ProcessPriorityWriteSource.CleanupOrRestore).ConfigureAwait(false);
            return RestoreOutcome.Restored;
        }

        // "chrome x12, steam x2": the many processes of one app read as one item.
        private static string Summarize(IEnumerable<string> names) =>
            string.Join(", ", names
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key));

        private static string DescribeRestore(int restored, int left, IReadOnlyCollection<string> failed)
        {
            var text = $"Restored {restored} change(s)";
            if (left > 0)
            {
                text += $"; {left} left as they are (changed by you or a rule since, or the process exited)";
            }

            return failed.Count > 0 ? $"{text}; could not restore: {Summarize(failed)}" : text;
        }

        private Task AuditAsync(ActivityAuditSeverity severity, string message, string? details = null)
        {
            if (this.activityAuditService == null)
            {
                return Task.CompletedTask;
            }

            const string category = ActivityAuditCategories.GameBoost;
            return severity switch
            {
                ActivityAuditSeverity.Success => this.activityAuditService.LogSuccessAsync(category, message, details),
                ActivityAuditSeverity.Warning => this.activityAuditService.LogWarningAsync(category, message, details),
                ActivityAuditSeverity.Error => this.activityAuditService.LogErrorAsync(category, message, details),
                _ => this.activityAuditService.LogInfoAsync(category, message, details),
            };
        }

        private bool HasUserRule(IReadOnlyList<PersistentProcessRule> rules, ProcessModel process, GameBoostSetting setting) =>
            rules.Any(rule => this.ruleMatcher.IsMatch(rule, process) && setting switch
            {
                GameBoostSetting.CpuSets => rule.CpuSelection != null || rule.LegacyAffinityMask.HasValue,
                GameBoostSetting.EfficiencyMode => rule.EfficiencyMode.HasValue,
                _ => rule.Priority.HasValue,
            });

        private bool IsSameProcessAlive(int processId, long startTicks) =>
            this.processProbe.TryGetIdentity(processId, out var currentStartTicks, out _) && currentStartTicks == startTicks;

        private static bool IsUnderDirectory(string? path, string? directory) =>
            !string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(directory)
            && path.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

        private void OnSettingsChanged(object? sender, ApplicationSettingsChangedEventArgs e)
        {
            var oldSettings = e.OldSettings;
            var newSettings = e.NewSettings;
            var changed = oldSettings.GameBoostEnabled != newSettings.GameBoostEnabled
                || oldSettings.GameBoostMode != newSettings.GameBoostMode
                || oldSettings.GameBoostAboveNormalPriority != newSettings.GameBoostAboveNormalPriority
                || !oldSettings.GameBoostBackgroundProcesses.SequenceEqual(newSettings.GameBoostBackgroundProcesses)
                || !oldSettings.GameBoostManualGames.SequenceEqual(newSettings.GameBoostManualGames)
                || !oldSettings.GameBoostExcludedGames.SequenceEqual(newSettings.GameBoostExcludedGames);
            if (changed)
            {
                this.rejectedForeground = null;
                this.SyncTimer();
                TaskSafety.FireAndForget(this.ReapplyAsync(), ex => this.logger.LogWarning(ex, "Game Boost could not apply the new settings"));
            }
        }

        // A settings change restores everything first, then applies the new policy to the same game, if still running.
        internal async Task ReapplyAsync()
        {
            await this.stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var previous = this.session;
                await this.EndSessionAsync("Game Boost settings changed").ConfigureAwait(false);
                if (previous == null
                    || !this.Settings.GameBoostEnabled
                    || !this.IsSameProcessAlive(previous.Game.ProcessId, previous.GameStartTimeUtcTicks)
                    || !GameBoostProcessLists.IsGame(previous.Game.Name, previous.Game.ExecutablePath, this.Settings.GameBoostManualGames, this.Settings.GameBoostExcludedGames))
                {
                    return;
                }

                this.session = previous;
                this.ResetSessionState();
                await this.AuditSessionStartAsync(previous.Game).ConfigureAwait(false);
                await this.ApplyToSessionAsync(includeGame: true).ConfigureAwait(false);
            }
            finally
            {
                this.stateLock.Release();
            }
        }

        private async Task AddJournalEntryAsync(GameBoostJournalEntry entry)
        {
            this.journal.Add(entry);
            await this.SaveJournalAsync().ConfigureAwait(false);
        }

        private async Task ConfirmJournalEntryAsync(GameBoostJournalEntry entry)
        {
            var index = this.journal.IndexOf(entry);
            if (index >= 0)
            {
                this.journal[index] = entry with { Confirmed = true };
                await this.SaveJournalAsync().ConfigureAwait(false);
            }
        }

        private async Task<List<GameBoostJournalEntry>> LoadJournalAsync()
        {
            try
            {
                if (!File.Exists(this.journalPath))
                {
                    return new();
                }

                var json = await File.ReadAllTextAsync(this.journalPath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<List<GameBoostJournalEntry>>(json) ?? new();
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Game Boost journal at {Path} is unreadable; nothing to restore", this.journalPath);
                return new();
            }
        }

        private async Task SaveJournalAsync()
        {
            try
            {
                if (this.journal.Count == 0)
                {
                    File.Delete(this.journalPath);
                    return;
                }

                await AtomicFileWriter.WriteAllTextAsync(this.journalPath, JsonSerializer.Serialize(this.journal, JournalJsonOptions)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Game Boost could not write its journal at {Path}", this.journalPath);
            }
        }

        private static void LogAmdVCacheDriver(ILogger logger)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\amd3dvcache");
                if (key != null)
                {
                    logger.LogInformation("AMD 3D V-Cache Performance Optimizer driver is installed; it may also park cores for games");
                }
            }
            catch
            {
                // Logging only.
            }
        }

        private enum ApplyOutcome
        {
            AlreadyAttempted,
            Applied,
            UserRule,
            ExistingSelection,
            Unreadable,
            Failed,
        }

        private enum RestoreOutcome
        {
            Restored,
            LeftAlone,
            Failed,
        }

        private sealed record GameSession(ProcessModel Game, long GameStartTimeUtcTicks, int GameSessionId, string? GameDirectory);
    }
}
