namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;

    public sealed class ProcessViewXamlBindingTests
    {
        private static readonly string ProcessViewPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProcessView.xaml");

        [Fact]
        public void LastOperationMessageBinding_IsDisplayOnly()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var lastOperationBindings = document
                .Descendants()
                .SelectMany(element => element.Attributes().Select(attribute => new
                {
                    Element = element.Name.LocalName,
                    Attribute = attribute.Name.LocalName,
                    Value = attribute.Value,
                }))
                .Where(attribute => attribute.Value.Contains("SelectedProcessSummary.LastOperationMessage", StringComparison.Ordinal))
                .ToList();

            var binding = Assert.Single(lastOperationBindings);
            Assert.Equal("Text", binding.Attribute);
            Assert.Contains("Mode=OneWay", binding.Value, StringComparison.Ordinal);
        }

        [Fact]
        public void SelectedProcessSummaryBindings_AreNotUsedByEditableControls()
        {
            var editableControls = new HashSet<string>(StringComparer.Ordinal)
            {
                "CheckBox",
                "ComboBox",
                "DatePicker",
                "PasswordBox",
                "Slider",
                "TextBox",
                "ToggleButton",
            };
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);

            var editableSummaryBindings = document
                .Descendants()
                .Where(element => editableControls.Contains(element.Name.LocalName))
                .SelectMany(element => element.Attributes().Select(attribute => new
                {
                    Element = element.Name.LocalName,
                    Attribute = attribute.Name.LocalName,
                    Value = attribute.Value,
                }))
                .Where(attribute => attribute.Value.Contains("SelectedProcessSummary.", StringComparison.Ordinal))
                .ToList();

            Assert.Empty(editableSummaryBindings);
        }

        [Fact]
        public void ProcessGridRowStyle_HighlightsSelectedRowsWithAccentTheme()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("IsSelected", serialized, StringComparison.Ordinal);
            Assert.Contains("Accent", serialized, StringComparison.Ordinal);
            Assert.Contains("BorderThickness", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessGridContextMenu_MenuItemsUseStableDetachedMenuStyle()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("<ContextMenu", serialized, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource ProcessContextMenuStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TargetType=\"{x:Type MenuItem}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("FontWeight\" Value=\"Normal\"", serialized, StringComparison.Ordinal);
            Assert.Contains("FontSize\" Value=\"{DynamicResource BodyFontSize}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource ProcessContextMenuItemStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ControlTemplate TargetType=\"{x:Type MenuItem}\"", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight\" Value=\"{Binding", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight\" Value=\"{TemplateBinding", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessGridContextMenu_DoesNotApplyMenuItemStyleToSeparators()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("<Separator", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("ItemContainerStyle", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("<Separator Style=\"{StaticResource ProcessContextMenuItemStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("<Separator Style=\"{StaticResource ProcessContextMenuSeparatorStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TargetType=\"{x:Type Separator}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ControlTemplate TargetType=\"{x:Type Separator}\"", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessGridContextMenu_UsesThemeAwareTemplateWithoutIconCheckGutter()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("TargetType=\"{x:Type ContextMenu}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TargetType=\"{x:Type MenuItem}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ControlTemplate TargetType=\"{x:Type MenuItem}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("x:Name=\"SubmenuArrow\"", serialized, StringComparison.Ordinal);
            Assert.Contains("PART_Popup", serialized, StringComparison.Ordinal);
            Assert.Contains("QuietRowHoverBackgroundBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("CardSurfaceBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("BorderSubtleBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("TextPrimaryBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("TextDisabledBrush", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("Grid.IsSharedSizeScope", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("IconPresenter", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("GlyphPanel", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("Checkmark", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("CheckMark", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("IconHost", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("CheckGlyph", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("SystemColors.Menu", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessGridContextMenu_ThemeResourceKeysExistInLightAndDarkThemes()
        {
            var themePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Themes", "FluentLight.xaml"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Themes", "FluentDark.xaml"),
            };

            foreach (var themePath in themePaths)
            {
                var theme = File.ReadAllText(themePath);

                Assert.Contains("x:Key=\"BodyFontSize\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"CardSurfaceBrush\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"BorderSubtleBrush\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"TextPrimaryBrush\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"TextSecondaryBrush\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"TextDisabledBrush\"", theme, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"QuietRowHoverBackgroundBrush\"", theme, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ProcessGridContextMenu_ContainsExpectedActionsAndSubmenus()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Header=\"{DynamicResource ProcessView_ApplyAffinity}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_ClearCpuSets}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_Rules}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_SaveCurrentRule}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_ApplyAffinitySaveRule}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_SetCpuPriority}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityBelowNormal}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityNormal}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityAboveNormal}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityHigh}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_RealtimeBlocked}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_SetMemoryPriority}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityVeryLow}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityLow}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_PriorityMedium}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_OpenLocation}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_CopyProcessInfo}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"{DynamicResource ProcessView_RefreshInfo}\"", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessToolbar_ExposesLockProcessListToggle()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("ProcessView_LockList", serialized, StringComparison.Ordinal);
            Assert.Contains("IsChecked=\"{Binding IsProcessListLocked}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ProcessView_LockListTooltip", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_SelectedCpuTilesUseSubtleMaskSelectionResources()
        {
            var masksViewPath = Path.Combine(
                GetRepositoryRoot(),
                "Views",
                "MasksView.xaml");
            var document = XDocument.Load(masksViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("MaskSelectedBackgroundBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("MaskSelectedBorderBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("BorderThickness\" Value=\"2\"", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("SoftSelectionBackgroundBrush", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void MasksView_SelectedMaskListItemUsesReadableFluentSelection()
        {
            var masksViewPath = Path.Combine(
                GetRepositoryRoot(),
                "Views",
                "MasksView.xaml");
            var document = XDocument.Load(masksViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("x:Key=\"MaskListItemStyle\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TargetType=\"{x:Type ListBoxItem}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ItemContainerStyle=\"{StaticResource MaskListItemStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("MaskSelectedListBackgroundBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("MaskSelectedBorderBrush", serialized, StringComparison.Ordinal);
            Assert.Contains("BorderThickness\" Value=\"2\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Foreground\" Value=\"{DynamicResource TextFillColorPrimaryBrush}\"", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("TextOnAccentFillColorPrimaryBrush", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("AccentFillColorDefaultBrush", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_ContainsStartupMinimizedSuggestionOverlay()
        {
            var mainWindowPath = Path.Combine(GetRepositoryRoot(), "MainWindow.xaml");
            var document = XDocument.Load(mainWindowPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("StartupMinimizedSuggestionOverlay", serialized, StringComparison.Ordinal);
            Assert.Contains("MainWindow_StartupMinimizedSuggestionTitle", serialized, StringComparison.Ordinal);
            Assert.Contains("MainWindow_OpenSettings", serialized, StringComparison.Ordinal);
            Assert.Contains("MainWindow_DontShowAgain", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_QueuesStartupUpdateCheckOnceWithoutBlockingStartup()
        {
            var mainWindowBehaviorPath = Path.Combine(GetRepositoryRoot(), "MainWindow.Behaviors.partial.cs");
            var source = File.ReadAllText(mainWindowBehaviorPath);
            var updateCheckSection = source[
                source.IndexOf("private void QueueStartupUpdateCheck()", StringComparison.Ordinal)..
                source.IndexOf("private void UpdateLoadingStatus", StringComparison.Ordinal)];

            Assert.Contains("QueueStartupUpdateCheck();", source, StringComparison.Ordinal);
            Assert.Contains("Interlocked.Exchange(ref this.startupUpdateCheckStarted, 1)", updateCheckSection, StringComparison.Ordinal);
            Assert.Contains("TaskSafety.FireAndForget(this.CheckForUpdatesAtStartupAsync()", updateCheckSection, StringComparison.Ordinal);
            Assert.Contains("GetLatestVersionAsync(\"PrimeBuild-pc\", \"ThreadPilot\")", updateCheckSection, StringComparison.Ordinal);
            Assert.Contains("Startup update check ignored failure", updateCheckSection, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Windows.MessageBox.Show", updateCheckSection, StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyActionSidePanel_IsNotPersistentPrimaryUi()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Grid.Column=\"2\" Visibility=\"Collapsed\"", serialized, StringComparison.Ordinal);
            Assert.Contains("ProcessView_AdvancedAffinityPicker", serialized, StringComparison.Ordinal);
        }

        private static string GetRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot.csproj")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Repository root was not found.");
            }

            return directory.FullName;
        }
    }
}
