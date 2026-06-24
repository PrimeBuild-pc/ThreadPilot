namespace ThreadPilot.Services
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }

        event EventHandler<string>? LanguageChanged;

        void ApplyLanguage(string? language);

        string GetString(string key);
    }
}
