namespace ThreadPilot.Services
{
    using System;

    public interface IThemeService
    {
        bool IsDarkTheme { get; }

        void ApplyTheme(bool useDarkTheme);

        bool GetSystemUsesDarkTheme();
    }
}
