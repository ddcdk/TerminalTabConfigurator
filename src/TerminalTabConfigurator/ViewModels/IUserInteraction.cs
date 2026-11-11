using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Core.Templates;

namespace TerminalTabConfigurator.ViewModels;

public sealed record ProfileOption(string Display, string? Reference);

public sealed record NewLayoutRequest(LayoutTemplate Template, string Name, string? Folder, string? Profile);

public sealed record PublishRequest(PublishOptions Options, bool CreateShortcut, bool Backup);

/// <summary>Dialogs the view model needs from the window.</summary>
public interface IUserInteraction
{
    bool Confirm(string title, string message);

    void ShowError(string title, string message);

    string? PickFolder(string? initialFolder);

    string? PickIconFile(string? current);

    NewLayoutRequest? AskNewLayout(IReadOnlyList<ProfileOption> profiles, string? defaultProfile);

    PublishRequest? AskPublish(LayoutEntry entry, TerminalInstallation installation, TerminalSettingsDocument currentSettings, bool elevated);

    void CopyToClipboard(string text);
}
