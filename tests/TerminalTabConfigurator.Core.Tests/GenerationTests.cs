// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Templates;

namespace TerminalTabConfigurator.Core.Tests;

public class GenerationTests
{
    /// <summary>The example from the brief: vertical split, right half split horizontally.</summary>
    internal static TabLayout DevWeb()
    {
        var front = new PaneLeaf { Commandline = "pwsh.exe", StartingDirectory = @"C:\Dev\webapp\front" };
        var api = new PaneLeaf { Title = "API", Commandline = "pwsh.exe", StartingDirectory = @"C:\Dev\webapp\api" };
        var logs = new PaneLeaf { Title = "Logs", Commandline = "pwsh.exe -NoExit -Command \"npm run logs\"", StartingDirectory = @"C:\Dev\webapp\logs" };
        var root = new SplitNode(SplitOrientation.Vertical, front, new SplitNode(SplitOrientation.Horizontal, api, logs, 0.6));
        return new TabLayout("Dev Web", root) { TabTitle = "Dev Web", TabColor = "#C18FFF" };
    }

    [Fact]
    public void DevWeb_generates_newTab_then_two_splits()
    {
        var command = JsonActionBuilder.BuildCommand(DevWeb());

        var expected = JsonNode.Parse("""
            {
              "action": "multipleActions",
              "actions": [
                { "action": "newTab", "commandline": "pwsh.exe", "startingDirectory": "C:\\Dev\\webapp\\front", "tabColor": "#C18FFF" },
                { "action": "splitPane", "split": "right", "size": 0.5, "commandline": "pwsh.exe", "startingDirectory": "C:\\Dev\\webapp\\api", "tabTitle": "API" },
                { "action": "splitPane", "split": "down", "size": 0.4, "commandline": "pwsh.exe -NoExit -Command \"npm run logs\"", "startingDirectory": "C:\\Dev\\webapp\\logs", "tabTitle": "Logs" },
                { "action": "renameTab", "title": "Dev Web" }
              ]
            }
            """);

        Assert.True(JsonNode.DeepEquals(expected, command), command.ToJsonString());
    }

    [Fact]
    public void DevWeb_generates_wt_command_line()
    {
        var args = WtCommandLineBuilder.BuildArguments(DevWeb());

        Assert.Equal(
            "-w 0 nt --tabColor #C18FFF --title \"Dev Web\" -d C:\\Dev\\webapp\\front pwsh.exe" +
            " ; sp -V -s 0.5 --title API -d C:\\Dev\\webapp\\api pwsh.exe" +
            " ; sp -H -s 0.4 --title Logs -d C:\\Dev\\webapp\\logs pwsh.exe -NoExit -Command \"npm run logs\"",
            args);
    }

    [Fact]
    public void Grid_refocuses_first_half_before_splitting_it()
    {
        var steps = LayoutStepBuilder.Build(LayoutTemplates.All.Single(t => t.Id == "grid-2x2").Create("G", null, null));

        Assert.Collection(steps,
            s => Assert.IsType<NewTabStep>(s),
            s => Assert.Equal(SplitOrientation.Vertical, Assert.IsType<SplitPaneStep>(s).Orientation),
            s => Assert.Equal(SplitOrientation.Horizontal, Assert.IsType<SplitPaneStep>(s).Orientation),
            s => Assert.Equal(0, Assert.IsType<FocusPaneStep>(s).PaneId),
            s => Assert.Equal(SplitOrientation.Horizontal, Assert.IsType<SplitPaneStep>(s).Orientation));
    }

    public static TheoryData<string> TemplateNames() => [.. LayoutTemplates.All.Select(t => t.Id)];

    [Theory]
    [MemberData(nameof(TemplateNames))]
    public void Every_template_round_trips_through_settings_json(string templateName)
    {
        var layout = LayoutTemplates.All.Single(t => t.Id == templateName).Create("T", @"C:\Work", "{guid}");
        var i = 0;
        foreach (var pane in layout.Leaves()) pane.Commandline = $"cmd{++i}.exe";

        var command = JsonActionBuilder.BuildCommand(layout);
        var reread = LayoutParser.Parse("T", JsonNode.Parse(command.ToJsonString()));

        Assert.True(JsonNode.DeepEquals(command, JsonActionBuilder.BuildCommand(reread)));
        Assert.Equal(Describe(layout.Root), Describe(reread.Root));
    }

    [Fact]
    public void Focus_on_start_is_kept_after_round_trip()
    {
        var layout = DevWeb();
        layout.Leaves().First().FocusOnStart = true;

        var reread = LayoutParser.Parse("x", JsonActionBuilder.BuildCommand(layout));

        Assert.True(reread.Leaves().First().FocusOnStart);
        Assert.Equal(1, reread.Leaves().Count(l => l.FocusOnStart));
    }

