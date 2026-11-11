namespace TerminalTabConfigurator.Core.Settings;

public enum TerminalChannel { Stable, Preview, Canary, Unpackaged }

/// <summary>A Windows Terminal installation and the settings.json it reads.</summary>
public sealed record TerminalInstallation(TerminalChannel Channel, string DisplayName, string SettingsPath)
{
    public override string ToString() => DisplayName;

    /// <summary>Lists the installations whose settings.json exists, stable release first.</summary>
    public static IReadOnlyList<TerminalInstallation> Detect()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string Packaged(string package) => Path.Combine(localAppData, "Packages", package, "LocalState", "settings.json");

        TerminalInstallation[] candidates =
        [
            new(TerminalChannel.Stable, "Windows Terminal", Packaged("Microsoft.WindowsTerminal_8wekyb3d8bbwe")),
            new(TerminalChannel.Preview, "Windows Terminal Preview", Packaged("Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe")),
            new(TerminalChannel.Canary, "Windows Terminal Canary", Packaged("Microsoft.WindowsTerminalCanary_8wekyb3d8bbwe")),
            new(TerminalChannel.Unpackaged, "Windows Terminal (unpackaged)", Path.Combine(localAppData, "Microsoft", "Windows Terminal", "settings.json")),
        ];

        return candidates.Where(c => File.Exists(c.SettingsPath)).ToList();
    }
}
