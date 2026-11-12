// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Generation;

/// <summary>Builds the equivalent <c>wt.exe</c> arguments (for testing and desktop shortcuts).</summary>
public static class WtCommandLineBuilder
{
    /// <summary>Window targeting the most recent window (a new one if none is open).</summary>
    public const string CurrentWindow = "0";

    /// <summary>Window targeting a brand new window.</summary>
    public const string NewWindow = "new";

    public static string BuildArguments(TabLayout layout, string window = CurrentWindow)
    {
        var commands = new List<string>();
        foreach (var step in LayoutStepBuilder.Build(layout))
        {
            var sb = new StringBuilder();
            switch (step)
            {
                case NewTabStep s:
                    sb.Append("nt");
                    if (layout.TabColor is not null) sb.Append(" --tabColor ").Append(Quote(layout.TabColor));
                    // The command line has no renameTab: untitled panes carry the tab title instead.
                    AppendPaneArgs(sb, s.Pane, s.Pane.Title ?? layout.TabTitle);
                    break;

                case SplitPaneStep s:
                    sb.Append("sp ")
                      .Append(s.Orientation == SplitOrientation.Vertical ? "-V" : "-H")
                      .Append(" -s ")
                      .Append(JsonActionBuilder.FormatSize(s.Size));
                    AppendPaneArgs(sb, s.Pane, s.Pane.Title ?? layout.TabTitle);
                    break;

                case FocusPaneStep s:
                    sb.Append("fp -t ").Append(s.PaneId);
                    break;
            }

            commands.Add(sb.ToString());
        }

        return $"-w {window} " + string.Join(" ; ", commands);
    }

    public static string BuildDisplay(TabLayout layout) => "wt.exe " + BuildArguments(layout);

    private static void AppendPaneArgs(StringBuilder sb, PaneLeaf pane, string? title)
    {
        if (pane.Profile is not null) sb.Append(" -p ").Append(Quote(pane.Profile));
        if (title is not null) sb.Append(" --title ").Append(Quote(EscapeSemicolons(title)));
        if (pane.StartingDirectory is not null) sb.Append(" -d ").Append(Quote(pane.StartingDirectory));
        if (pane.Commandline is not null) sb.Append(' ').Append(EscapeSemicolons(pane.Commandline));
    }

    /// <summary>wt.exe uses <c>;</c> to separate commands; a literal one must be written <c>\;</c>.</summary>
    private static string EscapeSemicolons(string value) => value.Replace(";", "\\;");

    /// <summary>Quotes one argument following the Windows command-line rules (backslashes before a quote are doubled).</summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"', ';']) < 0) return value;

        var sb = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(c);
            }

            backslashes = 0;
        }

        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }
}