    [Fact]
    public void Unknown_pane_arguments_are_preserved()
    {
        var command = JsonNode.Parse("""
            { "action": "multipleActions", "actions": [
                { "action": "newTab", "profile": "Ubuntu", "colorScheme": "Campbell" },
                { "action": "splitPane", "split": "left", "size": 0.3, "suppressApplicationTitle": true } ] }
            """);

        var layout = LayoutParser.Parse("x", command);
        var root = Assert.IsType<SplitNode>(layout.Root);

        Assert.Equal(0.3, root.Ratio);
        Assert.Equal("Ubuntu", ((PaneLeaf)root.Second).Profile);
        var regenerated = JsonActionBuilder.BuildCommand(layout).ToJsonString();
        Assert.Contains("\"colorScheme\":\"Campbell\"", regenerated);
        Assert.Contains("\"suppressApplicationTitle\":true", regenerated);
    }

    [Fact]
    public void Unsupported_action_is_reported()
    {
        var command = JsonNode.Parse("""
            { "action": "multipleActions", "actions": [ { "action": "newTab" }, { "action": "moveFocus", "direction": "left" } ] }
            """);

        var ex = Assert.Throws<LayoutParseException>(() => LayoutParser.Parse("x", command));
        Assert.Equal(LayoutParseError.UnsupportedAction, ex.Error);
        Assert.Equal("moveFocus", ex.Detail);
    }

    [Theory]
    [InlineData("C:\\Dev", "C:\\Dev")]
    [InlineData("C:\\My Dev\\", "\"C:\\My Dev\\\\\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a;b", "\"a;b\"")]
    public void Quote_follows_windows_rules(string input, string expected) =>
        Assert.Equal(expected, WtCommandLineBuilder.Quote(input));

    [Fact]
    public void Semicolons_in_commandline_are_escaped_for_wt()
    {
        var layout = new TabLayout("x", new PaneLeaf { Commandline = "pwsh -c \"a; b\"" });

        Assert.Equal("-w 0 nt pwsh -c \"a\\; b\"", WtCommandLineBuilder.BuildArguments(layout));
    }

    [Fact]
    public void Text_fields_keep_a_trailing_space_while_typing()
    {
        // WPF reads the value back after each keystroke: a trimming setter would swallow the space.
        var pane = new PaneLeaf { Commandline = "pwsh ", Title = "Mon ", StartingDirectory = @"C:\Program " };
        var layout = new TabLayout("x", pane) { Name = "Dev ", TabTitle = "Mon " };

        Assert.Equal("pwsh ", pane.Commandline);
        Assert.Equal("Mon ", pane.Title);
        Assert.Equal(@"C:\Program ", pane.StartingDirectory);
        Assert.Equal("Dev ", layout.Name);
        Assert.Equal("Mon ", layout.TabTitle);
        Assert.Null(new PaneLeaf { Commandline = "   " }.Commandline);
    }

    [Fact]
    public void Generated_commands_and_published_layouts_are_trimmed()
    {
        var pane = new PaneLeaf { Commandline = " pwsh -NoExit c:\\monscript.ps1 ", Title = " Logs ", StartingDirectory = " C:\\Dev " };
        var layout = new TabLayout(" Dev web ", pane) { TabTitle = " Web ", TabColor = " #C18FFF " };

        Assert.Equal("-w 0 nt --tabColor #C18FFF --title Logs -d C:\\Dev pwsh -NoExit c:\\monscript.ps1",
            WtCommandLineBuilder.BuildArguments(layout));

        var actions = JsonActionBuilder.BuildCommand(layout)["actions"]!.AsArray();
        Assert.Equal("pwsh -NoExit c:\\monscript.ps1", (string?)actions[0]!["commandline"]);
        Assert.Equal("Logs", (string?)actions[0]!["tabTitle"]);
        Assert.Equal("C:\\Dev", (string?)actions[0]!["startingDirectory"]);
        Assert.Equal("#C18FFF", (string?)actions[0]!["tabColor"]);
        Assert.Equal("Web", (string?)actions[^1]!["title"]);

        layout.TrimText();
        Assert.Equal("Dev web", layout.Name);
        Assert.Equal("Web", layout.TabTitle);
        Assert.Equal("#C18FFF", layout.TabColor);
        Assert.Equal("pwsh -NoExit c:\\monscript.ps1", pane.Commandline);
        Assert.Equal("Logs", pane.Title);
        Assert.Equal("C:\\Dev", pane.StartingDirectory);
    }

    [Fact]
    public void Split_remove_and_equalize_keep_tree_consistent()
    {
        var first = new PaneLeaf();
        var layout = new TabLayout("x", first);

        var second = layout.Split(first, SplitOrientation.Vertical);
        var third = layout.Split(second, SplitOrientation.Vertical);
        layout.Equalize();

        var root = Assert.IsType<SplitNode>(layout.Root);
        Assert.Equal(0.333, root.Ratio);
        Assert.Equal([1, 2, 3], layout.Leaves().Select(l => l.Number));

        var next = layout.Remove(second);
        Assert.Same(third, next);
        Assert.Equal(2, layout.Leaves().Count());

        layout.Remove(third);
        Assert.Same(first, layout.Root);
        Assert.Null(first.Parent);
        Assert.Null(layout.Remove(first)); // the last pane cannot be removed
        Assert.Same(first, layout.Root);
    }

    private static string Describe(LayoutNode node) => node switch
    {
        PaneLeaf p => p.Commandline ?? "?",
        SplitNode s => $"{s.Orientation}({s.Ratio:0.###}:{Describe(s.First)},{Describe(s.Second)})",
        _ => throw new InvalidOperationException(),
    };
}
