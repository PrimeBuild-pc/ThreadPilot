namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using Microsoft.Extensions.Logging;

    public class LocalizationService : ILocalizationService
    {
        public const string DefaultLanguage = "en-US";
        public const string SimplifiedChineseLanguage = "zh-CN";
        public const string ItalianLanguage = "it-IT";
        public const string FrenchLanguage = "fr-FR";
        public const string GermanLanguage = "de-DE";
        public const string SpanishLanguage = "es-ES";
        public const string RussianLanguage = "ru-RU";

        private static readonly Dictionary<string, string> LocaleDictionaryPaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DefaultLanguage] = "Locales/en-US.xaml",
                [SimplifiedChineseLanguage] = "Locales/zh-CN.xaml",
                [ItalianLanguage] = "Locales/it-IT.xaml",
                [FrenchLanguage] = "Locales/fr-FR.xaml",
                [GermanLanguage] = "Locales/de-DE.xaml",
                [SpanishLanguage] = "Locales/es-ES.xaml",
                [RussianLanguage] = "Locales/ru-RU.xaml",
            };

        private readonly ILogger<LocalizationService> logger;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? localizedStringOverrides;
        private ResourceDictionary? activeLocaleDictionary;
        private ResourceDictionary? englishFallbackDictionary;
        private Uri? activeLocaleUri;

        public string CurrentLanguage { get; private set; } = DefaultLanguage;

        public event EventHandler<string>? LanguageChanged;

        public LocalizationService(ILogger<LocalizationService> logger)
            : this(logger, localizedStringOverrides: null)
        {
        }

        public LocalizationService(
            ILogger<LocalizationService> logger,
            IReadOnlyDictionary<string, string>? englishStrings,
            IReadOnlyDictionary<string, string>? chineseStrings)
            : this(logger, CreateOverrides(englishStrings, chineseStrings))
        {
        }

        internal LocalizationService(
            ILogger<LocalizationService> logger,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? localizedStringOverrides)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.localizedStringOverrides = localizedStringOverrides;
        }

        internal static IReadOnlyCollection<string> SupportedLanguages => LocaleDictionaryPaths.Keys;

        public static string NormalizeLanguage(string? language)
        {
            return TryNormalizeSupportedLanguage(language, out var normalizedLanguage)
                ? normalizedLanguage
                : DefaultLanguage;
        }

        internal static bool TryNormalizeSupportedLanguage(string? language, out string normalizedLanguage)
        {
            if (!string.IsNullOrWhiteSpace(language))
            {
                foreach (var supportedLanguage in LocaleDictionaryPaths.Keys)
                {
                    if (string.Equals(language, supportedLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedLanguage = supportedLanguage;
                        return true;
                    }
                }
            }

            normalizedLanguage = DefaultLanguage;
            return false;
        }

        internal static string ResolveLanguagePreference(string? language, CultureInfo systemUiCulture)
        {
            if (TryNormalizeSupportedLanguage(language, out var normalizedLanguage))
            {
                return normalizedLanguage;
            }

            return ResolveSystemLanguage(systemUiCulture);
        }

        internal static string ResolveSystemLanguage(CultureInfo systemUiCulture)
        {
            ArgumentNullException.ThrowIfNull(systemUiCulture);

            var language = systemUiCulture.TwoLetterISOLanguageName;
            return language.ToLowerInvariant() switch
            {
                "en" => DefaultLanguage,
                "it" => ItalianLanguage,
                "fr" => FrenchLanguage,
                "de" => GermanLanguage,
                "es" => SpanishLanguage,
                "ru" => RussianLanguage,
                "zh" when IsSimplifiedChineseCulture(systemUiCulture) => SimplifiedChineseLanguage,
                _ => DefaultLanguage,
            };
        }

        public void ApplyLanguage(string? language)
        {
            var normalizedLanguage = NormalizeLanguage(language);
            var targetUri = new Uri(GetDictionaryPath(normalizedLanguage), UriKind.Relative);

            var appResources = System.Windows.Application.Current?.Resources;
            if (appResources == null)
            {
                this.CurrentLanguage = normalizedLanguage;
                this.activeLocaleUri = targetUri;
                this.LanguageChanged?.Invoke(this, normalizedLanguage);
                return;
            }

            try
            {
                this.ApplyLanguageDictionary(appResources, targetUri);
                this.CurrentLanguage = normalizedLanguage;
                this.activeLocaleUri = targetUri;
                this.logger.LogInformation("Applied display language {Language}", normalizedLanguage);
                this.LanguageChanged?.Invoke(this, normalizedLanguage);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to apply language {Language}", normalizedLanguage);
                this.ApplyEnglishFallback(appResources, normalizedLanguage);
            }
        }

        public string GetString(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (this.TryGetStringFromOverrides(this.CurrentLanguage, key, out var localized))
            {
                return localized;
            }

            if (this.TryGetStringFromApplicationResources(key, out localized))
            {
                return localized;
            }

            if (this.activeLocaleDictionary != null && TryGetString(this.activeLocaleDictionary, key, out localized))
            {
                return localized;
            }

            if (this.CurrentLanguage != DefaultLanguage &&
                this.TryGetStringFromOverrides(DefaultLanguage, key, out localized))
            {
                return localized;
            }

            if (this.CurrentLanguage != DefaultLanguage &&
                this.TryGetStringFromEnglishFallbackDictionary(key, out localized))
            {
                return localized;
            }

            return key;
        }

        private void ApplyLanguageDictionary(ResourceDictionary appResources, Uri targetUri)
        {
            ResourceDictionary? matchingDictionary = null;
            for (var i = appResources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dictionary = appResources.MergedDictionaries[i];
                var source = dictionary.Source?.OriginalString;
                if (IsLocaleDictionary(source))
                {
                    if (matchingDictionary == null &&
                        string.Equals(source, targetUri.OriginalString, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingDictionary = dictionary;
                        continue;
                    }

                    appResources.MergedDictionaries.RemoveAt(i);
                }
            }

            if (matchingDictionary != null)
            {
                appResources.MergedDictionaries.Remove(matchingDictionary);
                appResources.MergedDictionaries.Insert(0, matchingDictionary);
                this.activeLocaleDictionary = matchingDictionary;
            }
            else
            {
                var nextDictionary = new ResourceDictionary { Source = targetUri };
                appResources.MergedDictionaries.Insert(0, nextDictionary);
                this.activeLocaleDictionary = nextDictionary;
            }
        }

        private static string GetDictionaryPath(string language)
        {
            return LocaleDictionaryPaths.TryGetValue(language, out var path)
                ? path
                : LocaleDictionaryPaths[DefaultLanguage];
        }

        private static bool IsLocaleDictionary(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var path in LocaleDictionaryPaths.Values)
            {
                if (source.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetString(ResourceDictionary dictionary, string key, out string value)
        {
            if (dictionary.Contains(key) && dictionary[key] is string text && !string.IsNullOrEmpty(text))
            {
                value = text;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private bool TryGetStringFromOverrides(string language, string key, out string value)
        {
            if (this.localizedStringOverrides != null &&
                this.localizedStringOverrides.TryGetValue(language, out var source) &&
                source.TryGetValue(key, out var text) &&
                !string.IsNullOrEmpty(text))
            {
                value = text;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private bool TryGetStringFromApplicationResources(string key, out string value)
        {
            value = string.Empty;
            var app = System.Windows.Application.Current;
            if (app == null)
            {
                return false;
            }

            try
            {
                if (app.Dispatcher.CheckAccess())
                {
                    return TryGetApplicationResourceValue(app, key, out value);
                }

                var found = false;
                var dispatcherValue = string.Empty;
                app.Dispatcher.Invoke(() =>
                {
                    found = TryGetApplicationResourceValue(app, key, out dispatcherValue);
                });
                value = dispatcherValue;
                return found;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to read localized resource {Key}", key);
                return false;
            }
        }

        private static bool TryGetApplicationResourceValue(System.Windows.Application app, string key, out string value)
        {
            if (app.Resources.Contains(key) && app.Resources[key] is string text && !string.IsNullOrEmpty(text))
            {
                value = text;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private bool TryGetStringFromEnglishFallbackDictionary(string key, out string value)
        {
            value = string.Empty;
            try
            {
                this.englishFallbackDictionary ??= new ResourceDictionary
                {
                    Source = new Uri(LocaleDictionaryPaths[DefaultLanguage], UriKind.Relative),
                };
                return TryGetString(this.englishFallbackDictionary, key, out value);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to load English fallback localization dictionary");
                return false;
            }
        }

        private static bool IsSimplifiedChineseCulture(CultureInfo culture)
        {
            var name = culture.Name;
            return name.Contains("Hans", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("CHS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "zh-SG", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyEnglishFallback(ResourceDictionary appResources, string failedLanguage)
        {
            var fallbackUri = new Uri(GetDictionaryPath(DefaultLanguage), UriKind.Relative);
            try
            {
                if (!string.Equals(failedLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    this.ApplyLanguageDictionary(appResources, fallbackUri);
                }

                this.CurrentLanguage = DefaultLanguage;
                this.activeLocaleUri = fallbackUri;
                this.LanguageChanged?.Invoke(this, DefaultLanguage);
            }
            catch (Exception fallbackException)
            {
                this.CurrentLanguage = DefaultLanguage;
                this.activeLocaleUri = fallbackUri;
                this.logger.LogError(fallbackException, "Failed to apply English fallback language");
                this.LanguageChanged?.Invoke(this, DefaultLanguage);
            }
        }

        private static Dictionary<string, IReadOnlyDictionary<string, string>>? CreateOverrides(
            IReadOnlyDictionary<string, string>? englishStrings,
            IReadOnlyDictionary<string, string>? chineseStrings)
        {
            if (englishStrings == null && chineseStrings == null)
            {
                return null;
            }

            var overrides = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (englishStrings != null)
            {
                overrides[DefaultLanguage] = englishStrings;
            }

            if (chineseStrings != null)
            {
                overrides[SimplifiedChineseLanguage] = chineseStrings;
            }

            return overrides;
        }
    }
}
