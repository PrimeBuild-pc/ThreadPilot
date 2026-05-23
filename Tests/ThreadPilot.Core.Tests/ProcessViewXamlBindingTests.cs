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

            Assert.Contains("Header=\"Apply Affinity\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Clear CPU Sets\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Rules\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Save Current Settings as Rule\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Apply Affinity and Save as Rule\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Set CPU Priority\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Below Normal\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Normal\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Above Normal\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"High\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Realtime (blocked)\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Set Memory Priority\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Very Low\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Low\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Medium\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Open Executable Location\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Copy Process Info\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Header=\"Refresh Process Info\"", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void ProcessToolbar_ExposesLockProcessListToggle()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Lock process list", serialized, StringComparison.Ordinal);
            Assert.Contains("IsChecked=\"{Binding IsProcessListLocked}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Pause process list refresh and sorting updates while you work with the current list.", serialized, StringComparison.Ordinal);
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
        public void MainWindow_ContainsStartupMinimizedSuggestionOverlay()
        {
            var mainWindowPath = Path.Combine(GetRepositoryRoot(), "MainWindow.xaml");
            var document = XDocument.Load(mainWindowPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("StartupMinimizedSuggestionOverlay", serialized, StringComparison.Ordinal);
            Assert.Contains("Startup minimized", serialized, StringComparison.Ordinal);
            Assert.Contains("Open Settings", serialized, StringComparison.Ordinal);
            Assert.Contains("Don't show again", serialized, StringComparison.Ordinal);
        }

        [Fact]
        public void LegacyActionSidePanel_IsNotPersistentPrimaryUi()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Grid.Column=\"2\" Visibility=\"Collapsed\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Advanced affinity picker", serialized, StringComparison.Ordinal);
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
