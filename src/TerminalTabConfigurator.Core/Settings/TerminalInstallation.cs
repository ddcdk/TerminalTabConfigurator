namespace TerminalTabConfigurator.Core.Settings;

/// <summary>A Windows Terminal installation and the settings.json it reads.</summary>
public sealed record TerminalInstallation(string DisplayName, string SettingsPath)
{
    public override string ToString() => DisplayName;

    /// <summary>Lists the installations whose settings.json exists, stable release first.</summary>
    public static IReadOnlyList<TerminalInstallation> Detect()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string Packaged(string package) => Path.Combine(localAppData, "Packages", package, "LocalState", "settings.json");

        TerminalInstallation[] candidates =
        [
            new("Windows Terminal", Packaged("Microsoft.WindowsTerminal_8wekyb3d8bbwe")),
            new("Windows Terminal Preview", Packaged("Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe")),
            new("Windows Terminal Canary", Packaged("Microsoft.WindowsTerminalCanary_8wekyb3d8bbwe")),
            new("Windows Terminal (non empaqueté)", Path.Combine(localAppData, "Microsoft", "Windows Terminal", "settings.json")),
        ];

        return candidates.Where(c => File.Exists(c.SettingsPath)).ToList();
    }
}
