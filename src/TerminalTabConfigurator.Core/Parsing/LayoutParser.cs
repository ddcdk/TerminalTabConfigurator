using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public static TabLayout Parse(string name, JsonNode? command)
    {
        if (command is not JsonObject obj || Str(obj, "action") != "multipleActions" || obj["actions"] is not JsonArray actions)
            throw new LayoutParseException("La commande n'est pas une action « multipleActions ».");

        if (actions.Count == 0 || actions[0] is not JsonObject first || Str(first, "action") != "newTab")
            throw new LayoutParseException("La première action doit être « newTab ».");

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
                throw new LayoutParseException($"Action n°{i + 1} invalide.");

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
                        throw new LayoutParseException($"focusPane vers un volet inexistant ({id}).");
                    focused = id;
                    lastWasFocus = true;
                    break;

                case "renameTab":
                    // Tab-level title: does not move the focus.
                    renamedTitle = Str(action, "title");
                    renamed = true;
                    break;

                case var other:
                    throw new LayoutParseException($"Action « {other} » non prise en charge par l'éditeur.");
            }
        }

        if (lastWasFocus) panes[focused].FocusOnStart = true;

        var tabTitle = renamedTitle;
        if (!renamed)
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
        var pane = new PaneLeaf
        {
            Title = title,
            Profile = Str(action, "profile"),
            Commandline = Str(action, "commandline"),
            StartingDirectory = Str(action, "startingDirectory"),
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
        _ => throw new LayoutParseException($"Direction de division inconnue : « {split} »."),
    };

    private static string? Str(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

public sealed class LayoutParseException(string message) : Exception(message);
