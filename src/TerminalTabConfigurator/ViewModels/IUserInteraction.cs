// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Core.Templates;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Mvvm;

namespace TerminalTabConfigurator.ViewModels;

/// <summary>A choice of the profile combo boxes. A null label stands for "Default profile".</summary>
public sealed class ProfileOption(string? label, string reference, bool hidden = false) : ObservableObject
{
    public string Display =>
        label is null ? Loc.T("DefaultProfile") : hidden ? $"{label} ({Loc.T("Hidden")})" : label;

    public string Reference { get; } = reference;

    public void RefreshDisplay() => Raise(nameof(Display));
}

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

    /// <summary>Shows the profiles that open a layout; <paramref name="import"/> is called with the one picked.</summary>
    void ShowImportMenu(IReadOnlyList<TerminalProfile> profiles, Action<TerminalProfile> import);

    PublishRequest? AskPublish(LayoutEntry entry, TerminalInstallation installation, TerminalSettingsDocument currentSettings, bool elevated);

    void CopyToClipboard(string text);
}
