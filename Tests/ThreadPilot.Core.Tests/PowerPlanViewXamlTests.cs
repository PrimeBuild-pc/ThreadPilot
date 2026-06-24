namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;

    public sealed class PowerPlanViewXamlTests
    {
        private static readonly string PowerPlanViewPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Views",
            "PowerPlanView.xaml");

        [Fact]
        public void PowerPlanItems_ExposeDeleteContextMenu()
        {
            var document = XDocument.Load(PowerPlanViewPath, LoadOptions.PreserveWhitespace);
            var deleteCommandBinding = document
                .Descendants()
                .SelectMany(element => element.Attributes())
                .SingleOrDefault(attribute => attribute.Value.Contains("DeletePowerPlanCommand", StringComparison.Ordinal));

            Assert.NotNull(deleteCommandBinding);
        }

        [Fact]
        public void HeaderInstructionText_WrapsToAvoidButtonOverlap()
        {
            var document = XDocument.Load(PowerPlanViewPath, LoadOptions.PreserveWhitespace);
            var instructionTextBlocks = document
                .Descendants()
                .Where(element => element.Name.LocalName == "TextBlock")
                .Where(element => element.Attributes().Any(attribute =>
                    attribute.Value.Contains("PowerPlanView_SelectActiveTip", StringComparison.Ordinal) ||
                    attribute.Value.Contains("PowerPlanView_LocalPlansTip", StringComparison.Ordinal)))
                .ToList();

            Assert.Equal(2, instructionTextBlocks.Count);
            Assert.All(instructionTextBlocks, textBlock =>
                Assert.Contains(textBlock.Attributes(), attribute =>
                    attribute.Name.LocalName == "TextWrapping" && attribute.Value == "Wrap"));
        }

        [Fact]
        public void ActivePowerPlanTemplate_ContainsActiveBadgeAndAccentBorder()
        {
            var document = XDocument.Load(PowerPlanViewPath, LoadOptions.PreserveWhitespace);
            var serialized = document.ToString(SaveOptions.DisableFormatting);

            Assert.Contains("PowerPlanView_Active", serialized, StringComparison.Ordinal);
            Assert.Contains("IsActive", serialized, StringComparison.Ordinal);
            Assert.Contains("Accent", serialized, StringComparison.Ordinal);
        }
    }
}
