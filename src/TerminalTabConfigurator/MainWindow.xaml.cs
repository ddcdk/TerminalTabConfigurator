// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Dialogs;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Services;
using TerminalTabConfigurator.ViewModels;

namespace TerminalTabConfigurator;

public partial class MainWindow : Window, IUserInteraction
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(this);
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges && !_viewModel.ConfirmDiscard()) e.Cancel = true;
        base.OnClosing(e);
    }

    // ---------- Preferences (language, theme) ----------

    private void OnPreferences(object sender, RoutedEventArgs e)
    {
        var prefs = App.Preferences;
        var menu = new ContextMenu
        {
            PlacementTarget = PreferencesButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };

        var language = new MenuItem { Header = Loc.T("Language") };
        AddChoice(language, Loc.T("LanguageSystem"), prefs.Language == Preferences.System, () => SetLanguage(Preferences.System));
        AddChoice(language, "Français", prefs.Language == Loc.French, () => SetLanguage(Loc.French));
        AddChoice(language, "English", prefs.Language == Loc.English, () => SetLanguage(Loc.English));

        var theme = new MenuItem { Header = Loc.T("Theme") };
        AddChoice(theme, Loc.T("ThemeSystem"), prefs.Theme == Preferences.System, () => SetTheme(Preferences.System));
        AddChoice(theme, Loc.T("ThemeDark"), prefs.Theme == ThemeService.Dark, () => SetTheme(ThemeService.Dark));
        AddChoice(theme, Loc.T("ThemeLight"), prefs.Theme == ThemeService.Light, () => SetTheme(ThemeService.Light));

        menu.Items.Add(language);
        menu.Items.Add(theme);
        menu.IsOpen = true;

        static void AddChoice(MenuItem parent, string header, bool isChecked, Action apply)
        {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
            item.Click += (_, _) => apply();
            parent.Items.Add(item);
        }
    }

    private static void SetLanguage(string preference)
    {
        App.Preferences.Language = preference;
        App.Preferences.Save();
        App.ApplyLanguage(preference);
    }

    private static void SetTheme(string preference)
    {
        App.Preferences.Theme = preference;
        App.Preferences.Save();
        ThemeService.Apply(preference);
    }

    // ---------- IUserInteraction ----------

    public bool Confirm(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? PickFolder(string? initialFolder) => FolderPicker.Pick(this, initialFolder);

    public string? PickIconFile(string? current)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("IconDialogTitle"),
            Filter = $"{Loc.T("ImageFiles")} (*.png;*.ico;*.jpg;*.jpeg;*.svg)|*.png;*.ico;*.jpg;*.jpeg;*.svg|{Loc.T("AllFiles")}|*.*",
        };
        if (IconValue.LocalFile(current) is { } file) dialog.InitialDirectory = Path.GetDirectoryName(file);
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public NewLayoutRequest? AskNewLayout(IReadOnlyList<ProfileOption> profiles, string? defaultProfile)
    {
        var dialog = new NewLayoutWindow(profiles, defaultProfile) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public void ShowImportMenu(IReadOnlyList<TerminalProfile> profiles, Action<TerminalProfile> import)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = ImportButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };

        if (profiles.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Loc.T("NoProfileToImport"), IsEnabled = false });
        }
        else
        {
            menu.Items.Add(new MenuItem { Header = Loc.T("ImportFromProfile"), IsEnabled = false });
            foreach (var profile in profiles)
            {
                var item = new MenuItem
                {
                    Header = profile.Hidden ? $"{profile.Name} ({Loc.T("Hidden")})" : profile.Name,
                    ToolTip = profile.Commandline,
                };
                item.Click += (_, _) => import(profile);
                menu.Items.Add(item);
            }
        }

        menu.IsOpen = true;
    }

    public PublishRequest? AskPublish(LayoutEntry entry, TerminalInstallation installation, TerminalSettingsDocument currentSettings, bool elevated)
    {
        var dialog = new PublishWindow(entry, installation, currentSettings, elevated) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            _viewModel.SetStatus(() => Loc.T("Copied"));
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            _viewModel.SetStatus(() => Loc.T("ClipboardBusy"));
        }
    }
}

internal static class FolderPicker
{
    public static string? Pick(Window owner, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("StartingDirectory") };
        var initial = Environment.ExpandEnvironmentVariables(initialFolder ?? string.Empty);
        if (initial.Length > 0 && Directory.Exists(initial)) dialog.InitialDirectory = initial;
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }
}
