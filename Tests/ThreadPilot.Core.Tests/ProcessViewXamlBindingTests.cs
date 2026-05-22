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
            Assert.Contains("Style=\"{StaticResource ProcessRowContextMenuStyle}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("BasedOn=\"{x:Null}\"", serialized, StringComparison.Ordinal);
            Assert.Contains("TargetType=\"MenuItem\"", serialized, StringComparison.Ordinal);
            Assert.Contains("FontWeight\" Value=\"Normal\"", serialized, StringComparison.Ordinal);
            Assert.Contains("FontSize\" Value=\"{DynamicResource BodyFontSize}\"", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight\" Value=\"{Binding", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight\" Value=\"{TemplateBinding", serialized, StringComparison.Ordinal);
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
        public void LegacyActionSidePanel_IsNotPersistentPrimaryUi()
        {
            var document = XDocument.Load(ProcessViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("Grid.Column=\"2\" Visibility=\"Collapsed\"", serialized, StringComparison.Ordinal);
            Assert.Contains("Advanced affinity picker", serialized, StringComparison.Ordinal);
        }
    }
}
