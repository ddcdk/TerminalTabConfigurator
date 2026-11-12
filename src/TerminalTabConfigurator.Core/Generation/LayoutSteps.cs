// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Generation;

public abstract record LayoutStep;

/// <summary>Opens the tab; the new pane gets id 0.</summary>
public sealed record NewTabStep(PaneLeaf Pane) : LayoutStep;

/// <summary>Splits the focused pane. <see cref="Size"/> is the share given to the new pane. The new pane gets the next id and the focus.</summary>
public sealed record SplitPaneStep(SplitOrientation Orientation, double Size, PaneLeaf Pane) : LayoutStep;

/// <summary>Moves the focus to a pane, by the id Windows Terminal gave it (creation order, starting at 0).</summary>
public sealed record FocusPaneStep(int PaneId) : LayoutStep;

/// <summary>
/// Turns a split tree into the sequence of Windows Terminal commands that rebuilds it.
/// Windows Terminal always splits the focused pane, and the new pane takes the focus, so:
/// split the area, build the second half (which is focused), focus the first half back, build it.
/// </summary>
public static class LayoutStepBuilder
{
    public static IReadOnlyList<LayoutStep> Build(TabLayout layout)
    {
        var steps = new List<LayoutStep> { new NewTabStep(layout.PrimaryPane) };
        var ids = new Dictionary<PaneLeaf, int>(ReferenceEqualityComparer.Instance);
        var nextId = 1;
        var focused = 0;

        BuildNode(layout.Root, 0);

        var focusPane = layout.Leaves().FirstOrDefault(l => l.FocusOnStart);
        if (focusPane is not null) steps.Add(new FocusPaneStep(ids[focusPane]));

        return steps;

        void BuildNode(LayoutNode node, int paneId)
        {
            if (node is PaneLeaf leaf)
            {
                ids[leaf] = paneId;
                return;
            }

            var split = (SplitNode)node;
            if (focused != paneId)
            {
                steps.Add(new FocusPaneStep(paneId));
                focused = paneId;
            }

            steps.Add(new SplitPaneStep(split.Orientation, Math.Round(1 - split.Ratio, 3), split.Second.FirstLeaf()));
            var newId = nextId++;
            focused = newId;

            BuildNode(split.Second, newId);
            BuildNode(split.First, paneId);
        }
    }
}
