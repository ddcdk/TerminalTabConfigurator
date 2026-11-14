// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text;
using System.Text.RegularExpressions;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Core.Generation;

/// <summary>
/// Replaces the PowerShell prompt of a pane by appending <c>-NoExit -Command "function prompt { … }"</c>
/// to its command line. The function is defined after the user's <c>$PROFILE</c> has run, so it wins
/// over the default prompt (and over oh-my-posh, Starship…). Reading a layout back strips it again.
/// </summary>
public static partial class PowerShellPrompt
{
    /// <summary>Body of the <see cref="PromptStyle.FolderName"/> prompt: <c>PS front&gt; </c>.</summary>
    public const string FolderNameBody = "'PS ' + (Split-Path -Leaf $PWD.Path) + '> '";

    /// <summary>Body of the <see cref="PromptStyle.TwoLines"/> prompt: the path, then <c>&gt; </c> on the next line.</summary>
    public const string TwoLinesBody = "'PS ' + $PWD.Path + [Environment]::NewLine + '> '";

    private static readonly HashSet<string> Executables =
        new(["pwsh", "pwsh.exe", "pwsh-preview", "pwsh-preview.cmd", "powershell", "powershell.exe"], StringComparer.OrdinalIgnoreCase);

    // Switches that leave PowerShell interactive. Anything else (a script, -Command, -File…) is left alone.
    private static readonly HashSet<string> Switches =
        new(["-nologo", "-nol", "-noprofile", "-nop", "-noexit", "-noe", "-login", "-l", "-mta", "-sta", "-interactive"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NoExitSwitches = new(["-noexit", "-noe"], StringComparer.OrdinalIgnoreCase);

    // Switches followed by a value.
    private static readonly HashSet<string> ValueSwitches =
        new(["-executionpolicy", "-ep", "-ex", "-workingdirectory", "-wd", "-windowstyle", "-w"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The prompt function body of a style, or null for <see cref="PromptStyle.Default"/>.</summary>
    public static string? Body(PromptStyle style, string? customBody) => style switch
    {
        PromptStyle.FolderName => FolderNameBody,
        PromptStyle.TwoLines => TwoLinesBody,
        PromptStyle.Custom => TextValue.Clean(customBody),
        _ => null,
    };

    /// <summary>True when the command line starts an interactive PowerShell whose prompt can be replaced.</summary>
    public static bool CanApply(string? commandline)
    {
        if (string.IsNullOrWhiteSpace(commandline)) return false;
        var args = WtCommandLineParser.SplitArguments(commandline);
        if (args.Count == 0 || !IsPowerShell(args[0])) return false;

        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i].Replace('/', '-');
            if (Switches.Contains(arg)) continue;
            if (ValueSwitches.Contains(arg) && i + 1 < args.Count)
            {
                i++;
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>The command line that runs, prompt included (trimmed, null when empty).</summary>
    public static string? Commandline(PaneLeaf pane) => Apply(TextValue.Clean(pane.Commandline), pane.Prompt, pane.CustomPrompt);

    public static string? Apply(string? commandline, PromptStyle style, string? customBody)
    {
        if (Body(style, customBody) is not { } body || !CanApply(commandline)) return commandline;

        var sb = new StringBuilder(commandline);
        var hasNoExit = WtCommandLineParser.SplitArguments(commandline!).Skip(1).Any(a => NoExitSwitches.Contains(a.Replace('/', '-')));
        if (!hasNoExit) sb.Append(" -NoExit");

        var script = $"function prompt {{ {body} }}";
        // wt.exe puts the pane command line back together without escaping inner quotes: a script
        // that contains one goes encoded.
        if (script.Contains('"'))
            sb.Append(" -EncodedCommand ").Append(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        else
            sb.Append(" -Command ").Append(WtCommandLineBuilder.Quote(script));

        return sb.ToString();
    }

    /// <summary>Splits a command line written by <see cref="Apply"/> into the original command line and its prompt.</summary>
    public static (string? Commandline, PromptStyle Style, string? CustomBody) Extract(string? commandline)
    {
        if (commandline is null) return (null, PromptStyle.Default, null);

        var match = InjectedPrompt().Match(commandline);
        if (!match.Success) return (commandline, PromptStyle.Default, null);

        var baseline = match.Groups["base"].Value;
        string script;
        if (match.Groups["encoded"].Success)
        {
            try
            {
                script = Encoding.Unicode.GetString(Convert.FromBase64String(match.Groups["encoded"].Value));
            }
            catch (FormatException)
            {
                return (commandline, PromptStyle.Default, null);
            }
        }
        else
        {
            var args = WtCommandLineParser.SplitArguments(match.Groups["quoted"].Value);
            if (args.Count != 1) return (commandline, PromptStyle.Default, null);
            script = args[0];
        }

        var function = PromptFunction().Match(script);
        if (!function.Success || !CanApply(baseline)) return (commandline, PromptStyle.Default, null);

        var body = function.Groups["body"].Value;
        return body switch
        {
            FolderNameBody => (baseline, PromptStyle.FolderName, null),
            TwoLinesBody => (baseline, PromptStyle.TwoLines, null),
            _ => (baseline, PromptStyle.Custom, body),
        };
    }

    /// <summary>
    /// The PowerShell command line of a profile, for a pane that has none of its own, or null when the
    /// profile does not start a plain PowerShell (cmd, WSL, Developer PowerShell…).
    /// </summary>
    public static string? ProfileCommandline(TerminalProfile? profile)
    {
        if (profile is null) return null;
        if (profile.Commandline is { } commandline) return CanApply(commandline) ? commandline : null;
        return profile.Source == "Windows.Terminal.PowershellCore" ? "pwsh.exe" : null;
    }

    private static bool IsPowerShell(string executable)
    {
        var name = executable.Replace('/', '\\');
        var slash = name.LastIndexOf('\\');
        return Executables.Contains(slash < 0 ? name : name[(slash + 1)..]);
    }

    // The first " -Command "/" -EncodedCommand " is ours: the original command line cannot contain one (see CanApply).
    [GeneratedRegex("""^(?<base>.*?)(?:\s+-NoExit)?\s+(?:-Command\s+(?<quoted>".*")|-EncodedCommand\s+(?<encoded>[A-Za-z0-9+/=]+))\s*$""", RegexOptions.Singleline)]
    private static partial Regex InjectedPrompt();

    [GeneratedRegex(@"^function prompt \{ (?<body>.*) \}$", RegexOptions.Singleline)]
    private static partial Regex PromptFunction();
}
