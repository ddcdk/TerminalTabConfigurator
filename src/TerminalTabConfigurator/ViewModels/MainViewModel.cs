// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Mvvm;
using TerminalTabConfigurator.Services;

namespace TerminalTabConfigurator.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IUserInteraction _ui;
    private TerminalInstallation? _installation;
    private LayoutEntry? _selectedEntry;
    private TabLayout? _watchedLayout;
    private PaneLeaf? _selectedPane;
    private string _commandPreview = string.Empty;
    private string _statusMessage = string.Empty;
    private string? _defaultProfile;
    private Func<string>? _status;

    public MainViewModel(IUserInteraction ui)
    {
        _ui = ui;

        NewLayoutCommand = new RelayCommand(NewLayout, () => _installation is not null);
        ImportCommand = new RelayCommand(ShowImportMenu, () => _installation is not null);
        ReloadCommand = new RelayCommand(() => Reload(confirm: true), () => _installation is not null);
        SplitVerticalCommand = new RelayCommand(() => Split(SplitOrientation.Vertical), () => SelectedPane is not null);
        SplitHorizontalCommand = new RelayCommand(() => Split(SplitOrientation.Horizontal), () => SelectedPane is not null);
        DeletePaneCommand = new RelayCommand(DeletePane, () => SelectedPane?.Parent is not null);
        EqualizeCommand = new RelayCommand(() => SelectedLayout?.Equalize(), () => SelectedLayout?.Root is SplitNode);
        TestCommand = new RelayCommand(Test, () => SelectedLayout is not null);
        PublishCommand = new RelayCommand(Publish, () => SelectedLayout is not null && _installation is not null);
        DeleteLayoutCommand = new RelayCommand(DeleteLayout, () => SelectedEntry is not null);
        CopyCommand = new RelayCommand(() => _ui.CopyToClipboard(CommandPreview), () => CommandPreview.Length > 0);
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => SelectedPane is not null);
        SetIconCommand = new RelayCommand(p => { if (SelectedLayout is not null) SelectedLayout.Icon = p as string; }, _ => SelectedLayout is not null);
        BrowseIconCommand = new RelayCommand(BrowseIcon, () => SelectedLayout is not null);
        SetTabColorCommand = new RelayCommand(p => { if (SelectedLayout is not null) SelectedLayout.TabColor = p as string; }, _ => SelectedLayout is not null);

        foreach (var installation in TerminalInstallation.Detect()) Installations.Add(installation);
        SelectedInstallation = Installations.FirstOrDefault();
        if (_installation is null) SetStatus(() => Loc.T("NoSettingsFound"));

        Loc.LanguageChanged += (_, _) => RefreshTexts();
    }

    public ObservableCollection<TerminalInstallation> Installations { get; } = [];

    public ObservableCollection<ProfileOption> Profiles { get; } = [];

    public ObservableCollection<LayoutEntry> Layouts { get; } = [];

    public IReadOnlyList<string> TabColors { get; } = ["#C18FFF", "#60CDFF", "#F7B955", "#6CCB5F", "#FF8C8C", "#4FD1C5"];

    public IReadOnlyList<string> IconSuggestions { get; } = ["🚀", "🛠️", "🧪", "📦", "🌐", "🐳", "⚙️", "📊", "🔥", "🐍", "☁️", "🗄️"];

    public TerminalInstallation? SelectedInstallation
    {
        get => _installation;
        set
        {
            if (ReferenceEquals(value, _installation)) return;
            if (HasUnsavedChanges && !ConfirmDiscard())
            {
                Raise(); // puts the combo box back
                return;
            }

            _installation = value;
            Raise();
            Reload(confirm: false);
        }
    }

    public LayoutEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!Set(ref _selectedEntry, value)) return;
            Raise(nameof(SelectedLayout));
            Raise(nameof(IsEditable));
            WatchLayout(SelectedLayout);
            SelectedPane = SelectedLayout?.PrimaryPane;
        }
    }

    public TabLayout? SelectedLayout => _selectedEntry?.Layout;

    public bool IsEditable => SelectedLayout is not null;

    public PaneLeaf? SelectedPane
    {
        get => _selectedPane;
        set
        {
            if (!Set(ref _selectedPane, value)) return;
            Raise(nameof(SelectedPaneShare));
            Raise(nameof(SelectedPaneIsElevated));
            Raise(nameof(CanResizeSelectedPane));
            RaisePrompt();
        }
    }

    public IReadOnlyList<PromptOption> PromptOptions { get; } =
    [
        new(PromptStyle.Default, "PromptDefault"),
        new(PromptStyle.FolderName, "PromptFolderName"),
        new(PromptStyle.TwoLines, "PromptTwoLines"),
        new(PromptStyle.Custom, "PromptCustom"),
    ];

    /// <summary>
    /// PowerShell prompt of the selected pane. Choosing one for a pane without a command line fills it
    /// with the PowerShell of its profile: the prompt is passed to that command line.
    /// </summary>
    public PromptStyle SelectedPanePrompt
    {
        get => _selectedPane?.Prompt ?? PromptStyle.Default;
        set
        {
            if (_selectedPane is not { } pane || pane.Prompt == value) return;
            if (value != PromptStyle.Default && TextValue.Clean(pane.Commandline) is null && ProfileShell(pane) is { } shell)
                pane.Commandline = shell;
            pane.Prompt = value;
            RaisePrompt();
        }
    }

    /// <summary>The prompt list is usable: the pane runs PowerShell, or a prompt must be cleared.</summary>
    public bool CanEditPrompt =>
        _selectedPane is { } pane &&
        (pane.Prompt != PromptStyle.Default || PowerShellPrompt.CanApply(TextValue.Clean(pane.Commandline) ?? ProfileShell(pane)));

    public bool IsCustomPrompt => _selectedPane?.Prompt == PromptStyle.Custom;

    /// <summary>Why the chosen prompt will not be applied, or null.</summary>
    public string? PromptWarning =>
        _selectedPane is { Prompt: not PromptStyle.Default } pane && !PowerShellPrompt.CanApply(TextValue.Clean(pane.Commandline))
            ? Loc.T(TextValue.Clean(pane.Commandline) is null ? "PromptNeedsCommandline" : "PromptNotApplied")
            : null;

    private string? ProfileShell(PaneLeaf pane) =>
        PowerShellPrompt.ProfileCommandline(ProfileCatalog.Profiles.FirstOrDefault(p => p.Matches(pane.Profile ?? _defaultProfile)));

    private void RaisePrompt()
    {
        Raise(nameof(SelectedPanePrompt));
        Raise(nameof(CanEditPrompt));
        Raise(nameof(IsCustomPrompt));
        Raise(nameof(PromptWarning));
    }

    public bool CanResizeSelectedPane => _selectedPane?.Parent is not null;

    /// <summary>Share (in %) of the selected pane inside its parent split.</summary>
    public double SelectedPaneShare
    {
        get
        {
            if (_selectedPane?.Parent is not { } parent) return 100;
            var ratio = ReferenceEquals(parent.First, _selectedPane) ? parent.Ratio : 1 - parent.Ratio;
            return Math.Round(ratio * 100);
        }
        set
        {
            if (_selectedPane?.Parent is not { } parent) return;
            var share = value / 100;
            parent.Ratio = ReferenceEquals(parent.First, _selectedPane) ? share : 1 - share;
        }
    }

    public LayoutElevation SelectedElevation =>
        SelectedLayout is null ? LayoutElevation.None : Elevation.Analyze(SelectedLayout, ProfileCatalog.Profiles, _defaultProfile);

    public bool SelectedPaneIsElevated =>
        _selectedPane is not null && Elevation.IsElevated(_selectedPane, ProfileCatalog.Profiles, _defaultProfile);

    /// <summary>Why the layout will not open as drawn from a normal Windows Terminal window, or null.</summary>
    public string? ElevationWarning => SelectedElevation switch
    {
        LayoutElevation.All => Loc.T("ElevationAll"),
        LayoutElevation.Mixed => Loc.T("ElevationMixed"),
        _ => null,
    };

    public string CommandPreview { get => _commandPreview; private set => Set(ref _commandPreview, value); }

    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }

    /// <summary>Shows a status message; it is rebuilt when the language changes.</summary>
    public void SetStatus(Func<string> message)
    {
        _status = message;
        StatusMessage = message();
    }

    public bool HasUnsavedChanges => Layouts.Any(e => e.IsDirty);

    public ICommand NewLayoutCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand SplitVerticalCommand { get; }
    public ICommand SplitHorizontalCommand { get; }
    public ICommand DeletePaneCommand { get; }
    public ICommand EqualizeCommand { get; }
    public ICommand TestCommand { get; }
    public ICommand PublishCommand { get; }
    public ICommand DeleteLayoutCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand SetTabColorCommand { get; }
    public ICommand SetIconCommand { get; }
    public ICommand BrowseIconCommand { get; }

    public bool ConfirmDiscard() =>
        _ui.Confirm(Loc.T("UnsavedTitle"), Loc.T("UnsavedMessage"));

    private void RefreshTexts()
    {
        foreach (var entry in Layouts) entry.RefreshTexts();
        foreach (var profile in Profiles) profile.RefreshDisplay();
        foreach (var option in PromptOptions) option.RefreshDisplay();
        Raise(nameof(PromptWarning));
        Raise(nameof(ElevationWarning));
        if (_status is not null) StatusMessage = _status();
    }

    // ---------- Loading ----------

    private void Reload(bool confirm)
    {
        if (confirm && HasUnsavedChanges && !ConfirmDiscard()) return;

        var previousId = SelectedEntry?.ActionId;
        SelectedEntry = null;
        Layouts.Clear();
        Profiles.Clear();
        ProfileCatalog.Profiles = [];
        if (_installation is null) return;

        TerminalSettingsDocument doc;
        try
        {
            doc = TerminalSettingsDocument.Load(_installation.SettingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            _ui.ShowError(Loc.T("ReadErrorTitle"), Loc.F("ReadErrorMessage", _installation.SettingsPath, ex.Message));
            return;
        }

        var profiles = doc.GetProfiles();
        ProfileCatalog.Profiles = profiles;
        Profiles.Add(new ProfileOption(null, string.Empty));
        // Profiles that open a layout (wt.exe) cannot run inside a pane: they are not offered.
        foreach (var p in profiles.Where(p => !p.IsLayout)) Profiles.Add(new ProfileOption(ProfileCatalog.Label(p), p.Reference, p.Hidden));

        _defaultProfile = doc.DefaultProfile;

        foreach (var published in doc.GetLayouts())
        {
            Layouts.Add(new LayoutEntry(published.Layout, published.Name, published.ActionId, published.Error)
            {
                IsPublished = true,
                Keys = published.Keys,
                InNewTabMenu = published.InNewTabMenu,
                HasShortcut = ShortcutService.Exists(published.Name),
                HasProfile = published.HasProfile,
                PublishedName = published.Name,
            });
        }

        SelectedEntry = Layouts.FirstOrDefault(e => e.ActionId == previousId) ?? Layouts.FirstOrDefault();
        var count = Layouts.Count;
        var installation = _installation;
        SetStatus(() => count == 0 ? Loc.T("NoLayoutsYet") : Loc.F("LayoutsRead", count, Loc.InstallationName(installation)));
    }

    private void WatchLayout(TabLayout? layout)
    {
        if (_watchedLayout is not null) _watchedLayout.Changed -= OnLayoutChanged;
        _watchedLayout = layout;
        if (layout is not null) layout.Changed += OnLayoutChanged;
        RefreshPreview();
    }

    private void OnLayoutChanged(object? sender, bool structural)
    {
        RefreshPreview();
        Raise(nameof(SelectedPaneIsElevated));
        Raise(nameof(SelectedPaneShare));
        Raise(nameof(CanResizeSelectedPane));
        RaisePrompt();
    }

    private void RefreshPreview()
    {
        CommandPreview = SelectedLayout is null ? string.Empty : WtCommandLineBuilder.BuildDisplay(SelectedLayout);
        Raise(nameof(SelectedElevation));
        Raise(nameof(ElevationWarning));
    }

    // ---------- Editing ----------

    private void NewLayout()
    {
        var request = _ui.AskNewLayout(Profiles, _defaultProfile);
        if (request is null) return;

        var layout = request.Template.Create(request.Name, request.Folder, request.Profile);
        var entry = new LayoutEntry(layout, request.Name) { IsDirty = true };
        Layouts.Add(entry);
        SelectedEntry = entry;
        SetStatus(() => Loc.T("LayoutCreated"));
    }

    private void ShowImportMenu()
    {
        if (_installation is null) return;

        // Read the file again: the profile may have been added since the last reload.
        IReadOnlyList<TerminalProfile> profiles;
        try
        {
            profiles = TerminalSettingsDocument.Load(_installation.SettingsPath).GetImportableProfiles();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            _ui.ShowError(Loc.T("ReadErrorTitle"), Loc.F("ReadErrorMessage", _installation.SettingsPath, ex.Message));
            return;
        }

        _ui.ShowImportMenu(profiles, ImportProfile);
    }

    private void ImportProfile(TerminalProfile profile)
    {
        TabLayout layout;
        try
        {
            layout = WtCommandLineParser.Parse(profile.Name, profile.Commandline ?? string.Empty);
        }
        catch (Exception ex) when (ex is LayoutParseException or InvalidOperationException or FormatException)
        {
            var reason = ex is LayoutParseException parse ? Loc.ParseError(parse) : ex.Message;
            _ui.ShowError(Loc.T("ImportFailedTitle"), Loc.F("ImportFailedMessage", profile.Name, reason));
            return;
        }

        layout.Icon = profile.Icon;
        var entry = new LayoutEntry(layout, layout.Name) { IsDirty = true };
        Layouts.Add(entry);
        SelectedEntry = entry;
        var name = profile.Name;
        SetStatus(() => Loc.F("ProfileImported", name));
    }

    private void Split(SplitOrientation orientation)
    {
        if (SelectedLayout is null || SelectedPane is null) return;
        SelectedPane = SelectedLayout.Split(SelectedPane, orientation);
    }

    private void DeletePane()
    {
        if (SelectedLayout is null || SelectedPane is null) return;
        var next = SelectedLayout.Remove(SelectedPane);
        if (next is not null) SelectedPane = next;
    }

    private void BrowseFolder()
    {
        if (SelectedPane is null) return;
        var folder = _ui.PickFolder(SelectedPane.StartingDirectory);
        if (folder is not null) SelectedPane.StartingDirectory = folder;
    }

    private void BrowseIcon()
    {
        if (SelectedLayout is null) return;
        var file = _ui.PickIconFile(SelectedLayout.Icon);
        if (file is not null) SelectedLayout.Icon = file;
    }

    // ---------- Windows Terminal ----------

    private void Test()
    {
        if (SelectedLayout is null) return;
        var elevation = SelectedElevation;
        if (elevation == LayoutElevation.Mixed)
        {
            _ui.ShowError(Loc.T("TestFailedTitle"), ElevationWarning!);
            return;
        }

        try
        {
            // An elevated layout must start in an elevated window: launching it from a normal one
            // goes through elevate-shim, which opens a separate window and loses the splits.
            Process.Start(new ProcessStartInfo("wt.exe", WtCommandLineBuilder.BuildArguments(SelectedLayout, WtCommandLineBuilder.NewWindow))
            {
                UseShellExecute = true,
                Verb = elevation == LayoutElevation.All ? "runas" : string.Empty,
            });
            var key = elevation == LayoutElevation.All ? "TestOpenedElevated" : "TestOpened";
            SetStatus(() => Loc.T(key));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            SetStatus(() => Loc.T("TestCancelled"));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _ui.ShowError(Loc.T("TestFailedTitle"), Loc.F("CannotLaunchWt", ex.Message));
        }
    }

    private void Publish()
    {
        if (SelectedEntry is not { Layout: { } layout } entry || _installation is null) return;
        layout.TrimText();
        if (layout.Name.Length == 0)
        {
            _ui.ShowError(Loc.T("MissingNameTitle"), Loc.T("MissingNameMessage"));
            return;
        }

        var elevation = SelectedElevation;
        if (elevation == LayoutElevation.Mixed)
        {
            _ui.ShowError(Loc.T("PublishFailedTitle"), ElevationWarning!);
            return;
        }

        try
        {
            var current = TerminalSettingsDocument.Load(_installation.SettingsPath);
            var request = _ui.AskPublish(entry, _installation, current, elevation == LayoutElevation.All);
            if (request is null) return;

            // Re-read right before writing: Windows Terminal may have saved the file meanwhile.
            var doc = TerminalSettingsDocument.Load(_installation.SettingsPath);
            doc.Upsert(layout, request.Options);
            var backup = doc.Save(request.Backup);

            if (entry.HasShortcut) ShortcutService.Delete(entry.PublishedName ?? layout.Name);
            if (request.CreateShortcut) ShortcutService.Create(layout.Name, WtCommandLineBuilder.BuildArguments(layout), elevation == LayoutElevation.All, IconValue.LocalFile(layout.Icon));

            entry.PublishedName = layout.Name;
            entry.IsPublished = true;
            entry.IsDirty = false;
            entry.Keys = request.Options.Keys;
            entry.InNewTabMenu = request.Options.AddToNewTabMenu;
            entry.HasShortcut = request.CreateShortcut;
            entry.HasProfile = request.Options.CreateProfile;
            entry.RaiseActionId();

            var name = layout.Name;
            var installation = _installation;
            var backupName = backup is null ? null : Path.GetFileName(backup);
            SetStatus(() => Loc.F("Published", name, Loc.InstallationName(installation))
                            + (backupName is null ? string.Empty : Loc.F("BackupSuffix", backupName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException)
        {
            _ui.ShowError(Loc.T("PublishFailedTitle"), ex.Message);
        }
    }

    private void DeleteLayout()
    {
        if (SelectedEntry is not { } entry) return;

        if (!entry.IsPublished)
        {
            if (!_ui.Confirm(Loc.T("DeleteDraftTitle"), Loc.F("DeleteDraftMessage", entry.Name))) return;
            RemoveEntry(entry);
            return;
        }

        if (_installation is null || entry.ActionId is null) return;
        var extra = (entry.HasShortcut ? Loc.T("RemoveShortcutToo") : string.Empty)
                    + (entry.HasProfile ? Loc.T("RemoveProfileToo") : string.Empty);
        if (!_ui.Confirm(Loc.T("RemoveTitle"), Loc.F("RemoveMessage", entry.Name, extra))) return;

        try
        {
            var doc = TerminalSettingsDocument.Load(_installation.SettingsPath);
            if (doc.IsLayoutProfileDefault(entry.ActionId))
            {
                _ui.ShowError(Loc.T("RemoveFailedTitle"), Loc.F("RemoveDefaultProfile", entry.Name));
                return;
            }

            doc.Remove(entry.ActionId);
            var backup = doc.Save(backup: true);
            if (entry.HasShortcut) ShortcutService.Delete(entry.PublishedName ?? entry.Name);

            RemoveEntry(entry);
            var removedName = entry.Name;
            var backupName = Path.GetFileName(backup);
            SetStatus(() => Loc.F("Removed", removedName, backupName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            _ui.ShowError(Loc.T("RemoveFailedTitle"), ex.Message);
        }
    }

    private void RemoveEntry(LayoutEntry entry)
    {
        var index = Layouts.IndexOf(entry);
        Layouts.Remove(entry);
        SelectedEntry = Layouts.Count == 0 ? null : Layouts[Math.Min(index, Layouts.Count - 1)];
    }
}
