// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Globalization;
using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Generation;

/// <summary>Builds the <c>multipleActions</c> command stored in settings.json.</summary>
public static class JsonActionBuilder
{
    public static JsonObject BuildCommand(TabLayout layout)
    {
        var actions = new JsonArray();
        foreach (var step in LayoutStepBuilder.Build(layout))
        {
            switch (step)
            {
                case NewTabStep s:
                    var newTab = new JsonObject { ["action"] = "newTab" };
                    AddPaneArgs(newTab, s.Pane, s.Pane.Title);
                    if (layout.TabColor is not null) newTab["tabColor"] = layout.TabColor;
                    actions.Add(newTab);
                    break;

                case SplitPaneStep s:
                    var split = new JsonObject
                    {
                        ["action"] = "splitPane",
                        ["split"] = s.Orientation == SplitOrientation.Vertical ? "right" : "down",
                        ["size"] = JsonValue.Create(s.Size),
                    };
                    AddPaneArgs(split, s.Pane, s.Pane.Title);
                    actions.Add(split);
                    break;

                case FocusPaneStep s:
                    actions.Add(new JsonObject { ["action"] = "focusPane", ["id"] = s.PaneId });
                    break;
            }
        }

        // In Windows Terminal a pane title ("tabTitle") becomes the tab title while that pane is
        // focused. renameTab sets a tab-level title instead, so the first pane keeps its own.
        if (layout.TabTitle is not null)
            actions.Add(new JsonObject { ["action"] = "renameTab", ["title"] = layout.TabTitle });

        return new JsonObject
        {
            ["action"] = "multipleActions",
            ["actions"] = actions,
        };
    }

    private static void AddPaneArgs(JsonObject target, PaneLeaf pane, string? title)
    {
        if (pane.Profile is not null) target["profile"] = pane.Profile;
        if (pane.Commandline is not null) target["commandline"] = pane.Commandline;
        if (pane.StartingDirectory is not null) target["startingDirectory"] = pane.StartingDirectory;
        if (title is not null) target["tabTitle"] = title;

        foreach (var (key, value) in pane.ExtraArgs)
            if (!target.ContainsKey(key)) target[key] = value?.DeepClone();
    }

    internal static string FormatSize(double size) => size.ToString("0.###", CultureInfo.InvariantCulture);
}
