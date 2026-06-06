namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    public sealed class LocalizationServiceTests
    {
        [Fact]
        public void Constructor_DefaultsToEnglish()
        {
            var service = CreateService();

            Assert.Equal("en-US", service.CurrentLanguage);
        }

        [Fact]
        public void ApplyLanguage_AppliesChinese_WhenSupported()
        {
            var service = CreateService();

            service.ApplyLanguage("zh-CN");

            Assert.Equal("zh-CN", service.CurrentLanguage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("fr-FR")]
        [InlineData("zh")]
        public void ApplyLanguage_FallsBackToEnglish_WhenLanguageIsInvalid(string? language)
        {
            var service = CreateService();
            service.ApplyLanguage("zh-CN");

            service.ApplyLanguage(language);

            Assert.Equal("en-US", service.CurrentLanguage);
        }

        [Fact]
        public void GetString_UsesEnglishFallback_WhenActiveLanguageMissesKey()
        {
            var service = CreateService(
                new Dictionary<string, string>
                {
                    ["Shared_Key"] = "English fallback",
                },
                new Dictionary<string, string>());
            service.ApplyLanguage("zh-CN");

            var result = service.GetString("Shared_Key");

            Assert.Equal("English fallback", result);
        }

        [Fact]
        public void GetString_ReturnsKey_WhenNoTranslationExists()
        {
            var service = CreateService();

            var result = service.GetString("Missing_Key");

            Assert.Equal("Missing_Key", result);
        }

        [Fact]
        public void LocaleFiles_DefineEnglishDefaultAndOptionalChineseLanguageLabels()
        {
            var root = FindRepositoryRoot();
            var english = File.ReadAllText(Path.Combine(root, "Locales", "en-US.xaml"));
            var chinese = File.ReadAllText(Path.Combine(root, "Locales", "zh-CN.xaml"));
            var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));

            Assert.Contains("Source=\"Locales/en-US.xaml\"", appXaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Source=\"Locales/zh-CN.xaml\"", appXaml, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageEnUs\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageZhCn\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageEnUs\"", chinese, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageZhCn\"", chinese, StringComparison.Ordinal);
        }

        private static LocalizationService CreateService(
            IReadOnlyDictionary<string, string>? englishStrings = null,
            IReadOnlyDictionary<string, string>? chineseStrings = null)
        {
            return new LocalizationService(
                NullLogger<LocalizationService>.Instance,
                englishStrings,
                chineseStrings);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ThreadPilot_1.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
