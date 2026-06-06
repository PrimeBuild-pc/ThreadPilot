namespace ThreadPilot.Core.Tests
{
    using System.Reflection;
    using System.Windows;
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

        [Fact]
        public void ApplyLanguage_FiresLanguageChangedWithNormalizedLanguage()
        {
            var service = CreateService();
            var observedLanguages = new List<string>();
            service.LanguageChanged += (_, language) => observedLanguages.Add(language);

            service.ApplyLanguage("zh-cn");
            service.ApplyLanguage("unsupported");

            Assert.Equal(new[] { "zh-CN", "en-US" }, observedLanguages);
        }

        [Fact]
        public void ApplyLanguage_RemovesDuplicateAndOldLocaleDictionaries()
        {
            var resources = new ResourceDictionary();
            var nonLocaleDictionary = CreateDictionaryWithSource("Themes/FluentDark.xaml");
            var oldEnglishDictionary = CreateDictionaryWithSource("Locales/en-US.xaml");
            var duplicateChineseDictionary = CreateDictionaryWithSource("Locales/zh-CN.xaml");
            var matchingChineseDictionary = CreateDictionaryWithSource("Locales/zh-CN.xaml");
            resources.MergedDictionaries.Add(nonLocaleDictionary);
            resources.MergedDictionaries.Add(oldEnglishDictionary);
            resources.MergedDictionaries.Add(duplicateChineseDictionary);
            resources.MergedDictionaries.Add(matchingChineseDictionary);
            var service = CreateService();

            InvokeApplyLanguageDictionary(service, resources, new Uri("Locales/zh-CN.xaml", UriKind.Relative));

            Assert.Equal(2, resources.MergedDictionaries.Count);
            Assert.Same(matchingChineseDictionary, resources.MergedDictionaries[0]);
            Assert.Same(nonLocaleDictionary, resources.MergedDictionaries[1]);
            Assert.DoesNotContain(resources.MergedDictionaries, dictionary => ReferenceEquals(dictionary, oldEnglishDictionary));
            Assert.DoesNotContain(resources.MergedDictionaries, dictionary => ReferenceEquals(dictionary, duplicateChineseDictionary));
        }

        [Fact]
        public void GetString_UsesCurrentLanguageOverrideBeforeEnglishFallback()
        {
            var service = CreateService(
                new Dictionary<string, string>
                {
                    ["Shared_Key"] = "English",
                },
                new Dictionary<string, string>
                {
                    ["Shared_Key"] = "Chinese",
                });
            service.ApplyLanguage("zh-CN");

            var result = service.GetString("Shared_Key");

            Assert.Equal("Chinese", result);
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
        public void GetString_ReturnsEmpty_WhenKeyIsBlank()
        {
            var service = CreateService();

            Assert.Equal(string.Empty, service.GetString(string.Empty));
            Assert.Equal(string.Empty, service.GetString("   "));
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

        private static ResourceDictionary CreateDictionaryWithSource(string source)
        {
            var dictionary = new ResourceDictionary();
            var sourceField = typeof(ResourceDictionary).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sourceField == null)
            {
                throw new InvalidOperationException("ResourceDictionary source field was not found.");
            }

            sourceField.SetValue(dictionary, new Uri(source, UriKind.Relative));
            return dictionary;
        }

        private static void InvokeApplyLanguageDictionary(
            LocalizationService service,
            ResourceDictionary resources,
            Uri targetUri)
        {
            var method = typeof(LocalizationService).GetMethod(
                "ApplyLanguageDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("ApplyLanguageDictionary method was not found.");
            }

            method.Invoke(service, new object[] { resources, targetUri });
        }
    }
}
