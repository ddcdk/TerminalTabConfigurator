// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;
using TerminalTabConfigurator.Core.Templates;

namespace TerminalTabConfigurator.Core.Tests;

public class WtCommandLineParserTests
{
    private const string Pwsh = "{574e775e-4f2a-5b96-ac1e-a2962a402336}";

    /// <summary>A profile written by hand from the "Generated command" of a layout.</summary>
    private const string GuikProfile =
        "wt.exe -w 0 nt --tabColor #60CDFF -p {574e775e-4f2a-5b96-ac1e-a2962a402336} --title Backend -d C:\\GitRepository\\GuiK\\backend" +
        " ; sp -V -s 0.43 -p {574e775e-4f2a-5b96-ac1e-a2962a402336} --title Helm -d C:\\GitRepository\\GuiK\\helm" +
        " ; fp -t 0" +
        " ; sp -H -s 0.34 -p {574e775e-4f2a-5b96-ac1e-a2962a402336} --title Fronted -d C:\\GitRepository\\GuiK\\ui" +
        " ; fp -t 0";

    [Fact]
    public void Imports_a_multi_pane_profile()
    {
        var layout = WtCommandLineParser.Parse("Multi-Pane Terminal", GuikProfile);

        Assert.Equal("Multi-Pane Terminal", layout.Name);
        Assert.Null(layout.ActionId);
        Assert.Equal("#60CDFF", layout.TabColor);
        Assert.Null(layout.TabTitle);

        // Backend on the left, split in two (Backend above Fronted), Helm on the right.
        var root = Assert.IsType<SplitNode>(layout.Root);
        Assert.Equal(SplitOrientation.Vertical, root.Orientation);
        Assert.Equal(0.57, root.Ratio, 3);
        var left = Assert.IsType<SplitNode>(root.First);
        Assert.Equal(SplitOrientation.Horizontal, left.Orientation);
        Assert.Equal(0.66, left.Ratio, 3);

        var panes = layout.Leaves().ToList();
        Assert.Equal(["Backend", "Fronted", "Helm"], panes.Select(p => p.Title));
        Assert.Equal([@"C:\GitRepository\GuiK\backend", @"C:\GitRepository\GuiK\ui", @"C:\GitRepository\GuiK\helm"], panes.Select(p => p.StartingDirectory));
        Assert.All(panes, p => Assert.Equal(Pwsh, p.Profile));
        Assert.All(panes, p => Assert.Null(p.Commandline));
        Assert.True(panes[0].FocusOnStart);
    }

    [Fact]
    public void Imported_profile_generates_the_same_command()
    {
        var layout = WtCommandLineParser.Parse("Guik", GuikProfile);

        Assert.Equal(GuikProfile, "wt.exe " + WtCommandLineBuilder.BuildArguments(layout));
    }

    [Fact]
    public void Generated_commands_round_trip()
    {
        var layouts = LayoutTemplates.All.Select(t => t.Create(t.Id, @"C:\Program Files\Work", "{guid}")).Append(GenerationTests.DevWeb());
        foreach (var layout in layouts)
        {
            var args = WtCommandLineBuilder.BuildArguments(layout);
            var imported = WtCommandLineParser.Parse(layout.Name, "wt.exe " + args);
            Assert.Equal(args, WtCommandLineBuilder.BuildArguments(imported));
        }
    }

    [Fact]
    public void A_title_shared_by_every_pane_is_the_tab_title()
    {
        var layout = WtCommandLineParser.Parse("x", "wt nt --title Dev -d C:\\a ; sp -V --title Dev -d C:\\b");

        Assert.Equal("Dev", layout.TabTitle);
        Assert.All(layout.Leaves(), p => Assert.Null(p.Title));
    }

    [Fact]
    public void Reads_pane_command_lines_with_quotes_and_escaped_semicolons()
    {
        var layout = WtCommandLineParser.Parse("x",
            "\"C:\\Users\\me\\AppData\\Local\\Microsoft\\WindowsApps\\wt.exe\" new-tab --title=\"Mon titre\" pwsh -NoExit -Command \"npm run dev\" ; " +
            "split-pane --horizontal --size 0.3 -d \"C:\\Program Files\" pwsh -c \"a\\; b\"");

        var panes = layout.Leaves().ToList();
        Assert.Equal("pwsh -NoExit -Command \"npm run dev\"", panes[0].Commandline);
        Assert.Equal("Mon titre", panes[0].Title);
        Assert.Equal("pwsh -c \"a; b\"", panes[1].Commandline);
        Assert.Equal(@"C:\Program Files", panes[1].StartingDirectory);
        Assert.Equal(SplitOrientation.Horizontal, Assert.IsType<SplitNode>(layout.Root).Orientation);
    }

    [Fact]
    public void Without_subcommand_the_arguments_are_a_new_tab()
    {
        var layout = WtCommandLineParser.Parse("x", "wt.exe -M -p PowerShell ; sp cmd.exe");

        var panes = layout.Leaves().ToList();
        Assert.Equal("PowerShell", panes[0].Profile);
        Assert.Equal("cmd.exe", panes[1].Commandline);
    }

    [Theory]
    [InlineData("pwsh.exe -NoExit", LayoutParseError.NotWtCommand)]
    [InlineData("wt.exe nt ; mf left", LayoutParseError.UnsupportedAction)]
    [InlineData("wt.exe nt --unknown", LayoutParseError.UnsupportedOption)]
    [InlineData("wt.exe nt -p", LayoutParseError.MissingOptionValue)]
    [InlineData("wt.exe nt ; sp -s 2", LayoutParseError.InvalidOptionValue)]
    [InlineData("wt.exe nt ; fp -t 5", LayoutParseError.FocusUnknownPane)]
    public void Reports_what_cannot_be_imported(string commandline, LayoutParseError error)
    {
        var ex = Assert.Throws<LayoutParseException>(() => WtCommandLineParser.Parse("x", commandline));
        Assert.Equal(error, ex.Error);
    }

    [Fact]
    public void Profiles_that_start_wt_are_layouts()
    {
        var doc = TerminalSettingsDocument.Parse($$"""
            { "profiles": { "list": [
                { "name": "Multi-Pane Terminal", "guid": "{574e775e-4f2a-5b96-ac1e-a2962a402258}", "icon": "🚀", "commandline": "{{GuikProfile.Replace("\\", "\\\\")}}" },
                { "name": "PowerShell", "guid": "{{Pwsh}}", "source": "Windows.Terminal.PowershellCore" },
                { "name": "cmd", "commandline": "%SystemRoot%\\System32\\cmd.exe" }
            ] } }
            """);

        var profile = Assert.Single(doc.GetProfiles(), p => p.IsLayout);
        Assert.Equal("Multi-Pane Terminal", profile.Name);
        Assert.Equal("🚀", profile.Icon);
    }
}
