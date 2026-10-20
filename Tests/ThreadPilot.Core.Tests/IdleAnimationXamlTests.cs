namespace ThreadPilot.Core.Tests
{
    using System.Xml.Linq;

    public sealed class IdleAnimationXamlTests
    {
        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        // A hard-coded indeterminate progress control animates forever, even collapsed: WPF then renders
        // every frame (measured: ~25% of a core with the window open, ~2% in the tray).
        [Fact]
        public void NoProgressControl_IsIndeterminateForever()
        {
            var xamlFiles = Directory.GetFiles(Path.Combine(RepoRoot, "Views"), "*.xaml")
                .Append(Path.Combine(RepoRoot, "MainWindow.xaml"));

            var offenders = xamlFiles
                .SelectMany(file => XDocument.Load(file).Descendants()
                    .Where(element => element.Name.LocalName is "ProgressRing" or "ProgressBar")
                    .Where(element => string.Equals(
                        (string?)element.Attribute("IsIndeterminate"), "True", StringComparison.OrdinalIgnoreCase))
                    .Select(_ => Path.GetFileName(file)))
                .ToList();

            Assert.Empty(offenders);
        }
    }
}
