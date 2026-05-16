namespace ThreadPilot.Core.Tests
{
    using System.Reflection;
    using System.Windows;
    using ThreadPilot.Services;

    public sealed class ThemeDictionaryPolicyTests
    {
        [Theory]
        [InlineData("Themes/FluentDark.xaml")]
        [InlineData("Themes/FluentLight.xaml")]
        [InlineData("/ThreadPilot;component/Themes/FluentDark.xaml")]
        [InlineData("pack://application:,,,/ThreadPilot;component/Themes/FluentLight.xaml")]
        public void IsThreadPilotThemeDictionary_RecognizesAppThemeDictionaries(string source)
        {
            Assert.True(ThemeDictionaryPolicy.IsThreadPilotThemeDictionary(source));
        }

        [Fact]
        public void GetInsertionIndex_AppendsThemeDictionaryToPreserveAppResourcePrecedence()
        {
            Assert.Equal(3, ThemeDictionaryPolicy.GetInsertionIndex(3));
        }

        [Fact]
        public void ReplaceThreadPilotThemeDictionary_RemovesOldThemeDictionariesAndAppendsRequestedTheme()
        {
            var lightThemeUri = new Uri("Themes/FluentLight.xaml", UriKind.Relative);
            var darkThemeUri = new Uri("/ThreadPilot;component/Themes/FluentDark.xaml", UriKind.Relative);
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(new ResourceDictionary());
            resources.MergedDictionaries.Add(CreateDictionaryWithSource(lightThemeUri));
            resources.MergedDictionaries.Add(CreateDictionaryWithSource(darkThemeUri));

            var activeDictionary = ThemeDictionaryPolicy.ReplaceThreadPilotThemeDictionary(
                resources,
                lightThemeUri,
                CreateDictionaryWithSource);

            Assert.Same(activeDictionary, resources.MergedDictionaries[^1]);
            Assert.Equal(lightThemeUri.OriginalString, activeDictionary.Source.OriginalString);
            Assert.Single(
                resources.MergedDictionaries,
                dictionary => ThemeDictionaryPolicy.IsThreadPilotThemeDictionary(dictionary.Source?.OriginalString));
        }

        [Theory]
        [InlineData("Themes/FluentDark.xaml")]
        [InlineData("Themes/FluentLight.xaml")]
        public void ThemeDictionaries_DefineSharedVisualResourceKeys(string themePath)
        {
            var themeText = File.ReadAllText(GetRepositoryFilePath(themePath));

            Assert.Contains("StandardCardCornerRadius", themeText, StringComparison.Ordinal);
            Assert.Contains("StandardCardPadding", themeText, StringComparison.Ordinal);
            Assert.Contains("StandardCardStyle", themeText, StringComparison.Ordinal);
            Assert.Contains("PageTitleTextStyle", themeText, StringComparison.Ordinal);
            Assert.Contains("PageSubtitleTextStyle", themeText, StringComparison.Ordinal);
            Assert.Contains("SectionTitleTextStyle", themeText, StringComparison.Ordinal);
            Assert.Contains("QuietRowBackgroundBrush", themeText, StringComparison.Ordinal);
            Assert.Contains("StatusPillBackgroundBrush", themeText, StringComparison.Ordinal);
        }

        private static ResourceDictionary CreateDictionaryWithSource(Uri source)
        {
            var dictionary = new ResourceDictionary();
            var sourceField = typeof(ResourceDictionary).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sourceField == null)
            {
                throw new InvalidOperationException("ResourceDictionary source field was not found.");
            }

            sourceField.SetValue(dictionary, source);
            return dictionary;
        }

        private static string GetRepositoryFilePath(string relativePath)
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

            return Path.Combine(directory.FullName, relativePath);
        }
    }
}
