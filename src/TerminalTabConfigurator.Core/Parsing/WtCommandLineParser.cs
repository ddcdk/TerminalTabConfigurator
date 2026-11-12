// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Parsing;

/// <summary>
/// Reads a layout back from a <c>wt.exe</c> command line, such as the <c>commandline</c> of a
/// profile that opens several panes (<c>wt.exe -w 0 nt … ; sp … ; fp …</c>). The command line is
/// turned into the equivalent <c>multipleActions</c> and replayed by <see cref="LayoutParser"/>.
/// </summary>
public static class WtCommandLineParser
{
    /// <summary>True when the command line starts <c>wt</c> / <c>wt.exe</c>.</summary>
    public static bool IsWtCommand(string? commandline)
    {
        if (string.IsNullOrWhiteSpace(commandline)) return false;
        var args = SplitArguments(commandline);
        return args.Count > 0 && IsWtExecutable(args[0]);
    }

    public static TabLayout Parse(string name, string commandline)
    {
        var args = SplitArguments(commandline);
        if (args.Count == 0 || !IsWtExecutable(args[0])) throw new LayoutParseException(LayoutParseError.NotWtCommand);

        var commands = SplitCommands(args.Skip(1));
        var actions = new JsonArray();
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (i == 0) command = SkipGlobalOptions(command);
            if (command.Count == 0) continue;
            actions.Add(ReadCommand(command));
        }

        var layout = LayoutParser.Parse(name, new JsonObject { ["action"] = "multipleActions", ["actions"] = actions }, legacyTitles: false);

        // The generator gives untitled panes the tab title: one title shared by every pane is the tab title.
        var titles = layout.Leaves().Select(p => p.Title).Distinct().ToList();
        if (titles is [{ } common])
        {
            layout.TabTitle = common;
            foreach (var pane in layout.Leaves()) pane.Title = null;
        }

