namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using Microsoft.Extensions.Logging;

    public class LocalizationService : ILocalizationService
    {
        public const string DefaultLanguage = "en-US";
        public const string SimplifiedChineseLanguage = "zh-CN";

        private const string EnUsDictionaryPath = "Locales/en-US.xaml";
        private const string ZhCnDictionaryPath = "Locales/zh-CN.xaml";

        private readonly ILogger<LocalizationService> logger;
        private readonly IReadOnlyDictionary<string, string>? englishStrings;
        private readonly IReadOnlyDictionary<string, string>? chineseStrings;
        private ResourceDictionary? activeLocaleDictionary;
        private ResourceDictionary? englishFallbackDictionary;
        private Uri? activeLocaleUri;

        public string CurrentLanguage { get; private set; } = DefaultLanguage;

        public event EventHandler<string>? LanguageChanged;

        public LocalizationService(ILogger<LocalizationService> logger)
            : this(logger, englishStrings: null, chineseStrings: null)
        {
        }

        public LocalizationService(
            ILogger<LocalizationService> logger,
            IReadOnlyDictionary<string, string>? englishStrings,
            IReadOnlyDictionary<string, string>? chineseStrings)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.englishStrings = englishStrings;
            this.chineseStrings = chineseStrings;
        }

        public static string NormalizeLanguage(string? language)
        {
            if (string.Equals(language, SimplifiedChineseLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return SimplifiedChineseLanguage;
            }

            return DefaultLanguage;
        }

        public void ApplyLanguage(string? language)
        {
            var normalizedLanguage = NormalizeLanguage(language);
            var targetUri = new Uri(GetDictionaryPath(normalizedLanguage), UriKind.Relative);

            this.CurrentLanguage = normalizedLanguage;

            var appResources = System.Windows.Application.Current?.Resources;
            if (appResources == null)
            {
                this.activeLocaleUri = targetUri;
                this.LanguageChanged?.Invoke(this, normalizedLanguage);
                return;
            }

            try
            {
                this.ApplyLanguageDictionary(appResources, targetUri);
                this.activeLocaleUri = targetUri;
                this.logger.LogInformation("Applied display language {Language}", normalizedLanguage);
                this.LanguageChanged?.Invoke(this, normalizedLanguage);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to apply language {Language}", normalizedLanguage);
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
            return language == SimplifiedChineseLanguage ? ZhCnDictionaryPath : EnUsDictionaryPath;
        }

        private static bool IsLocaleDictionary(string? source)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                (source.EndsWith(EnUsDictionaryPath, StringComparison.OrdinalIgnoreCase) ||
                 source.EndsWith(ZhCnDictionaryPath, StringComparison.OrdinalIgnoreCase));
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
            var source = language == SimplifiedChineseLanguage ? this.chineseStrings : this.englishStrings;
            if (source != null && source.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text))
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
                    Source = new Uri(EnUsDictionaryPath, UriKind.Relative),
                };
                return TryGetString(this.englishFallbackDictionary, key, out value);
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to load English fallback localization dictionary");
                return false;
            }
        }
    }
}
