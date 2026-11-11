using System.Windows;
using Microsoft.Win32;

namespace TerminalTabConfigurator.Services;

/// <summary>Switches between the dark and light palettes (Themes/*.xaml) and the matching Fluent theme.</summary>
public static class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";

    private static string _preference = Preferences.System;
    private static bool _listening;

    /// <summary>"dark" or "light", as Windows apps are currently set.</summary>
    public static string SystemTheme
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is 0 ? Dark : Light;
        }
    }

    /// <summary>Applies "system", "dark" or "light".</summary>
    public static void Apply(string preference)
    {
        _preference = preference is Dark or Light ? preference : Preferences.System;
        var theme = _preference == Preferences.System ? SystemTheme : _preference;

        var app = Application.Current;
#pragma warning disable WPF0001
        var mode = theme == Light ? ThemeMode.Light : ThemeMode.Dark;
        if (app.ThemeMode != mode) app.ThemeMode = mode;
#pragma warning restore WPF0001

        // Copy the palette into the application resources: every brush is a DynamicResource, so the open windows follow.
        var palette = new ResourceDictionary { Source = new Uri($"pack://application:,,,/TerminalTabConfigurator;component/Themes/{(theme == Light ? "Light" : "Dark")}.xaml") };
        foreach (var key in palette.Keys) app.Resources[key] = palette[key];

        if (!_listening)
        {
            _listening = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_preference != Preferences.System || e.Category != UserPreferenceCategory.General) return;
        Application.Current?.Dispatcher.BeginInvoke(() => Apply(Preferences.System));
    }
}
