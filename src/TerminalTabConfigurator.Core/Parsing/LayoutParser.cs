// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Parsing;

/// <summary>
/// Rebuilds a split tree from a <c>multipleActions</c> command by replaying it the way
/// Windows Terminal does: each split divides the focused pane and focuses the new one.
/// </summary>
public static class LayoutParser
{
    private static readonly HashSet<string> PaneKeys = ["action", "profile", "commandline", "startingDirectory", "tabTitle"];
    private static readonly HashSet<string> NewTabKeys = [.. PaneKeys, "tabColor"];
    private static readonly HashSet<string> SplitKeys = [.. PaneKeys, "split", "size"];

    public static TabLayout Parse(string name, JsonNode? command) => Parse(name, command, legacyTitles: true);

    /// <param name="legacyTitles">Without <c>renameTab</c>, read the first pane's title as the tab title (format of the first versions).</param>
    internal static TabLayout Parse(string name, JsonNode? command, bool legacyTitles)
    {
        if (command is not JsonObject obj || Str(obj, "action") != "multipleActions" || obj["actions"] is not JsonArray actions)
            throw new LayoutParseException(LayoutParseError.NotMultipleActions);

        if (actions.Count == 0 || actions[0] is not JsonObject first || Str(first, "action") != "newTab")
            throw new LayoutParseException(LayoutParseError.FirstNotNewTab);

        var primary = ReadPane(first, NewTabKeys, Str(first, "tabTitle"));
        string? renamedTitle = null;
        var renamed = false;
        var root = (LayoutNode)primary;
        var panes = new List<PaneLeaf> { primary };
        var focused = 0;
        var lastWasFocus = false;

        for (var i = 1; i < actions.Count; i++)
        {
            if (actions[i] is not JsonObject action)
                throw new LayoutParseException(LayoutParseError.InvalidAction, (i + 1).ToString(CultureInfo.InvariantCulture));

            switch (Str(action, "action"))
            {
                case "splitPane":
                    var target = panes[focused];
                    var pane = ReadPane(action, SplitKeys, Str(action, "tabTitle"));
                    var size = ReadSize(action);
                    var (orientation, newFirst) = ReadDirection(Str(action, "split"));
                    var split = newFirst
                        ? new SplitNode(orientation, pane, target, size)
                        : new SplitNode(orientation, target, pane, 1 - size);

                    var parent = target.Parent;
                    if (parent is null) root = split;
                    else parent.Replace(target, split);
                    split.Attach(null, parent);

                    panes.Add(pane);
                    focused = panes.Count - 1;
                    lastWasFocus = false;
                    break;

                case "focusPane":
                    var id = action["id"]?.GetValue<int>() ?? -1;
                    if (id < 0 || id >= panes.Count)
                        throw new LayoutParseException(LayoutParseError.FocusUnknownPane, id.ToString(CultureInfo.InvariantCulture));
                    focused = id;
                    lastWasFocus = true;
                    break;

                case "renameTab":
                    // Tab-level title: does not move the focus.
                    renamedTitle = Str(action, "title");
                    renamed = true;
                    break;

                case var other:
                    throw new LayoutParseException(LayoutParseError.UnsupportedAction, other);
            }
        }

        if (lastWasFocus) panes[focused].FocusOnStart = true;

        var tabTitle = renamedTitle;
        if (!renamed && legacyTitles)
        {
            // Older format, without renameTab: the first pane's title was the tab title and
            // untitled panes repeated it.
            tabTitle = primary.Title;
            primary.Title = null;
            foreach (var pane in panes.Where(p => p.Title == tabTitle)) pane.Title = null;
        }

        return new TabLayout(name, root)
        {
            TabTitle = tabTitle,
            TabColor = Str(first, "tabColor"),
        };
    }

    private static PaneLeaf ReadPane(JsonObject action, HashSet<string> knownKeys, string? title)
    {
        var (commandline, prompt, customPrompt) = PowerShellPrompt.Extract(Str(action, "commandline"));
        var pane = new PaneLeaf
        {
            Title = title,
            Profile = Str(action, "profile"),
            Commandline = commandline,
            StartingDirectory = Str(action, "startingDirectory"),
            Prompt = prompt,
            CustomPrompt = customPrompt,
        };

        foreach (var (key, value) in action)
            if (!knownKeys.Contains(key)) pane.ExtraArgs[key] = value?.DeepClone();

        return pane;
    }

    private static double ReadSize(JsonObject action)
    {
        var node = action["size"];
        if (node is null) return 0.5;
        return node.GetValueKind() switch
        {
            JsonValueKind.Number => node.GetValue<double>(),
            JsonValueKind.String when double.TryParse(node.GetValue<string>(), CultureInfo.InvariantCulture, out var d) => d,
            _ => 0.5,
        };
    }

    /// <summary>Returns the orientation and whether the new pane comes first (left/up).</summary>
    private static (SplitOrientation, bool) ReadDirection(string? split) => split?.ToLowerInvariant() switch
    {
        null or "auto" or "right" or "vertical" => (SplitOrientation.Vertical, false),
        "left" => (SplitOrientation.Vertical, true),
        "down" or "horizontal" => (SplitOrientation.Horizontal, false),
        "up" => (SplitOrientation.Horizontal, true),
        _ => throw new LayoutParseException(LayoutParseError.UnknownSplitDirection, split),
    };

    private static string? Str(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

public enum LayoutParseError
{
    NotMultipleActions,
    FirstNotNewTab,
    InvalidAction,
    FocusUnknownPane,
    UnsupportedAction,
    UnknownSplitDirection,

    /// <summary>The command line does not start <c>wt.exe</c>.</summary>
    NotWtCommand,

    /// <summary>A <c>wt.exe</c> option the editor does not know; <see cref="LayoutParseException.Detail"/> holds it.</summary>
    UnsupportedOption,

    /// <summary>A <c>wt.exe</c> option without its value.</summary>
    MissingOptionValue,

    /// <summary>A <c>wt.exe</c> option with an invalid value.</summary>
    InvalidOptionValue,

    /// <summary>Any other reading error; <see cref="LayoutParseException.Detail"/> holds its message.</summary>
    Invalid,
}

/// <summary>An action that the editor cannot turn back into a layout. The UI localizes it from <see cref="Error"/> and <see cref="Detail"/>.</summary>
public sealed class LayoutParseException(LayoutParseError error, string? detail = null) : Exception(Describe(error, detail))
{
    public LayoutParseError Error { get; } = error;

    public string? Detail { get; } = detail;

    private static string Describe(LayoutParseError error, string? detail) => error switch
    {
        LayoutParseError.NotMultipleActions => "The command is not a \"multipleActions\" action.",
        LayoutParseError.FirstNotNewTab => "The first action must be \"newTab\".",
        LayoutParseError.InvalidAction => $"Action #{detail} is invalid.",
        LayoutParseError.FocusUnknownPane => $"focusPane targets a pane that does not exist ({detail}).",
        LayoutParseError.UnsupportedAction => $"Action \"{detail}\" is not supported by the editor.",
        LayoutParseError.UnknownSplitDirection => $"Unknown split direction: \"{detail}\".",
        LayoutParseError.NotWtCommand => "The command line does not start wt.exe.",
        LayoutParseError.UnsupportedOption => $"Option \"{detail}\" is not supported by the editor.",
        LayoutParseError.MissingOptionValue => $"Option \"{detail}\" has no value.",
        LayoutParseError.InvalidOptionValue => $"Invalid value: \"{detail}\".",
        _ => detail ?? "Invalid layout.",
    };
}