        return layout;
    }

    // ---------- Commands ----------

    private static JsonObject ReadCommand(List<string> args)
    {
        var position = 0;
        var subcommand = args[0];
        JsonObject action;
        switch (subcommand)
        {
            case "nt" or "new-tab":
                position = 1;
                action = new JsonObject { ["action"] = "newTab" };
                break;

            case "sp" or "split-pane":
                position = 1;
                action = new JsonObject { ["action"] = "splitPane" };
                break;

            case "fp" or "focus-pane":
                return ReadFocusPane(args);

            case "ft" or "focus-tab" or "mf" or "move-focus" or "mp" or "move-pane" or "swap-pane":
                throw new LayoutParseException(LayoutParseError.UnsupportedAction, subcommand);

            default:
                // No subcommand: wt.exe treats the arguments as a new tab.
                action = new JsonObject { ["action"] = "newTab" };
                break;
        }

        var isSplit = (string?)action["action"] == "splitPane";
        while (position < args.Count)
        {
            var (option, inlineValue) = SplitOption(args[position]);
            if (option == "--")
            {
                position++;
                break;
            }

            if (!option.StartsWith('-')) break; // the command line of the pane starts here
            position++;

            switch (option)
            {
                case "-p" or "--profile":
                    action["profile"] = Value(option, inlineValue, args, ref position);
                    break;
                case "-d" or "--startingDirectory":
                    action["startingDirectory"] = Value(option, inlineValue, args, ref position);
                    break;
                case "--title":
                    action["tabTitle"] = Value(option, inlineValue, args, ref position);
                    break;
                case "--tabColor":
                    action["tabColor"] = Value(option, inlineValue, args, ref position);
                    break;
                case "--colorScheme":
                    action["colorScheme"] = Value(option, inlineValue, args, ref position);
                    break;
                case "--suppressApplicationTitle":
                    action["suppressApplicationTitle"] = true;
                    break;
                case "-H" or "--horizontal" when isSplit:
                    action["split"] = "down";
                    break;
                case "-V" or "--vertical" when isSplit:
                    action["split"] = "right";
                    break;
                case "-D" or "--duplicate" when isSplit:
                    action["splitMode"] = "duplicate";
                    break;
                case "-s" or "--size" when isSplit:
                    var size = Value(option, inlineValue, args, ref position);
                    if (!double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) || ratio <= 0 || ratio >= 1)
                        throw new LayoutParseException(LayoutParseError.InvalidOptionValue, $"{option} {size}");
                    action["size"] = ratio;
                    break;
                default:
                    throw new LayoutParseException(LayoutParseError.UnsupportedOption, option);
            }
        }

        if (position < args.Count)
            action["commandline"] = string.Join(' ', args.Skip(position).Select(WtCommandLineBuilder.Quote));

        return action;
    }

    private static JsonObject ReadFocusPane(List<string> args)
    {
        string? target = null;
        for (var position = 1; position < args.Count;)
        {
            var (option, inlineValue) = SplitOption(args[position++]);
            if (option is not ("-t" or "--target")) throw new LayoutParseException(LayoutParseError.UnsupportedOption, option);
            target = Value(option, inlineValue, args, ref position);
        }

        if (target is null) throw new LayoutParseException(LayoutParseError.MissingOptionValue, "focus-pane --target");
        if (!int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new LayoutParseException(LayoutParseError.InvalidOptionValue, $"--target {target}");

        return new JsonObject { ["action"] = "focusPane", ["id"] = id };
    }

    /// <summary>Drops the window options that may come before the first subcommand (<c>-w 0</c>, <c>--maximized</c>…).</summary>
    private static List<string> SkipGlobalOptions(List<string> args)
    {
        var position = 0;
        while (position < args.Count)
        {
            var (option, inlineValue) = SplitOption(args[position]);
            switch (option)
            {
                case "-w" or "--window" or "--pos" or "--size":
                    position++;
                    if (inlineValue is null) Value(option, null, args, ref position);
                    break;
                case "-M" or "--maximized" or "-F" or "--fullscreen" or "-f" or "--focus":
                    position++;
                    break;
                default:
                    return args.GetRange(position, args.Count - position);
            }
        }

        return [];
    }

    private static (string Option, string? InlineValue) SplitOption(string arg)
    {
        if (!arg.StartsWith("--", StringComparison.Ordinal)) return (arg, null);
        var equals = arg.IndexOf('=');
        return equals < 0 ? (arg, null) : (arg[..equals], arg[(equals + 1)..]);
    }

    private static string Value(string option, string? inlineValue, List<string> args, ref int position)
    {
        if (inlineValue is not null) return inlineValue;
        if (position >= args.Count) throw new LayoutParseException(LayoutParseError.MissingOptionValue, option);
        return args[position++];
    }

    // ---------- Tokenizing ----------

    private static bool IsWtExecutable(string arg)
    {
        var file = Path.GetFileName(arg.Trim());
        return file.Equals("wt", StringComparison.OrdinalIgnoreCase) || file.Equals("wt.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits the arguments into wt.exe commands the way Windows Terminal does: on every <c>;</c>,
    /// except an escaped <c>\;</c>, which stays a literal <c>;</c>.
    /// </summary>
    private static List<List<string>> SplitCommands(IEnumerable<string> args)
    {
        var commands = new List<List<string>> { new() };
        foreach (var arg in args)
        {
            var current = new StringBuilder();
            for (var i = 0; i < arg.Length; i++)
            {
                if (arg[i] == '\\' && i + 1 < arg.Length && arg[i + 1] == ';')
                {
                    current.Append(';');
                    i++;
                }
                else if (arg[i] == ';')
                {
                    if (current.Length > 0) commands[^1].Add(current.ToString());
                    current.Clear();
                    commands.Add([]);
                }
                else
                {
                    current.Append(arg[i]);
                }
            }

            if (current.Length > 0) commands[^1].Add(current.ToString());
        }

        return commands;
    }

    /// <summary>Splits a command line into arguments following the Windows rules (the inverse of <see cref="WtCommandLineBuilder.Quote"/>).</summary>
    internal static List<string> SplitArguments(string commandline)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasArg = false;

        for (var i = 0; i < commandline.Length; i++)
        {
            var c = commandline[i];
            if (c == '\\')
            {
                var start = i;
                while (i < commandline.Length && commandline[i] == '\\') i++;
                var count = i - start;
                if (i < commandline.Length && commandline[i] == '"')
                {
                    current.Append('\\', count / 2);
                    if (count % 2 == 1) current.Append('"');
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else
                {
                    current.Append('\\', count);
                    i--;
                }

                hasArg = true;
            }
            else if (c == '"')
            {
                if (inQuotes && i + 1 < commandline.Length && commandline[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                hasArg = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasArg) args.Add(current.ToString());
                current.Clear();
                hasArg = false;
            }
            else
            {
                current.Append(c);
                hasArg = true;
            }
        }

        if (hasArg) args.Add(current.ToString());
        return args;
    }
}
