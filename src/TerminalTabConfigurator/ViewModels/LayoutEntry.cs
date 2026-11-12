// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Localization;
using TerminalTabConfigurator.Mvvm;

namespace TerminalTabConfigurator.ViewModels;

/// <summary>One layout in the side list: published in settings.json, or a draft.</summary>
public sealed class LayoutEntry : ObservableObject
{
    private readonly string _fallbackName;
    private readonly string? _fallbackActionId;
    private bool _isPublished;
    private bool _isDirty;
    private string? _keys;
    private bool _inNewTabMenu;
    private bool _hasShortcut;
    private bool _hasProfile;

    private readonly LayoutParseException? _error;

    public LayoutEntry(TabLayout? layout, string name, string? actionId = null, LayoutParseException? error = null)
    {
        Layout = layout;
        _fallbackName = name;
        _fallbackActionId = actionId;
        _error = error;
        if (layout is not null)
        {
            layout.Changed += (_, _) =>
            {
                IsDirty = true;
                Raise(nameof(Name));
                Raise(nameof(PaneCount));
            };
        }
    }

    /// <summary>Null when the action exists in settings.json but could not be read back.</summary>
    public TabLayout? Layout { get; }

    /// <summary>Why the layout could not be read back, in the current language, or null.</summary>
    public string? Error => _error is null ? null : Loc.ParseError(_error);

    public string Name => Layout?.Name is { Length: > 0 } n ? n : _fallbackName;

    public string? ActionId => Layout is null ? _fallbackActionId : Layout.ActionId;

    public int PaneCount => Layout?.Leaves().Count() ?? 0;

    public bool IsPublished { get => _isPublished; set { if (Set(ref _isPublished, value)) RaiseStatus(); } }

    public bool IsDirty { get => _isDirty; set { if (Set(ref _isDirty, value)) RaiseStatus(); } }

    public string? Keys { get => _keys; set => Set(ref _keys, value); }

    public bool InNewTabMenu { get => _inNewTabMenu; set => Set(ref _inNewTabMenu, value); }

    public bool HasShortcut { get => _hasShortcut; set => Set(ref _hasShortcut, value); }

    /// <summary>A profile that opens the layout was published with it.</summary>
    public bool HasProfile { get => _hasProfile; set => Set(ref _hasProfile, value); }

    /// <summary>Name under which it was last published (the desktop shortcut is named after it).</summary>
    public string? PublishedName { get; set; }

    public string Status
    {
        get
        {
            if (_error is not null) return Loc.T("StatusUnreadable");
            var panes = PaneCount > 1 ? Loc.F("StatusPanes", PaneCount) : Loc.T("StatusOnePane");
            var state = Loc.T(!IsPublished ? "StatusNotPublished" : IsDirty ? "StatusModified" : "StatusPublished");
            return $"{panes} · {state}";
        }
    }

    public void RaiseActionId() => Raise(nameof(ActionId));

    /// <summary>Called after the language changed.</summary>
    public void RefreshTexts()
    {
        Raise(nameof(Error));
        RaiseStatus();
    }

    private void RaiseStatus() => Raise(nameof(Status));
}
