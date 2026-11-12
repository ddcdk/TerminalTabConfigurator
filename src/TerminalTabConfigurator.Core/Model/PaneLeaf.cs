// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;

namespace TerminalTabConfigurator.Core.Model;

/// <summary>One terminal pane: what it runs and where it starts.</summary>
public sealed class PaneLeaf : LayoutNode
{
    private string? _title;
    private string? _profile;
    private string? _commandline;
    private string? _startingDirectory;
    private bool _focusOnStart;
    private int _number;
    private bool _isSelected;

    /// <summary>Pane title (sent as <c>tabTitle</c>).</summary>
    public string? Title { get => _title; set => Set(ref _title, Normalize(value)); }

    /// <summary>Windows Terminal profile, by GUID or name. Null = no profile (default profile + commandline).</summary>
    public string? Profile { get => _profile; set => Set(ref _profile, Normalize(value)); }

    public string? Commandline { get => _commandline; set => Set(ref _commandline, Normalize(value)); }

    public string? StartingDirectory { get => _startingDirectory; set => Set(ref _startingDirectory, Normalize(value)); }

    /// <summary>When true, this pane gets the focus once the tab is built. Only one pane can have it.</summary>
    public bool FocusOnStart
    {
        get => _focusOnStart;
        set
        {
            if (!Set(ref _focusOnStart, value) || !value || Owner?.Root is null) return;
            foreach (var other in Owner.Root.Leaves())
                if (!ReferenceEquals(other, this)) other.FocusOnStart = false;
        }
    }

    /// <summary>Arguments read from settings.json that this tool does not edit; written back unchanged.</summary>
    public JsonObject ExtraArgs { get; set; } = new();

    /// <summary>1-based position, top-left first (UI only).</summary>
    public int Number
    {
        get => _number;
        internal set
        {
            if (!Set(ref _number, value, affectsLayout: false)) return;
            Raise(nameof(IsPrimary));
        }
    }

    /// <summary>The pane created by <c>newTab</c>.</summary>
    public bool IsPrimary => _number == 1;

    /// <summary>Selection state in the editor (UI only).</summary>
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value, affectsLayout: false); }

    public PaneLeaf CloneSettings() => new()
    {
        _profile = _profile,
        _commandline = _commandline,
        _startingDirectory = _startingDirectory,
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
