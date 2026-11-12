# Changelog

All notable changes to this project are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **Import a profile as a layout**: the new **Import** button lists the profiles whose command line starts `wt.exe` (`wt.exe -w 0 nt … ; sp … ; fp …`) and turns the chosen one into a new, unpublished layout (panes, titles, profiles, folders, command lines, sizes, focus, tab color, icon). The profile itself is left unchanged.
- **Also create a profile** option when publishing: a Windows Terminal profile named after the layout, whose command line is the equivalent `wt.exe` command, so the layout can be the default profile or be started with `wt -p`. It is updated on each publication and removed with the layout; settings added to it by hand (colors, font…) are kept. The "New tab" menu then shows the profile instead of the action, so the layout appears only once; when the menu option is cleared, the profile is hidden. The action stays in the command palette and keeps its keyboard shortcut. A layout whose profile is the default profile cannot be removed.
- Profiles that open a layout (`wt.exe …`) are no longer offered in the pane profile list, and profiles created by the tool are not offered for import.

### Fixed

- A space typed at the end of a text field (command line, pane title, starting directory, layout name, tab title, icon) was removed immediately, so `pwsh -NoExit c:\script.ps1` could not be entered. Fields now keep the text as typed; it is trimmed when the command is generated and when the layout is published.

### Removed

- The "Tab preview" caption next to the previewed tab header: it looked like a second, inactive tab.

## [0.1] - 2026-11-12

First public version.

### Added

- Project released under the **GNU General Public License v3.0 or later** (`LICENSE` file), copyright Delfim Da Costa. Copyright and SPDX license headers in every source file, license and author in the assembly metadata.
- **Visual editor** for a Windows Terminal tab split into panes: split vertically or horizontally (toolbar, pane buttons, right-click), delete, equalize, resize by dragging the dividers (the split percentage is shown on each divider).
- **Starting templates**: single pane, 2 columns, 2 rows, 1 + 2 on the right, 2 on the left + 1, 1 on top + 2 below, 3 columns, 2 × 2 grid.
- **Pane settings**: title (the first pane included), Windows Terminal profile, command line, starting directory, size, focus when the tab opens.
- **Tab settings**: name shown in the command palette and the "New tab" menu, icon (emoji, Segoe Fluent Icons glyph or image file), tab title independent of the pane titles, tab color.
- **Publishing to `settings.json`** as a `multipleActions` action (`User.TTC.<name>`), with:
  - an optional keyboard shortcut, with conflict detection;
  - an optional entry at the top of the "New tab" menu;
  - an optional desktop shortcut;
  - an exact preview of the JSON written;
  - a timestamped backup of `settings.json` before each write.
- **Reading back and editing** layouts already published. Actions edited by hand with unsupported steps are listed with an explanation.
- **Test**: opens the layout right away in a new Windows Terminal window, without saving anything.
- **Generated `wt.exe` command**, ready to copy into a script.
- **Administrator profiles**: layouts whose panes are all elevated are tested and launched from the desktop shortcut as administrator. Layouts that mix elevated and normal panes are blocked, with an explanation.
- Support for Windows Terminal, Windows Terminal Preview, Canary and the unpackaged version.
- **English and French interface**, following the Windows display language by default.
- **Dark and light themes**, following the Windows app mode by default.
- Preferences menu (gear button) to change the language and the theme on the fly; choices are saved in `%APPDATA%\TerminalTabConfigurator\preferences.json`.
- Application icon, README in English and French.
- Self-contained build for Windows x64 attached to the release.

[0.1]: https://github.com/ddcdk/TerminalTabConfigurator/releases/tag/v0.1
