namespace ThreadPilot.Core.Tests
{
    using System.Linq;
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;

    public sealed class CpuAssignmentModeMigrationPolicyTests
    {
        [Fact]
        public void Apply_MovesAnUpgradedProfileOffAutomatic()
        {
            // The case that shipped broken: 1.7.1 changed the default, but a persisted Automatic
            // won over it, so upgrading users kept soft CPU Sets and saw no affinity change.
            var settings = new ApplicationSettingsModel
            {
                DefaultCpuAssignmentMode = CpuAssignmentMode.Automatic,
                HasMigratedCpuAssignmentModeDefault = false,
                HasSeenCpuAssignmentModeChangeNotice = false,
            };

            var changed = CpuAssignmentModeMigrationPolicy.Apply(settings);

            Assert.True(changed);
            Assert.Equal(CpuAssignmentMode.AffinityMask, settings.DefaultCpuAssignmentMode);
            Assert.True(settings.HasMigratedCpuAssignmentModeDefault);
            Assert.False(settings.HasSeenCpuAssignmentModeChangeNotice);
            Assert.True(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));
        }

        [Theory]
        [InlineData(CpuAssignmentMode.AffinityMask)]
        [InlineData(CpuAssignmentMode.IdealProcessor)]
        [InlineData(CpuAssignmentMode.CpuSets)]
        public void Apply_LeavesAnyOtherModeAloneButStillRecordsCompletion(CpuAssignmentMode mode)
        {
            var settings = new ApplicationSettingsModel
            {
                DefaultCpuAssignmentMode = mode,
                HasMigratedCpuAssignmentModeDefault = false,
                HasSeenCpuAssignmentModeChangeNotice = false,
            };

            var changed = CpuAssignmentModeMigrationPolicy.Apply(settings);

            // True because the completion flags changed: they have to be persisted, or the policy
            // re-runs next launch and would undo a later deliberate switch to Automatic.
            Assert.True(changed);
            Assert.Equal(mode, settings.DefaultCpuAssignmentMode);
            Assert.True(settings.HasMigratedCpuAssignmentModeDefault);
            Assert.False(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));
        }

        [Fact]
        public void Apply_DoesNotOverrideAutomaticChosenAfterTheMigration()
        {
            // Someone who read the notice and deliberately went back to Automatic must stay there.
            var settings = new ApplicationSettingsModel
            {
                DefaultCpuAssignmentMode = CpuAssignmentMode.Automatic,
                HasMigratedCpuAssignmentModeDefault = true,
                HasSeenCpuAssignmentModeChangeNotice = true,
            };

            var changed = CpuAssignmentModeMigrationPolicy.Apply(settings);

            Assert.False(changed);
            Assert.Equal(CpuAssignmentMode.Automatic, settings.DefaultCpuAssignmentMode);
            Assert.False(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));
        }

        [Fact]
        public void Apply_IsIdempotent()
        {
            var settings = new ApplicationSettingsModel
            {
                DefaultCpuAssignmentMode = CpuAssignmentMode.Automatic,
            };

            Assert.True(CpuAssignmentModeMigrationPolicy.Apply(settings));
            Assert.False(CpuAssignmentModeMigrationPolicy.Apply(settings));
            Assert.Equal(CpuAssignmentMode.AffinityMask, settings.DefaultCpuAssignmentMode);
        }

        [Fact]
        public void ShouldShowNotice_StaysTrueUntilAcknowledged()
        {
            // The notice is the only thing telling the user their mode moved, so a restart before
            // they dismiss it must not swallow it.
            var settings = new ApplicationSettingsModel { DefaultCpuAssignmentMode = CpuAssignmentMode.Automatic };
            CpuAssignmentModeMigrationPolicy.Apply(settings);

            Assert.True(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));
            CpuAssignmentModeMigrationPolicy.Apply(settings);
            Assert.True(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));

            settings.HasSeenCpuAssignmentModeChangeNotice = true;
            Assert.False(CpuAssignmentModeMigrationPolicy.ShouldShowNotice(settings));
        }

        [Fact]
        public void CopyFrom_CarriesBothFlags()
        {
            var source = new ApplicationSettingsModel
            {
                HasMigratedCpuAssignmentModeDefault = true,
                HasSeenCpuAssignmentModeChangeNotice = true,
                HasMigratedPersistentRuleCpuAssignmentModes = true,
            };
            var target = new ApplicationSettingsModel();

            target.CopyFrom(source);

            Assert.True(target.HasMigratedCpuAssignmentModeDefault);
            Assert.True(target.HasSeenCpuAssignmentModeChangeNotice);
            Assert.True(target.HasMigratedPersistentRuleCpuAssignmentModes);
        }

        [Fact]
        public void ApplyToRules_MovesASavedRuleWithACpuSelectionOffAutomatic()
        {
            // A rule saved before 1.7.1 keeps applying CPU Sets on every process start: nothing the
            // user can see changes, so the rule is indistinguishable from one that never runs.
            var rules = new[] { RuleWithSelection(CpuAssignmentMode.Automatic) };

            var migrated = CpuAssignmentModeMigrationPolicy.ApplyToRules(rules);

            Assert.NotNull(migrated);
            Assert.Equal(CpuAssignmentMode.AffinityMask, migrated!.Single().CpuAssignmentMode);
            Assert.Equal(rules[0].Id, migrated.Single().Id);
            Assert.Equal(rules[0].CpuSelection, migrated.Single().CpuSelection);
        }

        [Fact]
        public void ApplyToRules_IsIdempotent()
        {
            var rules = new[] { RuleWithSelection(CpuAssignmentMode.Automatic) };

            var migrated = CpuAssignmentModeMigrationPolicy.ApplyToRules(rules);

            Assert.Null(CpuAssignmentModeMigrationPolicy.ApplyToRules(migrated!));
        }

        [Theory]
        [InlineData(CpuAssignmentMode.AffinityMask)]
        [InlineData(CpuAssignmentMode.IdealProcessor)]
        [InlineData(CpuAssignmentMode.CpuSets)]
        public void ApplyToRules_LeavesADeliberateModeAlone(CpuAssignmentMode mode)
        {
            var rules = new[] { RuleWithSelection(mode) };

            Assert.Null(CpuAssignmentModeMigrationPolicy.ApplyToRules(rules));
        }

        [Fact]
        public void ApplyToRules_LeavesALegacyMaskRuleAlone()
        {
            // Automatic with a legacy mask already applies a real affinity, so there is nothing to
            // repair - and rewriting it would change a working rule for no reason.
            var rules = new[]
            {
                new PersistentProcessRule
                {
                    ProcessName = "cs2",
                    IsEnabled = true,
                    LegacyAffinityMask = 0b1111,
                    ApplyAffinityOnStart = true,
                    CpuAssignmentMode = CpuAssignmentMode.Automatic,
                },
            };

            Assert.Null(CpuAssignmentModeMigrationPolicy.ApplyToRules(rules));
        }

        [Fact]
        public void ApplyToRules_LeavesARuleThatDoesNotApplyAffinityAlone()
        {
            var rules = new[] { RuleWithSelection(CpuAssignmentMode.Automatic) with { ApplyAffinityOnStart = false } };

            Assert.Null(CpuAssignmentModeMigrationPolicy.ApplyToRules(rules));
        }

        private static PersistentProcessRule RuleWithSelection(CpuAssignmentMode mode) =>
            new()
            {
                ProcessName = "cs2",
                IsEnabled = true,
                ApplyAffinityOnStart = true,
                CpuAssignmentMode = mode,
                CpuSelection = new CpuSelection
                {
                    CpuSetIds = { 256u },
                    LogicalProcessors = { new ProcessorRef(0, 0, 0) },
                    GlobalLogicalProcessorIndexes = { 0 },
                },
            };
    }
}
