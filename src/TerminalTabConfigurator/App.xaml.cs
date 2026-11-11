using System.Windows;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Services;

namespace TerminalTabConfigurator;

public partial class App : Application
{
    public static Preferences Preferences { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Preferences = Preferences.Load();
        ApplyLanguage(Preferences.Language);
        ThemeService.Apply(Preferences.Theme);

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    /// <summary>Applies "system", "fr" or "en".</summary>
    public static void ApplyLanguage(string preference) =>
        Loc.Instance.Language = preference is Loc.French or Loc.English ? preference : Loc.SystemLanguage;
}
