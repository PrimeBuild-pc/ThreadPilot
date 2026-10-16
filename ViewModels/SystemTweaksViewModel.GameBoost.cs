namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// Game Boost card on the Tweaks page. Every switch saves at once; the session service reacts to the
    /// settings change, so turning it off restores the running game without a restart.
    /// </summary>
    public partial class SystemTweaksViewModel
    {
        private readonly IApplicationSettingsService? settingsService;
        private readonly IGameSessionService? gameSessionService;
        private bool isLoadingGameBoost;

        [ObservableProperty]
        private bool isGameBoostAvailable;

        [ObservableProperty]
        private bool gameBoostEnabled;

        [ObservableProperty]
        private bool gameBoostAutomatic = true;

        [ObservableProperty]
        private bool gameBoostIsolation;

        [ObservableProperty]
        private bool gameBoostAboveNormal;

        [ObservableProperty]
        private string gameBoostDecisionText = string.Empty;

        [ObservableProperty]
        private string gameBoostManualGamesText = string.Empty;

        [ObservableProperty]
        private string gameBoostExcludedGamesText = string.Empty;

        [ObservableProperty]
        private string gameBoostBackgroundText = string.Empty;

        internal async Task LoadGameBoostAsync()
        {
            if (this.settingsService == null || this.gameSessionService == null)
            {
                return;
            }

            var settings = this.settingsService.Settings;
            await InvokeOnUiAsync(() =>
            {
                this.isLoadingGameBoost = true;
                try
                {
                    this.IsGameBoostAvailable = true;
                    this.GameBoostEnabled = settings.GameBoostEnabled;
                    this.GameBoostIsolation = settings.GameBoostMode == GameBoostMode.Isolation;
                    this.GameBoostAutomatic = !this.GameBoostIsolation;
                    this.GameBoostAboveNormal = settings.GameBoostAboveNormalPriority;
                    this.GameBoostManualGamesText = string.Join(Environment.NewLine, settings.GameBoostManualGames);
                    this.GameBoostExcludedGamesText = string.Join(Environment.NewLine, settings.GameBoostExcludedGames);
                    this.GameBoostBackgroundText = string.Join(Environment.NewLine, settings.GameBoostBackgroundProcesses);
                }
                finally
                {
                    this.isLoadingGameBoost = false;
                }
            });

            await this.RefreshGameBoostDecisionTextAsync();
        }

        [RelayCommand]
        private Task SaveGameBoostListsAsync() => this.SaveGameBoostAsync();

        partial void OnGameBoostEnabledChanged(bool value) => this.QueueGameBoostSave();

        partial void OnGameBoostAboveNormalChanged(bool value) => this.QueueGameBoostSave();

        partial void OnGameBoostIsolationChanged(bool value)
        {
            if (this.GameBoostAutomatic == value)
            {
                this.GameBoostAutomatic = !value;
            }

            this.QueueGameBoostSave();
        }

        partial void OnGameBoostAutomaticChanged(bool value)
        {
            if (this.GameBoostIsolation == value)
            {
                this.GameBoostIsolation = !value;
            }
        }

        private void QueueGameBoostSave()
        {
            if (!this.isLoadingGameBoost)
            {
                TaskSafety.FireAndForget(
                    this.SaveGameBoostAsync(),
                    ex => this.SetError(this.Localize("GameBoost_SaveFailed", "Could not save Game Boost settings"), ex));
            }
        }

        private async Task SaveGameBoostAsync()
        {
            if (this.settingsService == null)
            {
                return;
            }

            var updated = (ApplicationSettingsModel)this.settingsService.Settings.Clone();
            updated.GameBoostEnabled = this.GameBoostEnabled;
            updated.GameBoostMode = this.GameBoostIsolation ? GameBoostMode.Isolation : GameBoostMode.Automatic;
            updated.GameBoostAboveNormalPriority = this.GameBoostAboveNormal;
            updated.GameBoostManualGames = SplitLines(this.GameBoostManualGamesText);
            updated.GameBoostExcludedGames = SplitLines(this.GameBoostExcludedGamesText);
            updated.GameBoostBackgroundProcesses = SplitLines(this.GameBoostBackgroundText);

            await this.settingsService.UpdateSettingsAsync(updated);
            await this.LogUserActionAsync(
                "GameBoostSettingsChanged",
                $"Game Boost {(updated.GameBoostEnabled ? "on" : "off")}, {updated.GameBoostMode}",
                "GameBoost");
            await this.RefreshGameBoostDecisionTextAsync();
        }

        private async Task RefreshGameBoostDecisionTextAsync()
        {
            if (this.gameSessionService == null)
            {
                return;
            }

            var decision = await this.gameSessionService.GetDecisionAsync();
            var game = decision.GameSelection?.GlobalLogicalProcessorIndexes.Count ?? 0;
            var background = decision.BackgroundSelection?.GlobalLogicalProcessorIndexes.Count ?? 0;
            var text = decision.Kind switch
            {
                GameBoostTopologyKind.AmdAsymmetricCache => this.Localize(
                    "GameBoost_Decision_AmdVCache",
                    "Game on the CCD with 3D V-Cache ({0} CPUs), selected background apps on the other CCD ({1} CPUs).",
                    game,
                    background),
                GameBoostTopologyKind.AmdSymmetricMultiL3 when decision.GameSelection != null => this.Localize(
                    "GameBoost_Decision_AmdCcd0",
                    "Game on CCD0 ({0} CPUs), background apps on the other CCD ({1} CPUs).",
                    game,
                    background),
                GameBoostTopologyKind.AmdSymmetricMultiL3 => this.Localize(
                    "GameBoost_Decision_AmdBackgroundOnly",
                    "Windows places the game; selected background apps move off CCD0 ({0} CPUs). Isolation also keeps the game on CCD0.",
                    background),
                GameBoostTopologyKind.IntelHybrid => this.Localize(
                    "GameBoost_Decision_IntelHybrid",
                    "Game on the P-cores ({0} CPUs), selected background apps on the E-cores ({1} CPUs).",
                    game,
                    background),
                _ => this.Localize(
                    "GameBoost_Decision_Unsupported",
                    "On this CPU Windows decides the cores; Game Boost only applies the optional priority."),
            };

            await InvokeOnUiAsync(() => this.GameBoostDecisionText = text);
        }

        private static List<string> SplitLines(string text) =>
            (text ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
