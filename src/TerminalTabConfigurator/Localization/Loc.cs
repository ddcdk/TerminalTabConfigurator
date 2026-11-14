// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Localization;

/// <summary>
/// User interface texts in French and English. XAML binds to the indexer (see <see cref="TrExtension"/>),
/// so changing <see cref="Language"/> updates the open windows at once.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string French = "fr";
    public const string English = "en";

    public static Loc Instance { get; } = new();

    private string _language = SystemLanguage;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed, for texts built in code.</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>"fr" when Windows displays French, "en" otherwise.</summary>
    public static string SystemLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == French ? French : English;

    /// <summary>Current language: "fr" or "en".</summary>
    public string Language
    {
        get => _language;
        set
        {
            var language = value == French ? French : English;
            if (language == _language) return;
            _language = language;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string this[string key]
    {
        get
        {
            if (Texts.TryGetValue(key, out var text)) return _language == French ? text.Fr : text.En;
            Debug.Fail($"Missing text: {key}");
            return $"[{key}]";
        }
    }

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Instance[key], args);

    /// <summary>A binding that follows the language, for properties set in code.</summary>
    public static Binding Bind(string key) => new($"[{key}]") { Source = Instance, Mode = BindingMode.OneWay };

    public static bool Has(string key) => Texts.ContainsKey(key);

    public static string InstallationName(TerminalInstallation installation) =>
        installation.Channel == TerminalChannel.Unpackaged ? T("InstallationUnpackaged") : installation.DisplayName;

    public static string ParseError(LayoutParseException error) => error.Error switch
    {
        LayoutParseError.NotMultipleActions => T("ErrNotMultipleActions"),
        LayoutParseError.FirstNotNewTab => T("ErrFirstNotNewTab"),
        LayoutParseError.InvalidAction => F("ErrInvalidAction", error.Detail),
        LayoutParseError.FocusUnknownPane => F("ErrFocusUnknownPane", error.Detail),
        LayoutParseError.UnsupportedAction => F("ErrUnsupportedAction", error.Detail),
        LayoutParseError.UnknownSplitDirection => F("ErrUnknownSplitDirection", error.Detail),
        LayoutParseError.NotWtCommand => T("ErrNotWtCommand"),
        LayoutParseError.UnsupportedOption => F("ErrUnsupportedOption", error.Detail),
        LayoutParseError.MissingOptionValue => F("ErrMissingOptionValue", error.Detail),
        LayoutParseError.InvalidOptionValue => F("ErrInvalidOptionValue", error.Detail),
        _ => error.Detail ?? error.Message,
    };

    public static string TemplateName(string templateId) => T("Tpl_" + templateId);

    public static string TemplateDescription(string templateId) => T("TplDesc_" + templateId);

    private static readonly Dictionary<string, (string Fr, string En)> Texts = new()
    {
        // ---------- Toolbar ----------
        ["New"] = ("Nouvelle", "New"),
        ["Import"] = ("Importer", "Import"),
        ["ImportTooltip"] = ("Créer une disposition à partir d'un profil qui lance wt.exe", "Create a layout from a profile that starts wt.exe"),
        ["ImportFromProfile"] = ("Profils qui ouvrent une disposition :", "Profiles that open a layout:"),
        ["NoProfileToImport"] = ("Aucun profil ne lance wt.exe dans settings.json", "No profile in settings.json starts wt.exe"),
        ["Reload"] = ("Recharger", "Reload"),
        ["ReloadTooltip"] = ("Relire settings.json", "Read settings.json again"),
        ["SplitVertically"] = ("Diviser verticalement", "Split vertically"),
        ["SplitHorizontally"] = ("Diviser horizontalement", "Split horizontally"),
        ["Equalize"] = ("Égaliser", "Equalize"),
        ["EqualizeTooltip"] = ("Donner la même taille à tous les volets", "Give all panes the same size"),
        ["Delete"] = ("Supprimer", "Delete"),
        ["DeleteSelectedPaneTooltip"] = ("Supprimer le volet sélectionné", "Delete the selected pane"),
        ["InstallationLabel"] = ("Installation de Windows Terminal", "Windows Terminal installation"),
        ["InstallationUnpackaged"] = ("Windows Terminal (non empaqueté)", "Windows Terminal (unpackaged)"),
        ["Test"] = ("Tester", "Test"),
        ["TestTooltip"] = ("Ouvre la disposition dans une nouvelle fenêtre, sans rien enregistrer", "Opens the layout in a new window without saving anything"),
        ["AddToTerminal"] = ("Ajouter à Windows Terminal…", "Add to Windows Terminal…"),
        ["Preferences"] = ("Préférences", "Preferences"),
        ["Language"] = ("Langue", "Language"),
        ["LanguageSystem"] = ("Langue du système", "System language"),
        ["Theme"] = ("Thème", "Theme"),
        ["ThemeSystem"] = ("Thème du système", "System theme"),
        ["ThemeDark"] = ("Sombre", "Dark"),
        ["ThemeLight"] = ("Clair", "Light"),

        // ---------- Layout list ----------
        ["MyLayoutsHeader"] = ("MES DISPOSITIONS", "MY LAYOUTS"),
        ["MyLayouts"] = ("Mes dispositions", "My layouts"),
        ["NewLayout"] = ("Nouvelle disposition", "New layout"),
        ["NewLayoutEllipsis"] = ("Nouvelle disposition…", "New layout…"),
        ["RemoveLayout"] = ("Retirer / supprimer", "Remove / delete"),
        ["RemoveLayoutTooltip"] = ("Retire la disposition de Windows Terminal (ou supprime le brouillon)", "Removes the layout from Windows Terminal (or deletes the draft)"),
        ["StatusUnreadable"] = ("illisible", "unreadable"),
        ["StatusOnePane"] = ("1 volet", "1 pane"),
        ["StatusPanes"] = ("{0} volets", "{0} panes"),
        ["StatusNotPublished"] = ("non publié", "not published"),
        ["StatusModified"] = ("modifié", "modified"),
        ["StatusPublished"] = ("publié", "published"),

        // ---------- Preview ----------
        ["ActivePaneTitle"] = ("(titre du volet actif)", "(active pane title)"),
        ["CanvasHint"] = ("Cliquez un volet pour le sélectionner · glissez un séparateur pour redimensionner · clic droit sur un volet pour le diviser",
                          "Click a pane to select it · drag a divider to resize · right-click a pane to split it"),
        ["UnreadableTitle"] = ("Cette disposition n'a pas pu être relue", "This layout could not be read back"),
        ["UnreadableHint"] = ("Elle a sans doute été modifiée à la main dans settings.json. Vous pouvez la retirer, ou la corriger puis recharger.",
                              "It was probably edited by hand in settings.json. You can remove it, or fix it and reload."),
        ["NoSelectionTitle"] = ("Aucune disposition sélectionnée", "No layout selected"),
        ["NoSelectionHint"] = ("Choisissez une disposition dans la liste, ou créez-en une à partir d'un modèle.", "Pick a layout in the list, or create one from a template."),
        ["SplitPaneVertically"] = ("Diviser ce volet verticalement", "Split this pane vertically"),
        ["SplitPaneHorizontally"] = ("Diviser ce volet horizontalement", "Split this pane horizontally"),
        ["DeleteThisPane"] = ("Supprimer ce volet", "Delete this pane"),
        ["DeletePane"] = ("Supprimer le volet", "Delete pane"),
        ["PaneNoFolder"] = ("(dossier du profil)", "(profile folder)"),
        ["PaneNoCommand"] = ("(commande du profil)", "(profile command)"),
        ["DragToResize"] = ("Glisser pour redimensionner", "Drag to resize"),

        // ---------- Properties ----------
        ["Pane"] = ("Volet", "Pane"),
        ["Untitled"] = ("sans titre", "untitled"),
        ["PaneTitle"] = ("Titre du volet", "Pane title"),
        ["WtProfile"] = ("Profil Windows Terminal", "Windows Terminal profile"),
        ["DefaultProfile"] = ("Profil par défaut", "Default profile"),
        ["Hidden"] = ("masqué", "hidden"),
        ["CommandLine"] = ("Ligne de commande", "Command line"),
        ["CommandLineHint"] = ("Vide = la commande du profil. Ex. : pwsh.exe -NoExit -Command \"npm run dev\"",
                               "Empty = the profile's command. E.g. pwsh.exe -NoExit -Command \"npm run dev\""),
        ["StartingDirectory"] = ("Dossier de démarrage", "Starting directory"),
        ["BrowseFolders"] = ("Parcourir les dossiers", "Browse folders"),

        ["PowerShellPrompt"] = ("Invite PowerShell", "PowerShell prompt"),

        ["PromptDefault"] = ("Par défaut (inchangée)", "Default (unchanged)"),

        ["PromptFolderName"] = ("Nom du dossier seul  —  PS front>", "Folder name only  —  PS front>"),

        ["PromptTwoLines"] = ("Chemin complet, saisie à la ligne", "Full path, input on the next line"),

        ["PromptCustom"] = ("Personnalisée…", "Custom…"),

        ["PromptCustomLabel"] = ("Corps de la fonction prompt", "Body of the prompt function"),

        ["PromptHint"] = ("Raccourcit l'invite quand le dossier de démarrage est long. Remplace l'invite du profil PowerShell ($PROFILE, oh-my-posh…).",
                          "Shortens the prompt when the starting directory is long. Replaces the prompt set by the PowerShell profile ($PROFILE, oh-my-posh…)."),

        ["PromptCustomHint"] = ("Expression PowerShell qui renvoie le texte de l'invite. Ex. : \"$(Split-Path -Leaf $PWD)> \"",
                                "PowerShell expression that returns the prompt text. E.g. \"$(Split-Path -Leaf $PWD)> \""),

        ["PromptNeedsCommandline"] = ("Ignorée : l'invite est transmise par la ligne de commande, qui est vide. Indiquez pwsh.exe ou powershell.exe.",
                                      "Ignored: the prompt is passed on the command line, which is empty. Enter pwsh.exe or powershell.exe."),

        ["PromptNotApplied"] = ("Ignorée : la ligne de commande doit lancer pwsh.exe ou powershell.exe, sans script ni -Command.",
                                "Ignored: the command line must start pwsh.exe or powershell.exe, without a script or -Command."),
        ["SizeInSplit"] = ("Taille dans sa division", "Size within its split"),
        ["FocusOnStart"] = ("Focus sur ce volet à l'ouverture", "Focus this pane when the tab opens"),
        ["TabSection"] = ("ONGLET", "TAB"),
        ["LayoutNameLabel"] = ("Nom (palette de commandes et menu)", "Name (command palette and menu)"),
        ["LayoutName"] = ("Nom de la disposition", "Layout name"),
        ["IconLabel"] = ("Icône (palette de commandes et menu)", "Icon (command palette and menu)"),
        ["Icon"] = ("Icône", "Icon"),
        ["ChooseImage"] = ("Choisir une image", "Choose an image"),
        ["IconHint"] = ("Image (.png, .ico, .jpg), emoji ou glyphe Segoe Fluent Icons. Vide = aucune icône. Un fichier .ico sert aussi d'icône au raccourci Bureau.",
                        "Image (.png, .ico, .jpg), emoji or Segoe Fluent Icons glyph. Empty = no icon. An .ico file is also used for the desktop shortcut."),
        ["TabTitle"] = ("Titre de l'onglet", "Tab title"),
        ["TabTitleHint"] = ("Indépendant des titres de volets. Vide = l'onglet affiche le titre du volet actif.",
                            "Independent of the pane titles. Empty = the tab shows the title of the active pane."),
        ["TabColor"] = ("Couleur de l'onglet", "Tab color"),
        ["TabColorHex"] = ("Couleur de l'onglet (hexadécimal)", "Tab color (hex)"),
        ["NoColor"] = ("Aucune couleur", "No color"),
        ["ActionLabel"] = ("Action :", "Action:"),
        ["KeysLabel"] = ("Raccourci clavier :", "Keyboard shortcut:"),
        ["InNewTabMenu"] = ("Présente dans le menu « Nouvel onglet »", "Listed in the \"New tab\" menu"),
        ["HasDesktopShortcut"] = ("Raccourci présent sur le Bureau", "Desktop shortcut present"),
        ["HasLayoutProfile"] = ("Profil Windows Terminal créé avec la disposition", "Windows Terminal profile created with the layout"),
        ["GeneratedCommand"] = ("Commande générée", "Generated command"),
        ["GeneratedCommandName"] = ("Commande wt.exe générée", "Generated wt.exe command"),
        ["Copy"] = ("Copier", "Copy"),

        // ---------- Messages ----------
        ["NoSettingsFound"] = ("Aucun settings.json de Windows Terminal trouvé. Lancez Windows Terminal une fois puis rechargez.",
                               "No Windows Terminal settings.json found. Start Windows Terminal once, then reload."),
        ["ElevationAll"] = ("Profil administrateur : Windows Terminal ne peut pas ouvrir ces volets dans une fenêtre normale. Depuis le menu, la palette ou un raccourci clavier, la disposition ne fonctionnera que dans une fenêtre lancée en administrateur. « Tester » et le raccourci Bureau la lancent en administrateur.",
                            "Administrator profile: Windows Terminal cannot open these panes in a normal window. From the menu, the palette or a keyboard shortcut, the layout only works in a window started as administrator. \"Test\" and the desktop shortcut start it as administrator."),
        ["ElevationMixed"] = ("Volets administrateur et volets normaux mélangés : impossible dans un même onglet. Utilisez des profils tous administrateur ou tous normaux.",
                              "Administrator and normal panes are mixed: this is not possible in a single tab. Use profiles that are all administrator or all normal."),
        ["UnsavedTitle"] = ("Modifications non publiées", "Unpublished changes"),
        ["UnsavedMessage"] = ("Certaines dispositions ont des modifications qui ne sont pas encore dans Windows Terminal. Les abandonner ?",
                              "Some layouts have changes that are not in Windows Terminal yet. Discard them?"),
        ["ReadErrorTitle"] = ("Lecture impossible", "Cannot read the settings"),
        ["ReadErrorMessage"] = ("Impossible de lire {0} :\n{1}", "Cannot read {0}:\n{1}"),
        ["NoLayoutsYet"] = ("Aucune disposition publiée pour l'instant. Créez-en une avec « Nouvelle ».", "No layout published yet. Create one with \"New\"."),
        ["LayoutsRead"] = ("{0} disposition(s) lue(s) depuis {1}.", "{0} layout(s) read from {1}."),
        ["LayoutCreated"] = ("Nouvelle disposition créée. Configurez chaque volet puis publiez-la.", "New layout created. Set up each pane, then publish it."),
        ["ProfileImported"] = ("Profil « {0} » importé comme nouvelle disposition. Publiez-la pour l'ajouter aux actions ; le profil d'origine n'est pas modifié.",
                               "Profile \"{0}\" imported as a new layout. Publish it to add it to the actions; the original profile is left unchanged."),
        ["ImportFailedTitle"] = ("Import impossible", "Cannot import"),
        ["ImportFailedMessage"] = ("Le profil « {0} » ne peut pas être importé :\n{1}", "Profile \"{0}\" cannot be imported:\n{1}"),
        ["TestFailedTitle"] =("Test impossible", "Cannot test"),
        ["TestOpened"] = ("Disposition ouverte dans une nouvelle fenêtre Windows Terminal (test, rien n'est enregistré).",
                          "Layout opened in a new Windows Terminal window (test, nothing is saved)."),
        ["TestOpenedElevated"] = ("Disposition ouverte dans une nouvelle fenêtre Windows Terminal administrateur (test, rien n'est enregistré).",
                                  "Layout opened in a new administrator Windows Terminal window (test, nothing is saved)."),
        ["TestCancelled"] = ("Test annulé : l'élévation administrateur a été refusée.", "Test cancelled: administrator elevation was declined."),
        ["CannotLaunchWt"] = ("Impossible de lancer wt.exe :\n{0}", "Cannot start wt.exe:\n{0}"),
        ["MissingNameTitle"] = ("Nom manquant", "Missing name"),
        ["MissingNameMessage"] = ("Donnez un nom à la disposition : il apparaît dans la palette de commandes et le menu.",
                                  "Give the layout a name: it appears in the command palette and the menu."),
        ["PublishFailedTitle"] = ("Publication impossible", "Cannot publish"),
        ["Published"] = ("« {0} » publiée dans {1}.", "\"{0}\" published to {1}."),
        ["BackupSuffix"] = (" Sauvegarde : {0}", " Backup: {0}"),
        ["DeleteDraftTitle"] = ("Supprimer le brouillon", "Delete draft"),
        ["DeleteDraftMessage"] = ("Supprimer « {0} » ? Elle n'a jamais été publiée.", "Delete \"{0}\"? It was never published."),
        ["RemoveTitle"] = ("Retirer de Windows Terminal", "Remove from Windows Terminal"),
        ["RemoveMessage"] = ("Retirer « {0} » de Windows Terminal (action, raccourci clavier, entrée de menu) ?{1}\nUne sauvegarde de settings.json est faite avant.",
                             "Remove \"{0}\" from Windows Terminal (action, keyboard shortcut, menu entry)?{1}\nsettings.json is backed up first."),
        ["RemoveShortcutToo"] = ("\nLe raccourci du Bureau sera aussi supprimé.", "\nThe desktop shortcut will be deleted too."),
        ["RemoveProfileToo"] = ("\nLe profil créé avec la disposition sera aussi supprimé.", "\nThe profile created with the layout will be deleted too."),
        ["RemoveDefaultProfile"] = ("« {0} » ne peut pas être retirée : son profil est le profil par défaut de Windows Terminal. Choisissez un autre profil par défaut dans les paramètres de Windows Terminal, puis recommencez.",
                                    "\"{0}\" cannot be removed: its profile is the Windows Terminal default profile. Pick another default profile in the Windows Terminal settings, then try again."),
        ["Removed"] = ("« {0} » retirée de Windows Terminal. Sauvegarde : {1}", "\"{0}\" removed from Windows Terminal. Backup: {1}"),
        ["RemoveFailedTitle"] = ("Suppression impossible", "Cannot remove"),
        ["Copied"] = ("Commande copiée dans le presse-papiers.", "Command copied to the clipboard."),
        ["ClipboardBusy"] = ("Le presse-papiers est occupé, réessayez.", "The clipboard is busy, try again."),
        ["IconDialogTitle"] = ("Icône de la disposition", "Layout icon"),
        ["ImageFiles"] = ("Images", "Images"),
        ["AllFiles"] = ("Tous les fichiers", "All files"),
        ["WScriptMissing"] = ("WScript.Shell n'est pas disponible.", "WScript.Shell is not available."),
        ["WtNotFound"] = ("wt.exe est introuvable : Windows Terminal est-il installé ?", "wt.exe cannot be found: is Windows Terminal installed?"),
        ["ShortcutDescription"] = ("Ouvre la disposition « {0} » dans Windows Terminal", "Opens the \"{0}\" layout in Windows Terminal"),
        ["DefaultShortcutName"] = ("Disposition", "Layout"),

        // ---------- Read-back errors ----------
        ["ErrNotMultipleActions"] = ("La commande n'est pas une action « multipleActions ».", "The command is not a \"multipleActions\" action."),
        ["ErrFirstNotNewTab"] = ("La première action doit être « newTab ».", "The first action must be \"newTab\"."),
        ["ErrInvalidAction"] = ("Action n°{0} invalide.", "Action #{0} is invalid."),
        ["ErrFocusUnknownPane"] = ("focusPane vers un volet inexistant ({0}).", "focusPane targets a pane that does not exist ({0})."),
        ["ErrUnsupportedAction"] = ("Action « {0} » non prise en charge par l'éditeur.", "Action \"{0}\" is not supported by the editor."),
        ["ErrUnknownSplitDirection"] = ("Direction de division inconnue : « {0} ».", "Unknown split direction: \"{0}\"."),
        ["ErrNotWtCommand"] = ("La ligne de commande ne lance pas wt.exe.", "The command line does not start wt.exe."),
        ["ErrUnsupportedOption"] = ("Option « {0} » non prise en charge par l'éditeur.", "Option \"{0}\" is not supported by the editor."),
        ["ErrMissingOptionValue"] = ("L'option « {0} » n'a pas de valeur.", "Option \"{0}\" has no value."),
        ["ErrInvalidOptionValue"] = ("Valeur invalide : « {0} ».", "Invalid value: \"{0}\"."),

        // ---------- New layout dialog ----------
        ["StartFromTemplate"] = ("Partir d'un modèle", "Start from a template"),
        ["TemplateHint"] = ("Choisissez un découpage. Vous pourrez ensuite diviser, supprimer et redimensionner les volets librement.",
                            "Pick a layout. You can then split, delete and resize panes freely."),
        ["Templates"] = ("Modèles", "Templates"),
        ["Name"] = ("Nom", "Name"),
        ["ProfileAllPanes"] = ("Profil de tous les volets", "Profile for all panes"),
        ["StartFolderLabel"] = ("Dossier de départ (facultatif, appliqué à tous les volets)", "Starting directory (optional, applied to all panes)"),
        ["Cancel"] = ("Annuler", "Cancel"),
        ["Create"] = ("Créer", "Create"),
        ["ChooseTemplate"] = ("Choisissez un modèle.", "Pick a template."),
        ["GiveName"] = ("Donnez un nom à la disposition.", "Give the layout a name."),
        ["Tpl_single"] = ("Volet unique", "Single pane"),
        ["Tpl_two-columns"] = ("2 colonnes", "2 columns"),
        ["Tpl_two-rows"] = ("2 lignes", "2 rows"),
        ["Tpl_one-plus-two-right"] = ("1 + 2 à droite", "1 + 2 on the right"),
        ["Tpl_two-left-plus-one"] = ("2 à gauche + 1", "2 on the left + 1"),
        ["Tpl_one-top-two-bottom"] = ("1 en haut + 2 en bas", "1 on top + 2 below"),
        ["Tpl_three-columns"] = ("3 colonnes", "3 columns"),
        ["Tpl_grid-2x2"] = ("Grille 2 × 2", "2 × 2 grid"),
        ["TplDesc_single"] = ("1 volet", "1 pane"),
        ["TplDesc_two-columns"] = ("2 volets côte à côte", "2 panes side by side"),
        ["TplDesc_two-rows"] = ("2 volets empilés", "2 stacked panes"),
        ["TplDesc_one-plus-two-right"] = ("3 volets", "3 panes"),
        ["TplDesc_two-left-plus-one"] = ("3 volets", "3 panes"),
        ["TplDesc_one-top-two-bottom"] = ("3 volets", "3 panes"),
        ["TplDesc_three-columns"] = ("3 volets", "3 panes"),
        ["TplDesc_grid-2x2"] = ("4 volets", "4 panes"),

        // ---------- Publish dialog ----------
        ["PublishWindowTitle"] = ("Ajouter à Windows Terminal", "Add to Windows Terminal"),
        ["Target"] = ("Cible :", "Target:"),
        ["ElevatedLayout"] = ("Disposition administrateur.", "Administrator layout."),
        ["ElevatedLayoutText"] = ("Windows Terminal ne peut pas ouvrir des volets administrateur dans une fenêtre normale : il ouvrirait une autre fenêtre avec un seul volet. Le menu, la palette et le raccourci clavier ne fonctionneront que dans une fenêtre Windows Terminal lancée en administrateur. Depuis le Bureau, le raccourci (lancé en administrateur) fonctionne toujours.",
                                  "Windows Terminal cannot open administrator panes in a normal window: it would open another window with a single pane. The menu, the palette and the keyboard shortcut only work in a Windows Terminal window started as administrator. The desktop shortcut (started as administrator) always works."),
        ["PublishFooter"] = ("Windows Terminal recharge settings.json tout seul : la disposition est disponible dès la publication.",
                             "Windows Terminal reloads settings.json on its own: the layout is available as soon as it is published."),
        ["Publish"] = ("Publier", "Publish"),
        ["WhereToAdd"] = ("OÙ L'AJOUTER", "WHERE TO ADD IT"),
        ["CommandPalette"] = ("Palette de commandes", "Command palette"),
        ["PaletteAlways"] = ("Toujours présente : c'est l'action elle-même.", "Always included: it is the action itself."),
        ["KeyboardShortcut"] = ("Raccourci clavier", "Keyboard shortcut"),
        ["KeysExample"] = ("Ex. : ctrl+alt+d, ctrl+shift+1, alt+f2", "E.g. ctrl+alt+d, ctrl+shift+1, alt+f2"),
        ["NewTabMenu"] = ("Menu « Nouvel onglet » (bouton ˅)", "\"New tab\" menu (˅ button)"),
        ["NewTabMenuHint"] = ("Ajoutée en tête du menu ; les profils restent listés en dessous.", "Added at the top of the menu; profiles stay listed below."),
        ["NewTabMenuHintProfile"] = ("C'est le profil qui est ajouté en tête du menu, à la place de l'action (une seule entrée).",
                                     "The profile is added at the top of the menu, instead of the action (a single entry)."),
        ["AlsoProfile"] = ("Créer aussi un profil", "Also create a profile"),
        ["ProfileHintMenu"] = ("Un profil « {0} » qui lance la commande wt.exe équivalente : il peut servir de profil par défaut ou être lancé avec wt -p.",
                               "A \"{0}\" profile that runs the equivalent wt.exe command: it can be the default profile or be started with wt -p."),
        ["ProfileHintHidden"] = ("Un profil « {0} » qui lance la commande wt.exe équivalente. Il est masqué tant que le menu « Nouvel onglet » est décoché ; il reste utilisable avec wt -p.",
                                 "A \"{0}\" profile that runs the equivalent wt.exe command. It is hidden while the \"New tab\" menu is cleared; it can still be started with wt -p."),
        ["ProfileIsDefault"] = ("Ce profil est le profil par défaut de Windows Terminal : choisissez un autre profil par défaut avant de le retirer.",
                                "This profile is the Windows Terminal default profile: pick another default profile before removing it."),
        ["DesktopShortcut"] = ("Raccourci sur le Bureau", "Desktop shortcut"),
        ["BackupBefore"] = ("Sauvegarder settings.json avant", "Back up settings.json first"),
        ["BackupHint"] = ("Copie horodatée à côté du fichier (.ttc-backup-…). Les commentaires du fichier ne sont pas conservés à l'écriture.",
                          "Timestamped copy next to the file (.ttc-backup-…). Comments in the file are not kept when it is written."),
        ["JsonPreviewHeader"] = ("CE QUI SERA ÉCRIT DANS SETTINGS.JSON", "WHAT WILL BE WRITTEN TO SETTINGS.JSON"),
        ["JsonPreview"] = ("Aperçu JSON", "JSON preview"),
        ["UpdateHeader"] = ("Mettre à jour « {0} »", "Update \"{0}\""),
        ["AddHeader"] = ("Ajouter « {0} » à Windows Terminal", "Add \"{0}\" to Windows Terminal"),
        ["WtNotFoundShortcut"] = ("wt.exe introuvable : le raccourci ne peut pas être créé.", "wt.exe not found: the shortcut cannot be created."),
        ["ShortcutElevated"] = ("Crée {0}, lancé en administrateur (Windows demandera confirmation).", "Creates {0}, started as administrator (Windows will ask for confirmation)."),
        ["ShortcutNormal"] = ("Crée {0} qui ouvre la disposition dans la dernière fenêtre utilisée.", "Creates {0}, which opens the layout in the most recently used window."),
        ["EnterKeys"] = ("Saisissez une combinaison de touches, ou décochez l'option.", "Type a key combination, or clear the option."),
        ["KeysConflict"] = ("Cette combinaison est déjà utilisée par « {0} ». Choisissez-en une autre.", "This combination is already used by \"{0}\". Pick another one."),
        ["LayoutUnreadable"] = ("La disposition n'a pas pu être relue.", "The layout could not be read back."),
    };
}
