namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreadPilot.Models;

    public enum GameBoostMode
    {
        Automatic = 0,
        Isolation = 1,
    }

    public enum GameBoostTopologyKind
    {
        /// <summary>Single L3, homogeneous cores, or topology Game Boost cannot trust: no CPU Sets.</summary>
        Unsupported = 0,

        /// <summary>AMD with one L3 group much larger than the others (3D V-Cache on one CCD).</summary>
        AmdAsymmetricCache,

        /// <summary>AMD with two or more L3 groups of the same size (e.g. 9950X, 9950X3D2).</summary>
        AmdSymmetricMultiL3,

        /// <summary>Intel with performance and efficiency cores.</summary>
        IntelHybrid,
    }

    public sealed record GameBoostDecision(
        GameBoostTopologyKind Kind,
        CpuSelection? GameSelection,
        CpuSelection? BackgroundSelection)
    {
        public static GameBoostDecision None { get; } = new(GameBoostTopologyKind.Unsupported, null, null);
    }

    /// <summary>
    /// Pure topology-to-CPU-Set policy. It only ever sets process default CPU Sets: Windows still schedules
    /// every thread inside them, and thread-level selections or hard affinities still take precedence.
    /// </summary>
    public static class GameBoostPolicy
    {
        // 3D V-Cache CCDs have 96 MB of L3 against 32 MB on the plain CCD; equal-size groups never qualify.
        public const double VCacheMinimumRatio = 2.0;

        // Below this a game would be squeezed (2P+8E laptops, 4-core Zen 2 CCX, Zen 5c clusters).
        public const int MinimumGamePhysicalCores = 6;

        public static GameBoostDecision Decide(CpuTopologySnapshot topology, string? cpuBrand, GameBoostMode mode)
        {
            ArgumentNullException.ThrowIfNull(topology);

            var brand = cpuBrand ?? string.Empty;
            var processors = topology.LogicalProcessors
                .Where(processor => !topology.ReservedLogicalProcessorIndexes.Contains(processor.GlobalIndex))
                .ToList();

            // Every processor needs a CPU Set ID; the Environment.ProcessorCount fallback has none.
            if (processors.Count == 0
                || topology.Signature.PackageCount > 1
                || processors.Any(processor => !topology.TryGetCpuSetId(processor, out _)))
            {
                return GameBoostDecision.None;
            }

            if (brand.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            {
                return DecideAmd(topology, processors, mode);
            }

            if (brand.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            {
                return DecideIntel(topology, processors);
            }

            return GameBoostDecision.None;
        }

        private static GameBoostDecision DecideAmd(CpuTopologySnapshot topology, List<ProcessorRef> processors, GameBoostMode mode)
        {
            var l3ByProcessor = new Dictionary<ProcessorRef, int>();
            foreach (var processor in processors)
            {
                if (!topology.TryGetLastLevelCacheIndex(processor, out var l3Index))
                {
                    return GameBoostDecision.None;
                }

                l3ByProcessor[processor] = l3Index;
            }

            var groups = processors
                .GroupBy(processor => l3ByProcessor[processor])
                .OrderBy(group => group.Min(processor => processor.GlobalIndex))
                .Select(group => (L3Index: group.Key, Processors: group.ToList()))
                .ToList();
            if (groups.Count < 2)
            {
                return GameBoostDecision.None;
            }

            var sizes = groups
                .Select(group => topology.TryGetLastLevelCacheSize(group.L3Index, out var size) ? size : 0UL)
                .ToList();
            var largest = sizes.Max();
            var isAsymmetric = sizes.All(size => size > 0)
                && sizes.Count(size => size == largest) == 1
                && sizes.Where(size => size != largest).All(size => largest >= size * VCacheMinimumRatio);

            if (isAsymmetric)
            {
                var cacheGroup = groups[sizes.IndexOf(largest)].Processors;
                return Build(
                    topology,
                    GameBoostTopologyKind.AmdAsymmetricCache,
                    cacheGroup,
                    processors.Except(cacheGroup).ToList());
            }

            // Equal L3s: CCD0 (the group holding the lowest CPU) gets the game only in Isolation.
            var firstGroup = groups[0].Processors;
            var others = processors.Except(firstGroup).ToList();
            return Build(
                topology,
                GameBoostTopologyKind.AmdSymmetricMultiL3,
                mode == GameBoostMode.Isolation ? firstGroup : null,
                others,
                backgroundTarget: firstGroup);
        }

        private static GameBoostDecision DecideIntel(CpuTopologySnapshot topology, List<ProcessorRef> processors)
        {
            var classes = new Dictionary<ProcessorRef, byte>();
            foreach (var processor in processors)
            {
                if (!topology.TryGetEfficiencyClass(processor, out var efficiencyClass))
                {
                    return GameBoostDecision.None;
                }

                classes[processor] = efficiencyClass;
            }

            var performanceClass = classes.Values.Max();
            if (classes.Values.All(value => value == performanceClass))
            {
                return GameBoostDecision.None;
            }

            var performanceCores = processors.Where(processor => classes[processor] == performanceClass).ToList();
            return Build(
                topology,
                GameBoostTopologyKind.IntelHybrid,
                performanceCores,
                processors.Except(performanceCores).ToList());
        }

        // backgroundTarget: the cores whose size decides whether the split is worth it when the game stays free.
        private static GameBoostDecision Build(
            CpuTopologySnapshot topology,
            GameBoostTopologyKind kind,
            List<ProcessorRef>? gameProcessors,
            List<ProcessorRef> backgroundProcessors,
            List<ProcessorRef>? backgroundTarget = null)
        {
            var gameSide = gameProcessors ?? backgroundTarget ?? [];
            if (backgroundProcessors.Count == 0 || CountPhysicalCores(topology, gameSide) < MinimumGamePhysicalCores)
            {
                return GameBoostDecision.None;
            }

            return new GameBoostDecision(
                kind,
                gameProcessors == null ? null : CpuSelection.FromProcessors(gameProcessors, topology, "Game Boost: game"),
                CpuSelection.FromProcessors(backgroundProcessors, topology, "Game Boost: background"));
        }

        private static int CountPhysicalCores(CpuTopologySnapshot topology, IEnumerable<ProcessorRef> processors) =>
            processors
                .Select(processor => topology.TryGetCoreIndex(processor, out var coreIndex) ? coreIndex : -1 - processor.GlobalIndex)
                .Distinct()
                .Count();
    }

    public enum GameLoadAction
    {
        None = 0,

        /// <summary>The game keeps its CPU Set busy: drop it so Windows can use every core.</summary>
        Release,

        /// <summary>The load has been low for a while: put the CPU Set back.</summary>
        Confine,
    }

    /// <summary>
    /// Decides when a confined game's CPU Set gets in the way. Fed one CPU-time sample per tick, it releases
    /// the CPU Set after a sustained high load and restores it after a longer low one. The two thresholds and
    /// the different lengths are the hysteresis; after <see cref="MaxReleases"/> the game stays on all cores.
    /// </summary>
    public sealed class GameLoadTracker
    {
        // shortcut: thresholds are first guesses, not measured; tune them with the beta testers' frame-time data.
        public const double ReleaseLoad = 0.8;
        public const double ConfineLoad = 0.5;
        public const int ReleaseSamples = 4;
        public const int ConfineSamples = 20;
        public const int MaxReleases = 2;

        private readonly int gameCpuCount;
        private int streak;

        public GameLoadTracker(int gameCpuCount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(gameCpuCount, 1);
            this.gameCpuCount = gameCpuCount;
        }

        public bool Released { get; private set; }

        public int Releases { get; private set; }

        /// <summary>Adds one load sample and says whether the game's CPU Set should change now.</summary>
        /// <param name="cpusUsed">CPU time the game used over the sample divided by its length (2.0 = two CPUs busy).</param>
        public GameLoadAction Observe(double cpusUsed)
        {
            if (this.Released && this.Releases >= MaxReleases)
            {
                return GameLoadAction.None;
            }

            var load = cpusUsed / this.gameCpuCount;
            var pressing = this.Released ? load <= ConfineLoad : load >= ReleaseLoad;
            this.streak = pressing ? this.streak + 1 : 0;
            if (this.streak < (this.Released ? ConfineSamples : ReleaseSamples))
            {
                return GameLoadAction.None;
            }

            this.streak = 0;
            this.Released = !this.Released;
            if (this.Released)
            {
                this.Releases++;
                return GameLoadAction.Release;
            }

            return GameLoadAction.Confine;
        }
    }
}
