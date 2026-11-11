using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Dialogs;
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

    public bool Confirm(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? PickFolder(string? initialFolder) => FolderPicker.Pick(this, initialFolder);

    public string? PickIconFile(string? current)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Icône de la disposition",
            Filter = "Images (*.png;*.ico;*.jpg;*.jpeg;*.svg)|*.png;*.ico;*.jpg;*.jpeg;*.svg|Tous les fichiers|*.*",
        };
        if (IconValue.LocalFile(current) is { } file) dialog.InitialDirectory = Path.GetDirectoryName(file);
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public NewLayoutRequest? AskNewLayout(IReadOnlyList<ProfileOption> profiles, string? defaultProfile)
    {
        var dialog = new NewLayoutWindow(profiles, defaultProfile) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Result : null;
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
            _viewModel.StatusMessage = "Commande copiée dans le presse-papiers.";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            _viewModel.StatusMessage = "Le presse-papiers est occupé, réessayez.";
        }
    }
}

internal static class FolderPicker
{
    public static string? Pick(Window owner, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de démarrage" };
        var initial = Environment.ExpandEnvironmentVariables(initialFolder ?? string.Empty);
        if (initial.Length > 0 && Directory.Exists(initial)) dialog.InitialDirectory = initial;
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }
}
