# Changelog

All notable changes to this project are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1] - 2026-11-11

First public version.

### Added

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
