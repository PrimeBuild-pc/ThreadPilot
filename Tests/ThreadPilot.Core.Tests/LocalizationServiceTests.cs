namespace ThreadPilot.Core.Tests
{
    using System.Globalization;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Xml.Linq;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    public sealed partial class LocalizationServiceTests
    {
        private static readonly string[] LocaleCodes =
        [
            "en-US",
            "zh-CN",
            "it-IT",
            "fr-FR",
            "de-DE",
            "es-ES",
            "ru-RU",
        ];

        [Fact]
        public void Constructor_DefaultsToEnglish()
        {
            var service = CreateService();

            Assert.Equal("en-US", service.CurrentLanguage);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("zh-CN")]
        [InlineData("it-IT")]
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("ru-RU")]
        public void ApplyLanguage_AppliesLanguage_WhenSupported(string language)
        {
            var service = CreateService();

            service.ApplyLanguage(language);

            Assert.Equal(language, service.CurrentLanguage);
        }

        [Theory]
        [InlineData("EN-us", "en-US")]
        [InlineData("ZH-cn", "zh-CN")]
        [InlineData("IT-it", "it-IT")]
        [InlineData("FR-fr", "fr-FR")]
        [InlineData("DE-de", "de-DE")]
        [InlineData("ES-es", "es-ES")]
        [InlineData("RU-ru", "ru-RU")]
        public void NormalizeLanguage_ReturnsCanonicalSupportedCode(string input, string expected)
        {
            Assert.Equal(expected, LocalizationService.NormalizeLanguage(input));
        }

        [Theory]
        [InlineData("en-GB", "en-US")]
        [InlineData("it-CH", "it-IT")]
        [InlineData("fr-CA", "fr-FR")]
        [InlineData("de-AT", "de-DE")]
        [InlineData("es-MX", "es-ES")]
        [InlineData("ru-KZ", "ru-RU")]
        [InlineData("zh-CN", "zh-CN")]
        [InlineData("zh-SG", "zh-CN")]
        [InlineData("zh-Hans", "zh-CN")]
        [InlineData("zh-TW", "en-US")]
        [InlineData("zh-Hant", "en-US")]
        [InlineData("pt-BR", "en-US")]
        public void ResolveSystemLanguage_MapsSupportedLanguageFamilies(string cultureName, string expected)
        {
            Assert.Equal(expected, LocalizationService.ResolveSystemLanguage(new CultureInfo(cultureName)));
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
        [InlineData("pt-BR")]
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
        public void LocaleFiles_DefineEnglishDefaultAndAllLanguageLabels()
        {
            var root = FindRepositoryRoot();
            var english = File.ReadAllText(Path.Combine(root, "Locales", "en-US.xaml"));
            var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));

            Assert.Contains("Source=\"Locales/en-US.xaml\"", appXaml, StringComparison.Ordinal);
            foreach (var localeCode in LocaleCodes.Where(code => code != "en-US"))
            {
                Assert.DoesNotContain($"Source=\"Locales/{localeCode}.xaml\"", appXaml, StringComparison.Ordinal);
            }

            Assert.Contains("x:Key=\"SettingsView_LanguageEnUs\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageZhCn\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageItIt\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageFrFr\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageDeDe\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageEsEs\"", english, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"SettingsView_LanguageRuRu\"", english, StringComparison.Ordinal);
        }

        [Fact]
        public void LocaleFiles_DefineTheSameResourceKeys()
        {
            var root = FindRepositoryRoot();
            var english = ReadLocaleKeys(Path.Combine(root, "Locales", "en-US.xaml"));
            foreach (var localeCode in LocaleCodes.Where(code => code != "en-US"))
            {
                var localized = ReadLocaleKeys(Path.Combine(root, "Locales", $"{localeCode}.xaml"));
                Assert.Empty(english.Except(localized).Order(StringComparer.Ordinal));
                Assert.Empty(localized.Except(english).Order(StringComparer.Ordinal));
            }
        }

        [Fact]
        public void LocaleFiles_DefineUniqueNonEmptyValuesAndMatchingFormatPlaceholders()
        {
            var root = FindRepositoryRoot();
            var english = ReadLocaleEntries(Path.Combine(root, "Locales", "en-US.xaml"));

            foreach (var localeCode in LocaleCodes)
            {
                var path = Path.Combine(root, "Locales", $"{localeCode}.xaml");
                var matches = LocaleStringRegex().Matches(File.ReadAllText(path));
                var localized = ReadLocaleEntries(path);

                Assert.Equal(matches.Count, localized.Count);
                Assert.DoesNotContain(localized, entry => string.IsNullOrWhiteSpace(entry.Value));
                foreach (var key in english.Keys)
                {
                    Assert.Equal(ReadFormatPlaceholders(english[key]), ReadFormatPlaceholders(localized[key]));
                }
            }
        }

        [Fact]
        public void LocaleFiles_AreWellFormedResourceDictionaries()
        {
            var root = FindRepositoryRoot();
            foreach (var localeCode in LocaleCodes)
            {
                var document = XDocument.Load(Path.Combine(root, "Locales", $"{localeCode}.xaml"));

                Assert.Equal("ResourceDictionary", document.Root?.Name.LocalName);
            }
        }

        [Fact]
        public void SettingsView_LanguageTagsMatchSupportedLanguageRegistry()
        {
            var root = FindRepositoryRoot();
            var settingsView = File.ReadAllText(Path.Combine(root, "Views", "SettingsView.xaml"));
            var tags = Regex.Matches(settingsView, "Tag=\"(?<code>[a-z]{2}-[A-Z]{2})\"", RegexOptions.CultureInvariant)
                .Select(match => match.Groups["code"].Value)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(
                LocalizationService.SupportedLanguages.Order(StringComparer.Ordinal),
                tags.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void ImportantViews_DoNotUseHardcodedEnglishUiText()
        {
            var root = FindRepositoryRoot();
            var viewPaths = new[]
            {
                "MainWindow.xaml",
                Path.Combine("Views", "ProcessView.xaml"),
                Path.Combine("Views", "MasksView.xaml"),
                Path.Combine("Views", "PowerPlanView.xaml"),
                Path.Combine("Views", "ProcessPowerPlanAssociationView.xaml"),
                Path.Combine("Views", "PerformanceView.xaml"),
                Path.Combine("Views", "LogViewerView.xaml"),
                Path.Combine("Views", "SystemTweaksView.xaml"),
                Path.Combine("Views", "SettingsView.xaml"),
                Path.Combine("Views", "SettingsWindow.xaml"),
            };

            var failures = new List<string>();
            foreach (var relativePath in viewPaths)
            {
                var fullPath = Path.Combine(root, relativePath);
                var xaml = File.ReadAllText(fullPath);
                foreach (Match match in HardcodedUiAttributeRegex().Matches(xaml))
                {
                    var attribute = match.Groups["attribute"].Value;
                    var value = match.Groups["value"].Value;
                    if (IsAllowedHardcodedUiValue(attribute, value))
                    {
                        continue;
                    }

                    failures.Add($"{relativePath}: {attribute}=\"{value}\"");
                }
            }

            Assert.Empty(failures);
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

        private static SortedSet<string> ReadLocaleKeys(string path)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var xaml = File.ReadAllText(path);
            foreach (Match match in Regex.Matches(xaml, "x:Key=\"(?<key>[^\"]+)\"", RegexOptions.CultureInvariant))
            {
                keys.Add(match.Groups["key"].Value);
            }

            return keys;
        }

        private static Dictionary<string, string> ReadLocaleEntries(string path)
        {
            return LocaleStringRegex().Matches(File.ReadAllText(path))
                .ToDictionary(
                    match => match.Groups["key"].Value,
                    match => match.Groups["value"].Value,
                    StringComparer.Ordinal);
        }

        private static string[] ReadFormatPlaceholders(string value)
        {
            return Regex.Matches(value, "\\{\\d+(?:[^}]*)\\}", RegexOptions.CultureInvariant)
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsAllowedHardcodedUiValue(string attribute, string value)
        {
            if (value.Contains('{', StringComparison.Ordinal) ||
                value.Contains("DynamicResource", StringComparison.Ordinal) ||
                value.Contains("StaticResource", StringComparison.Ordinal) ||
                value.Contains("Binding", StringComparison.Ordinal) ||
                value.Contains("x:Static", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(attribute, "Tag", StringComparison.Ordinal) ||
                string.Equals(attribute, "TargetPageTag", StringComparison.Ordinal) ||
                string.Equals(attribute, "Name", StringComparison.Ordinal) ||
                string.Equals(attribute, "x:Name", StringComparison.Ordinal) ||
                string.Equals(attribute, "SelectedValuePath", StringComparison.Ordinal) ||
                string.Equals(attribute, "DisplayMemberPath", StringComparison.Ordinal))
            {
                return true;
            }

            var trimmedValue = value.Trim();

            if (value.Contains("ThreadPilot", StringComparison.Ordinal) ||
                value.Contains("Segoe", StringComparison.Ordinal) ||
                value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".pow", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                trimmedValue is "CPU" or "PID" or "ID" or "MB" or "AGPLv3" or "Windows" or "WMI" or "HPET" or "SMT" or "CPU %")
            {
                return true;
            }

            return !Regex.IsMatch(value, "[A-Za-z]{3,}", RegexOptions.CultureInvariant);
        }

        [GeneratedRegex("(?<attribute>Text|Content|Header|Title|ToolTip|PlaceholderText|AutomationProperties\\.Name|AutomationProperties\\.HelpText)=\"(?<value>[^\"]*[A-Za-z][^\"]*)\"", RegexOptions.CultureInvariant)]
        private static partial Regex HardcodedUiAttributeRegex();

        [GeneratedRegex("<sys:String x:Key=\"(?<key>[^\"]+)\">(?<value>.*?)</sys:String>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
        private static partial Regex LocaleStringRegex();
    }
}
