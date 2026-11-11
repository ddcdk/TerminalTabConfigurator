using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Services;
using TerminalTabConfigurator.ViewModels;

namespace TerminalTabConfigurator.Dialogs;

public partial class PublishWindow : Window
{
    private readonly TabLayout _layout;
    private readonly TerminalSettingsDocument _settings;
    private readonly string _actionId;
    private readonly bool _ready;

    public PublishWindow(LayoutEntry entry, TerminalInstallation installation, TerminalSettingsDocument currentSettings, bool elevated)
    {
        InitializeComponent();

        _layout = entry.Layout ?? throw new ArgumentException("La disposition n'a pas pu être relue.", nameof(entry));
        _settings = currentSettings;
        _actionId = _layout.ActionId ?? currentSettings.GenerateActionId(_layout.Name);

        HeaderText.Text = entry.IsPublished ? $"Mettre à jour « {_layout.Name} »" : $"Ajouter « {_layout.Name} » à Windows Terminal";
        TargetName.Text = installation.DisplayName;
        TargetPath.Text = installation.SettingsPath;

        KeysCheck.IsChecked = entry.Keys is not null;
        KeysBox.Text = entry.Keys ?? string.Empty;
        MenuCheck.IsChecked = !entry.IsPublished || entry.InNewTabMenu;
        // For an elevated layout the desktop shortcut is the one entry point that works from anywhere.
        ShortcutCheck.IsChecked = entry.HasShortcut || (elevated && !entry.IsPublished);
        ElevationBanner.Visibility = elevated ? Visibility.Visible : Visibility.Collapsed;
        var shortcutName = System.IO.Path.GetFileName(ShortcutService.GetPath(_layout.Name));
        ShortcutHint.Text = ShortcutService.FindWtExe() is null
            ? "wt.exe introuvable : le raccourci ne peut pas être créé."
            : elevated
                ? $"Crée {shortcutName}, lancé en administrateur (Windows demandera confirmation)."
                : $"Crée {shortcutName} qui ouvre la disposition dans la dernière fenêtre utilisée.";
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
            if (Keys is null) warning = "Saisissez une combinaison de touches, ou décochez l'option.";
            else if (_settings.FindKeyConflict(Keys, _layout.ActionId) is { } other)
                warning = $"Cette combinaison est déjà utilisée par « {other} ». Choisissez-en une autre.";
        }

        KeysWarning.Text = warning;
        KeysWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
        PublishButton.IsEnabled = warning is null;
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

        if (MenuCheck.IsChecked == true)
            preview["newTabMenu"] = new JsonArray(new JsonObject { ["type"] = "action", ["id"] = _actionId });

        return TerminalSettingsDocument.Format(preview);
    }

    private void OnPublish(object sender, RoutedEventArgs e)
    {
        Refresh();
        if (!PublishButton.IsEnabled) return;

        Result = new PublishRequest(
            new PublishOptions(Keys, MenuCheck.IsChecked == true),
            ShortcutCheck.IsChecked == true,
            BackupCheck.IsChecked == true);
        DialogResult = true;
    }
}
