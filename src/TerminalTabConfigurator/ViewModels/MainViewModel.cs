using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Settings;
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

    public MainViewModel(IUserInteraction ui)
    {
        _ui = ui;

        NewLayoutCommand = new RelayCommand(NewLayout, () => _installation is not null);
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
        if (_installation is null)
            StatusMessage = "Aucun settings.json de Windows Terminal trouvé. Lancez Windows Terminal une fois puis rechargez.";
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
        }
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
        LayoutElevation.All =>
            "Profil administrateur : Windows Terminal ne peut pas ouvrir ces volets dans une fenêtre normale. "
            + "Depuis le menu, la palette ou un raccourci clavier, la disposition ne fonctionnera que dans une fenêtre lancée en administrateur. "
            + "« Tester » et le raccourci Bureau la lancent en administrateur.",
        LayoutElevation.Mixed =>
            "Volets administrateur et volets normaux mélangés : impossible dans un même onglet. "
            + "Utilisez des profils tous administrateur ou tous normaux.",
        _ => null,
    };

    public string CommandPreview { get => _commandPreview; private set => Set(ref _commandPreview, value); }

    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    public bool HasUnsavedChanges => Layouts.Any(e => e.IsDirty);

    public ICommand NewLayoutCommand { get; }
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
        _ui.Confirm("Modifications non publiées",
            "Certaines dispositions ont des modifications qui ne sont pas encore dans Windows Terminal. Les abandonner ?");

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
            _ui.ShowError("Lecture impossible", $"Impossible de lire {_installation.SettingsPath} :\n{ex.Message}");
            return;
        }

        var profiles = doc.GetProfiles();
        ProfileCatalog.Profiles = profiles;
        Profiles.Add(new ProfileOption("Profil par défaut", string.Empty));
        foreach (var p in profiles)
        {
            var label = ProfileCatalog.Label(p) + (p.Hidden ? " (masqué)" : string.Empty);
            Profiles.Add(new ProfileOption(label, p.Reference));
        }

        _defaultProfile = doc.DefaultProfile;

        foreach (var published in doc.GetLayouts())
        {
            Layouts.Add(new LayoutEntry(published.Layout, published.Name, published.ActionId, published.Error)
            {
                IsPublished = true,
                Keys = published.Keys,
                InNewTabMenu = published.InNewTabMenu,
                HasShortcut = ShortcutService.Exists(published.Name),
                PublishedName = published.Name,
            });
        }

        SelectedEntry = Layouts.FirstOrDefault(e => e.ActionId == previousId) ?? Layouts.FirstOrDefault();
        StatusMessage = Layouts.Count == 0
            ? "Aucune disposition publiée pour l'instant. Créez-en une avec « Nouvelle »."
            : $"{Layouts.Count} disposition(s) lue(s) depuis {_installation.DisplayName}.";
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
        StatusMessage = "Nouvelle disposition créée. Configurez chaque volet puis publiez-la.";
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
            _ui.ShowError("Test impossible", ElevationWarning!);
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
            StatusMessage = elevation == LayoutElevation.All
                ? "Disposition ouverte dans une nouvelle fenêtre Windows Terminal administrateur (test, rien n'est enregistré)."
                : "Disposition ouverte dans une nouvelle fenêtre Windows Terminal (test, rien n'est enregistré).";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            StatusMessage = "Test annulé : l'élévation administrateur a été refusée.";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _ui.ShowError("Test impossible", $"Impossible de lancer wt.exe :\n{ex.Message}");
        }
    }

    private void Publish()
    {
        if (SelectedEntry is not { Layout: { } layout } entry || _installation is null) return;
        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            _ui.ShowError("Nom manquant", "Donnez un nom à la disposition : il apparaît dans la palette de commandes et le menu.");
            return;
        }

        var elevation = SelectedElevation;
        if (elevation == LayoutElevation.Mixed)
        {
            _ui.ShowError("Publication impossible", ElevationWarning!);
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
            entry.RaiseActionId();

            StatusMessage = $"« {layout.Name} » publiée dans {_installation.DisplayName}."
                            + (backup is null ? string.Empty : $" Sauvegarde : {Path.GetFileName(backup)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException)
        {
            _ui.ShowError("Publication impossible", ex.Message);
        }
    }

    private void DeleteLayout()
    {
        if (SelectedEntry is not { } entry) return;

        if (!entry.IsPublished)
        {
            if (!_ui.Confirm("Supprimer le brouillon", $"Supprimer « {entry.Name} » ? Elle n'a jamais été publiée.")) return;
            RemoveEntry(entry);
            return;
        }

        if (_installation is null || entry.ActionId is null) return;
        var extra = entry.HasShortcut ? "\nLe raccourci du Bureau sera aussi supprimé." : string.Empty;
        if (!_ui.Confirm("Retirer de Windows Terminal",
                $"Retirer « {entry.Name} » de Windows Terminal (action, raccourci clavier, entrée de menu) ?{extra}\nUne sauvegarde de settings.json est faite avant.")) return;

        try
        {
            var doc = TerminalSettingsDocument.Load(_installation.SettingsPath);
            doc.Remove(entry.ActionId);
            var backup = doc.Save(backup: true);
            if (entry.HasShortcut) ShortcutService.Delete(entry.PublishedName ?? entry.Name);

            RemoveEntry(entry);
            StatusMessage = $"« {entry.Name} » retirée de Windows Terminal. Sauvegarde : {Path.GetFileName(backup)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            _ui.ShowError("Suppression impossible", ex.Message);
        }
    }

    private void RemoveEntry(LayoutEntry entry)
    {
        var index = Layouts.IndexOf(entry);
        Layouts.Remove(entry);
        SelectedEntry = Layouts.Count == 0 ? null : Layouts[Math.Min(index, Layouts.Count - 1)];
    }
}
