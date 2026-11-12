// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Services;
using TerminalTabConfigurator.ViewModels;

namespace TerminalTabConfigurator.Dialogs;

public partial class PublishWindow : Window
{
    private readonly TabLayout _layout;
    private readonly TerminalSettingsDocument _settings;
    private readonly string _actionId;
    private readonly bool _profileIsDefault;
    private readonly bool _ready;

    public PublishWindow(LayoutEntry entry, TerminalInstallation installation, TerminalSettingsDocument currentSettings, bool elevated)
    {
        InitializeComponent();

        _layout = entry.Layout ?? throw new ArgumentException(Loc.T("LayoutUnreadable"), nameof(entry));
        _settings = currentSettings;
        _actionId = _layout.ActionId ?? currentSettings.GenerateActionId(_layout.Name);

        HeaderText.Text = Loc.F(entry.IsPublished ? "UpdateHeader" : "AddHeader", _layout.Name);
        TargetName.Text = Loc.InstallationName(installation);
        TargetPath.Text = installation.SettingsPath;

        KeysCheck.IsChecked = entry.Keys is not null;
        KeysBox.Text = entry.Keys ?? string.Empty;
        MenuCheck.IsChecked = !entry.IsPublished || entry.InNewTabMenu;
        ProfileCheck.IsChecked = entry.HasProfile;
        _profileIsDefault = entry.HasProfile && currentSettings.IsLayoutProfileDefault(_actionId);
        // For an elevated layout the desktop shortcut is the one entry point that works from anywhere.
        ShortcutCheck.IsChecked = entry.HasShortcut || (elevated && !entry.IsPublished);
        ElevationBanner.Visibility = elevated ? Visibility.Visible : Visibility.Collapsed;
        var shortcutName = System.IO.Path.GetFileName(ShortcutService.GetPath(_layout.Name));
        ShortcutHint.Text = ShortcutService.FindWtExe() is null
            ? Loc.T("WtNotFoundShortcut")
            : Loc.F(elevated ? "ShortcutElevated" : "ShortcutNormal", shortcutName);
        if (ShortcutService.FindWtExe() is null)
        {
            ShortcutCheck.IsChecked = false;
            ShortcutCheck.IsEnabled = false;
        }

        _ready = true;
        Refresh();
    }

    public PublishRequest? Result { get; private set; }

    private string? Keys => KeysCheck.IsChecked == true && KeysBox.Text.Trim() is { Length: > 0 } k ? k : null;

    private void OnOptionChanged(object sender, RoutedEventArgs e) => Refresh();

    private void OnKeysChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (!_ready) return;

        string? warning = null;
        if (KeysCheck.IsChecked == true)
        {
            if (Keys is null) warning = Loc.T("EnterKeys");
            else if (_settings.FindKeyConflict(Keys, _layout.ActionId) is { } other)
                warning = Loc.F("KeysConflict", other);
        }

        KeysWarning.Text = warning;
        KeysWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;

        // The default profile must not disappear from under Windows Terminal.
        var profileWarning = _profileIsDefault && ProfileCheck.IsChecked != true ? Loc.T("ProfileIsDefault") : null;
        ProfileWarning.Text = profileWarning;
        ProfileWarning.Visibility = profileWarning is null ? Visibility.Collapsed : Visibility.Visible;

        MenuHint.Text = Loc.T(ProfileCheck.IsChecked == true ? "NewTabMenuHintProfile" : "NewTabMenuHint");
        ProfileHint.Text = Loc.F(MenuCheck.IsChecked == true ? "ProfileHintMenu" : "ProfileHintHidden", _layout.Name);

        PublishButton.IsEnabled = warning is null && profileWarning is null;
        JsonPreview.Text = BuildPreview();
    }

    private string BuildPreview()
    {
        var action = new JsonObject { ["id"] = _actionId, ["name"] = _layout.Name };
        if (_layout.Icon is not null) action["icon"] = _layout.Icon;
        action["command"] = JsonActionBuilder.BuildCommand(_layout);
        var preview = new JsonObject { ["actions"] = new JsonArray(action) };

        if (Keys is { } keys)
            preview["keybindings"] = new JsonArray(new JsonObject { ["id"] = _actionId, ["keys"] = keys });

        var menu = MenuCheck.IsChecked == true;
        if (ProfileCheck.IsChecked == true)
        {
            var profile = TerminalSettingsDocument.BuildLayoutProfile(_layout, _actionId, hidden: !menu);
            preview["profiles"] = new JsonObject { ["list"] = new JsonArray(profile) };
            if (menu)
                preview["newTabMenu"] = new JsonArray(new JsonObject { ["type"] = "profile", ["profile"] = profile["guid"]!.DeepClone() });
        }
        else if (menu)
        {
            preview["newTabMenu"] = new JsonArray(new JsonObject { ["type"] = "action", ["id"] = _actionId });
        }

        return TerminalSettingsDocument.Format(preview);
    }

    private void OnPublish(object sender, RoutedEventArgs e)
    {
        Refresh();
        if (!PublishButton.IsEnabled) return;

        Result = new PublishRequest(
            new PublishOptions(Keys, MenuCheck.IsChecked == true, ProfileCheck.IsChecked == true),
            ShortcutCheck.IsChecked == true,
            BackupCheck.IsChecked == true);
        DialogResult = true;
    }
}
