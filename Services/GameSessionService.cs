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

        public bool Confirmed { get; init; }
    }

    public interface IGameBoostProcessProbe
    {
        /// <summary>Start time and Windows session of a running process; false when it is gone or unreadable.</summary>
        bool TryGetIdentity(int processId, out long startTimeUtcTicks, out int sessionId);
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
    /// Applies the Game Boost policy for one game session at a time: the game's and the listed background
    /// processes' default CPU Sets (and optionally Above Normal priority for the game), each applied once and
    /// restored when the game exits, Game Boost is turned off or ThreadPilot closes. User rules always win.
    /// </summary>
    public sealed class GameSessionService : IGameSessionService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

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
        private readonly ILogger<GameSessionService> logger;
        private readonly string journalPath;
        private readonly SemaphoreSlim stateLock = new(1, 1);
        private readonly HashSet<(int ProcessId, long StartTimeUtcTicks)> attempted = new();

        private List<GameBoostJournalEntry> journal = new();
        private Timer? timer;
        private GameSession? session;
        private GameBoostDecision? cachedDecision;
        private GameBoostMode cachedDecisionMode;
        private int tickCount;
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
            ILogger<GameSessionService> logger,
            string? journalPath = null)
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
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.journalPath = journalPath ?? Path.Combine(StoragePaths.AppDataRoot, "game_boost_journal.json");

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
                    await this.RestoreAllAsync().ConfigureAwait(false);
                }

                this.timer ??= new Timer(_ => TaskSafety.FireAndForget(this.TickAsync(), ex => this.logger.LogWarning(ex, "Game Boost tick failed")), null, TickInterval, TickInterval);
            }
            finally
            {
                this.stateLock.Release();
            }
        }

        public async Task StopAsync()
        {
            this.timer?.Dispose();
            this.timer = null;
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
            this.timer?.Dispose();
            this.timer = null;
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
                    await this.EndSessionAsync($"{this.session.Game.Name} exited").ConfigureAwait(false);
                    return;
                }

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

            var game = await this.processService.GetProcessByIdAsync(processId).ConfigureAwait(false);
            if (game == null
                || !GameBoostProcessLists.IsGame(game.Name, game.ExecutablePath, this.Settings.GameBoostManualGames, this.Settings.GameBoostExcludedGames)
                || !this.processProbe.TryGetIdentity(processId, out var startTicks, out var sessionId))
            {
                return;
            }

            this.session = new GameSession(game, startTicks, sessionId, Path.GetDirectoryName(game.ExecutablePath));
            this.tickCount = 0;
            this.logger.LogInformation("Game Boost session started for {Game} (PID {ProcessId})", game.Name, game.ProcessId);
            await this.ApplyToSessionAsync(includeGame: true).ConfigureAwait(false);
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
                if (decision.GameSelection != null)
                {
                    await this.TryApplyCpuSetsAsync(game, currentSession.GameStartTimeUtcTicks, decision.GameSelection, rules).ConfigureAwait(false);
                }

                if (this.Settings.GameBoostAboveNormalPriority)
                {
                    await this.TryApplyAboveNormalAsync(game, currentSession.GameStartTimeUtcTicks, rules).ConfigureAwait(false);
                }
            }

            if (decision.BackgroundSelection == null)
            {
                return;
            }

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

                await this.TryApplyCpuSetsAsync(process, startTicks, decision.BackgroundSelection, rules).ConfigureAwait(false);
            }
        }

        private bool IsBackgroundCandidate(ProcessModel process, GameSession currentSession) =>
            process.ProcessId != currentSession.Game.ProcessId
            && process.ProcessId != Environment.ProcessId
            && GameBoostProcessLists.IsListedBackground(process.Name, process.ExecutablePath, this.Settings.GameBoostMode, this.Settings.GameBoostBackgroundProcesses)
            && !this.processFilterService.IsSystemProcess(process)
            && !IsUnderDirectory(process.ExecutablePath, currentSession.GameDirectory);

        private async Task TryApplyCpuSetsAsync(
            ProcessModel process,
            long startTicks,
            CpuSelection selection,
            IReadOnlyList<PersistentProcessRule> rules)
        {
            // Applied at most once per process: a later manual change is the user's and is never undone or redone.
            if (!this.attempted.Add((process.ProcessId, startTicks))
                || this.HasUserRule(rules, process, GameBoostSetting.CpuSets))
            {
                return;
            }

            // null: unreadable, so unknown; non-empty: someone else's selection. Only an empty set is ours to change.
            var current = await this.processService.GetCpuAssignmentLogicalProcessorIndexesAsync(process, CpuAssignmentMode.CpuSets).ConfigureAwait(false);
            if (current == null || current.Count > 0)
            {
                return;
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
            }
            else
            {
                // Left in the journal unconfirmed: if the call half-succeeded, the restore still finds and undoes it.
                this.logger.LogInformation(
                    "Game Boost skipped {ProcessName} (PID {ProcessId}): {Reason}",
                    process.Name,
                    process.ProcessId,
                    result.TechnicalMessage);
            }
        }

        private async Task TryApplyAboveNormalAsync(ProcessModel game, long startTicks, IReadOnlyList<PersistentProcessRule> rules)
        {
            if (game.Priority != ProcessPriorityClass.Normal
                || this.HasUserRule(rules, game, GameBoostSetting.Priority)
                || this.journal.Any(e => e.ProcessId == game.ProcessId && e.StartTimeUtcTicks == startTicks && e.Setting == GameBoostSetting.Priority))
            {
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
            }
            catch (Exception ex)
            {
                this.logger.LogInformation(ex, "Game Boost could not raise the priority of {Game}", game.Name);
            }
        }

        private async Task EndSessionAsync(string reason)
        {
            if (this.session == null && this.journal.Count == 0)
            {
                return;
            }

            if (this.session != null)
            {
                this.logger.LogInformation("Game Boost session for {Game} ended: {Reason}", this.session.Game.Name, reason);
            }

            this.session = null;
            this.attempted.Clear();
            await this.RestoreAllAsync().ConfigureAwait(false);
        }

        private async Task RestoreAllAsync()
        {
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
                try
                {
                    await this.RestoreAsync(entry, rules).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Game Boost could not restore {Setting} of {ProcessName} (PID {ProcessId})", entry.Setting, entry.ProcessName, entry.ProcessId);
                }

                this.journal.Remove(entry);
            }

            await this.SaveJournalAsync().ConfigureAwait(false);
        }

        // Restores only a value that is still exactly what Game Boost set, on the same process instance,
        // and that no user rule has claimed since.
        private async Task RestoreAsync(GameBoostJournalEntry entry, IReadOnlyList<PersistentProcessRule> rules)
        {
            if (!this.IsSameProcessAlive(entry.ProcessId, entry.StartTimeUtcTicks))
            {
                return;
            }

            var process = await this.processService.GetProcessByIdAsync(entry.ProcessId).ConfigureAwait(false);
            if (process == null || this.HasUserRule(rules, process, entry.Setting))
            {
                return;
            }

            if (entry.Setting == GameBoostSetting.CpuSets)
            {
                var current = await this.processService.GetCpuAssignmentLogicalProcessorIndexesAsync(process, CpuAssignmentMode.CpuSets).ConfigureAwait(false);
                if (current != null && current.SequenceEqual(entry.AppliedCpuIndexes.OrderBy(index => index)))
                {
                    await this.processService.ClearProcessCpuSetAsync(process).ConfigureAwait(false);
                    this.logger.LogInformation("Game Boost restored CPU Sets of {ProcessName} (PID {ProcessId})", process.Name, process.ProcessId);
                }

                return;
            }

            if (entry.OriginalPriority is ProcessPriorityClass original && process.Priority == entry.AppliedPriority)
            {
                await this.processService.SetProcessPriority(process, original, ProcessPriorityWriteSource.CleanupOrRestore).ConfigureAwait(false);
            }
        }

        private bool HasUserRule(IReadOnlyList<PersistentProcessRule> rules, ProcessModel process, GameBoostSetting setting) =>
            rules.Any(rule => this.ruleMatcher.IsMatch(rule, process) && (setting == GameBoostSetting.CpuSets
                ? rule.CpuSelection != null || rule.LegacyAffinityMask.HasValue
                : rule.Priority.HasValue));

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
                this.tickCount = 0;
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

        private sealed record GameSession(ProcessModel Game, long GameStartTimeUtcTicks, int GameSessionId, string? GameDirectory);
    }
}
