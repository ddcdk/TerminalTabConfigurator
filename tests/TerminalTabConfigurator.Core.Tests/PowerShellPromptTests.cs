// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Core.Tests;

public class PowerShellPromptTests
{
    [Theory]
    [InlineData("pwsh.exe")]
    [InlineData("pwsh")]
    [InlineData("pwsh.exe -NoLogo")]
    [InlineData("pwsh.exe -NoExit -ExecutionPolicy Bypass")]
    [InlineData("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\"")]
    [InlineData("%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe")]
    public void Plain_powershell_accepts_a_prompt(string commandline) =>
        Assert.True(PowerShellPrompt.CanApply(commandline));

    [Theory]
    [InlineData(null)]
    [InlineData("cmd.exe")]
    [InlineData("wsl.exe -d Ubuntu")]
    [InlineData("pwsh.exe -NoExit -Command \"npm run dev\"")]
    [InlineData("pwsh.exe -NoExit C:\\script.ps1")]
    [InlineData("pwsh.exe -File C:\\script.ps1")]
    [InlineData("pwsh.exe -c dir")]
    public void Other_command_lines_are_left_alone(string? commandline)
    {
        Assert.False(PowerShellPrompt.CanApply(commandline));
        Assert.Equal(commandline, PowerShellPrompt.Apply(commandline, PromptStyle.FolderName, null));
    }

    [Fact]
    public void Folder_name_prompt_is_appended_to_the_command_line()
    {
        Assert.Equal(
            "pwsh.exe -NoExit -Command \"function prompt { 'PS ' + (Split-Path -Leaf $PWD.Path) + '> ' }\"",
            PowerShellPrompt.Apply("pwsh.exe", PromptStyle.FolderName, null));
    }

    [Fact]
    public void NoExit_is_not_repeated()
    {
        Assert.Equal(
            "pwsh.exe -noexit -Command \"function prompt { 'PS ' + $PWD.Path + [Environment]::NewLine + '> ' }\"",
            PowerShellPrompt.Apply("pwsh.exe -noexit", PromptStyle.TwoLines, null));
    }

    [Fact]
    public void Default_and_empty_custom_change_nothing()
    {
        Assert.Equal("pwsh.exe", PowerShellPrompt.Apply("pwsh.exe", PromptStyle.Default, "'x> '"));
        Assert.Equal("pwsh.exe", PowerShellPrompt.Apply("pwsh.exe", PromptStyle.Custom, "  "));
    }

    [Fact]
    public void Custom_prompt_with_double_quotes_is_encoded()
    {
        var commandline = PowerShellPrompt.Apply("pwsh.exe", PromptStyle.Custom, "\"$(Split-Path -Leaf $PWD)> \"")!;

        Assert.StartsWith("pwsh.exe -NoExit -EncodedCommand ", commandline);
        var encoded = commandline["pwsh.exe -NoExit -EncodedCommand ".Length..];
        Assert.Equal("function prompt { \"$(Split-Path -Leaf $PWD)> \" }", Encoding.Unicode.GetString(Convert.FromBase64String(encoded)));
    }

    [Theory]
    [InlineData("pwsh.exe", PromptStyle.FolderName, null)]
    [InlineData("pwsh.exe -NoLogo", PromptStyle.TwoLines, null)]
    [InlineData("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\"", PromptStyle.FolderName, null)]
    [InlineData("pwsh.exe", PromptStyle.Custom, "'[' + (Get-Date -Format t) + '] > '")]
    [InlineData("pwsh.exe", PromptStyle.Custom, "\"$(Split-Path -Leaf $PWD) -Command> \"")]
    [InlineData("powershell.exe", PromptStyle.Custom, "'a' ; 'b> '")]
    public void Prompt_is_read_back(string commandline, PromptStyle style, string? custom)
    {
        var generated = PowerShellPrompt.Apply(commandline, style, custom);

        Assert.Equal((commandline, style, custom), PowerShellPrompt.Extract(generated));
    }

    [Theory]
    [InlineData("pwsh.exe")]
    [InlineData("pwsh.exe -NoExit -Command \"npm run dev\"")]
    [InlineData("pwsh.exe -NoExit -Command \"function prompt { 'x' }\" -NoLogo")]
    [InlineData("cmd.exe /k -Command \"function prompt { 'x' }\"")]
    public void Other_command_lines_are_read_unchanged(string commandline) =>
        Assert.Equal((commandline, PromptStyle.Default, (string?)null), PowerShellPrompt.Extract(commandline));

    [Fact]
    public void Prompt_survives_publishing_and_reading_back()
    {
        var layout = GenerationTests.DevWeb();
        var front = layout.PrimaryPane;
        front.Prompt = PromptStyle.FolderName;

        var command = JsonActionBuilder.BuildCommand(layout);
        Assert.Equal(PowerShellPrompt.Apply("pwsh.exe", PromptStyle.FolderName, null), (string?)command["actions"]![0]!["commandline"]);

        var read = LayoutParser.Parse("Dev Web", command).PrimaryPane;
        Assert.Equal("pwsh.exe", read.Commandline);
        Assert.Equal(PromptStyle.FolderName, read.Prompt);
    }

    [Fact]
    public void Prompt_survives_the_wt_command_line()
    {
        var layout = GenerationTests.DevWeb();
        layout.PrimaryPane.Prompt = PromptStyle.TwoLines;
        var api = layout.Leaves().ElementAt(1);
        api.Prompt = PromptStyle.Custom;
        api.CustomPrompt = "\"API $(Split-Path -Leaf $PWD)> \"";

        var read = WtCommandLineParser.Parse("Dev Web", WtCommandLineBuilder.BuildDisplay(layout));

        Assert.Equal(PromptStyle.TwoLines, read.PrimaryPane.Prompt);
        Assert.Equal("pwsh.exe", read.PrimaryPane.Commandline);
        var readApi = read.Leaves().ElementAt(1);
        Assert.Equal((PromptStyle.Custom, "\"API $(Split-Path -Leaf $PWD)> \""), (readApi.Prompt, readApi.CustomPrompt));
    }

    [Fact]
    public void Split_copies_the_prompt()
    {
        var layout = GenerationTests.DevWeb();
        layout.PrimaryPane.Prompt = PromptStyle.FolderName;

        var created = layout.Split(layout.PrimaryPane, SplitOrientation.Vertical);

        Assert.Equal(PromptStyle.FolderName, created.Prompt);
    }

    [Fact]
    public void Profile_commandline_comes_from_the_profile_or_its_generator()
    {
        Assert.Equal("pwsh.exe", PowerShellPrompt.ProfileCommandline(new TerminalProfile("PowerShell", "{g}", null, false, Source: "Windows.Terminal.PowershellCore")));
        Assert.Equal("powershell.exe -NoLogo", PowerShellPrompt.ProfileCommandline(new TerminalProfile("WPS", "{g}", "powershell.exe -NoLogo", false)));
        Assert.Null(PowerShellPrompt.ProfileCommandline(new TerminalProfile("Dev PS", "{g}", null, false, Source: "Windows.Terminal.VisualStudio")));
        Assert.Null(PowerShellPrompt.ProfileCommandline(new TerminalProfile("cmd", "{g}", "cmd.exe", false)));
        Assert.Null(PowerShellPrompt.ProfileCommandline(null));
    }
}
