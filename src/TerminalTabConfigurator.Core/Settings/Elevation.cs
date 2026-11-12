// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Settings;

public enum LayoutElevation
{
    /// <summary>No pane runs as administrator: the layout works from any window.</summary>
    None,

    /// <summary>Every pane runs as administrator: the layout needs an elevated window.</summary>
    All,

    /// <summary>Some panes only: impossible, a window is either elevated or not.</summary>
    Mixed,
}

/// <summary>
/// Windows Terminal cannot host an administrator pane in a normal window. Opening an elevated
/// profile from a normal window goes through elevate-shim, which starts another window with that
/// single tab and drops the rest of the layout.
/// </summary>
public static class Elevation
{
    public static bool IsElevated(PaneLeaf pane, IReadOnlyList<TerminalProfile> profiles, string? defaultProfile)
    {
        // An explicit "elevate" on the pane overrides the profile.
        if (pane.ExtraArgs["elevate"] is JsonValue v && v.TryGetValue<bool>(out var elevate)) return elevate;

        var reference = pane.Profile ?? defaultProfile;
        return profiles.FirstOrDefault(p => p.Matches(reference))?.Elevate ?? false;
    }

    public static LayoutElevation Analyze(TabLayout layout, IReadOnlyList<TerminalProfile> profiles, string? defaultProfile)
    {
        var elevated = layout.Leaves().Select(p => IsElevated(p, profiles, defaultProfile)).ToList();
        return elevated.All(e => e) ? LayoutElevation.All
            : elevated.Any(e => e) ? LayoutElevation.Mixed
            : LayoutElevation.None;
    }
}
