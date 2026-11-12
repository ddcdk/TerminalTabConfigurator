// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Templates;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.ViewModels;

namespace TerminalTabConfigurator.Dialogs;

public sealed record TemplateOption(LayoutTemplate Template, TabLayout Preview)
{
    public string Name => Loc.TemplateName(Template.Id);

    public string Description => Loc.TemplateDescription(Template.Id);
}

public partial class NewLayoutWindow : Window
{
    private string _suggestedName = string.Empty;

    public NewLayoutWindow(IReadOnlyList<ProfileOption> profiles, string? defaultProfile)
    {
        InitializeComponent();

        TemplateList.ItemsSource = LayoutTemplates.All
            .Select(t => new TemplateOption(t, t.Create(Loc.TemplateName(t.Id), null, null)))
            .ToList();

        ProfileBox.ItemsSource = profiles;
        // Start on the explicit default profile when it is listed, otherwise on "Default profile".
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
            _suggestedName = option.Name;
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
        if (TemplateList.SelectedItem is null) error = Loc.T("ChooseTemplate");
        else if (string.IsNullOrWhiteSpace(NameBox.Text)) error = Loc.T("GiveName");

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
