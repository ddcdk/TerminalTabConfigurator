// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Templates;

/// <summary>A starting layout offered when creating a new one. The UI names it from <see cref="Id"/>.</summary>
public sealed record LayoutTemplate(string Id, Func<LayoutNode> CreateRoot)
{
    /// <summary>Creates a layout whose panes all start in <paramref name="folder"/> and run <paramref name="profile"/>.</summary>
    public TabLayout Create(string layoutName, string? folder, string? profile)
    {
        var root = CreateRoot();
        foreach (var pane in root.Leaves())
        {
            pane.StartingDirectory = folder;
            pane.Profile = profile;
        }

        return new TabLayout(layoutName, root) { TabTitle = layoutName };
    }
}

public static class LayoutTemplates
{
    private const SplitOrientation V = SplitOrientation.Vertical;
    private const SplitOrientation H = SplitOrientation.Horizontal;

    private static PaneLeaf P() => new();

    public static IReadOnlyList<LayoutTemplate> All { get; } =
    [
        new("single", P),
        new("two-columns", () => new SplitNode(V, P(), P())),
        new("two-rows", () => new SplitNode(H, P(), P())),
        new("one-plus-two-right", () => new SplitNode(V, P(), new SplitNode(H, P(), P()))),
        new("two-left-plus-one", () => new SplitNode(V, new SplitNode(H, P(), P()), P())),
        new("one-top-two-bottom", () => new SplitNode(H, P(), new SplitNode(V, P(), P()), 0.6)),
        new("three-columns", () => new SplitNode(V, P(), new SplitNode(V, P(), P()), 1 / 3.0)),
        new("grid-2x2", () => new SplitNode(V, new SplitNode(H, P(), P()), new SplitNode(H, P(), P()))),
    ];
}
