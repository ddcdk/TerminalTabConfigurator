using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Templates;
using TerminalTabConfigurator.ViewModels;

namespace TerminalTabConfigurator.Dialogs;

public sealed record TemplateOption(LayoutTemplate Template, TabLayout Preview);

public partial class NewLayoutWindow : Window
{
    private string _suggestedName = string.Empty;

    public NewLayoutWindow(IReadOnlyList<ProfileOption> profiles, string? defaultProfile)
    {
        InitializeComponent();

        TemplateList.ItemsSource = LayoutTemplates.All
            .Select(t => new TemplateOption(t, t.Create(t.Name, null, null)))
            .ToList();

        ProfileBox.ItemsSource = profiles;
        // Start on the explicit default profile when it is listed, otherwise on "Profil par défaut".
        ProfileBox.SelectedValue = profiles.Any(p => p.Reference == defaultProfile) ? defaultProfile : string.Empty;
        if (ProfileBox.SelectedIndex < 0) ProfileBox.SelectedIndex = 0;

        TemplateList.SelectedIndex = 1;
        Loaded += (_, _) => NameBox.Focus();
    }

    public NewLayoutRequest? Result { get; private set; }

    private void OnTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        // Keep following the template name until the user types their own.
        if (TemplateList.SelectedItem is TemplateOption option && (NameBox.Text.Length == 0 || NameBox.Text == _suggestedName))
        {
            _suggestedName = option.Template.Name;
            NameBox.Text = _suggestedName;
            NameBox.SelectAll();
        }

        Validate();
    }

    private void OnTemplateDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TemplateList.SelectedItem is not null) OnCreate(sender, e);
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => Validate();

    private bool Validate()
    {
        string? error = null;
        if (TemplateList.SelectedItem is null) error = "Choisissez un modèle.";
        else if (string.IsNullOrWhiteSpace(NameBox.Text)) error = "Donnez un nom à la disposition.";

        ErrorText.Text = error;
        ErrorText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var folder = FolderPicker.Pick(this, FolderBox.Text);
        if (folder is not null) FolderBox.Text = folder;
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        if (!Validate() || TemplateList.SelectedItem is not TemplateOption option) return;

        var profile = ProfileBox.SelectedValue as string;
        var folder = FolderBox.Text.Trim();
        Result = new NewLayoutRequest(
            option.Template,
            NameBox.Text.Trim(),
            folder.Length > 0 ? folder : null,
            string.IsNullOrEmpty(profile) ? null : profile);
        DialogResult = true;
    }
}
