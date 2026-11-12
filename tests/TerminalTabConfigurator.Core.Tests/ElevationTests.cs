// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Core.Tests;

public class ElevationTests
{
    private const string Normal = "{574e775e-4f2a-5b96-ac1e-a2962a402336}";
    private const string Admin = "{594ee2a1-90f2-4a6c-a4c9-684828ea66d8}";

    private static TerminalSettingsDocument Settings(bool elevateByDefault = false) => TerminalSettingsDocument.Parse($$"""
        {
            "defaultProfile": "{{Normal}}",
            "profiles": {
                "defaults": { "elevate": {{(elevateByDefault ? "true" : "false")}} },
                "list": [
                    { "guid": "{{Normal}}", "name": "PowerShell" },
                    { "guid": "{{Admin}}", "name": "PowerShell admin", "elevate": true },
                    { "name": "Jamais admin", "elevate": false },
                ]
            }
        }
        """);

    [Fact]
    public void Reads_elevate_with_inheritance_from_defaults()
    {
        var profiles = Settings().GetProfiles();
        Assert.Equal([false, true, false], profiles.Select(p => p.Elevate));

        var inherited = Settings(elevateByDefault: true).GetProfiles();
        Assert.Equal([true, true, false], inherited.Select(p => p.Elevate));
    }

    [Theory]
    [InlineData(null, null, null, LayoutElevation.None)]
    [InlineData(Admin, Admin, Admin, LayoutElevation.All)]
    [InlineData(Normal, Admin, Normal, LayoutElevation.Mixed)]
    public void Analyzes_a_layout(string? front, string? api, string? logs, LayoutElevation expected)
    {
        var settings = Settings();
        var layout = GenerationTests.DevWeb();
        var panes = layout.Leaves().ToList();
        panes[0].Profile = front;
        panes[1].Profile = api;
        panes[2].Profile = logs;

        Assert.Equal(expected, Elevation.Analyze(layout, settings.GetProfiles(), settings.DefaultProfile));
    }

    [Fact]
    public void Explicit_elevate_on_a_pane_overrides_its_profile()
    {
        var settings = Settings();
        var layout = GenerationTests.DevWeb();
        foreach (var pane in layout.Leaves())
        {
            pane.Profile = Admin;
            pane.ExtraArgs["elevate"] = false;
        }

        Assert.Equal(LayoutElevation.None, Elevation.Analyze(layout, settings.GetProfiles(), settings.DefaultProfile));
    }

    [Fact]
    public void Default_profile_decides_for_panes_without_profile()
    {
        var settings = TerminalSettingsDocument.Parse($$"""
            { "defaultProfile": "{{Admin}}", "profiles": { "list": [ { "guid": "{{Admin}}", "name": "PowerShell admin", "elevate": true } ] } }
            """);

        Assert.Equal(LayoutElevation.All, Elevation.Analyze(GenerationTests.DevWeb(), settings.GetProfiles(), settings.DefaultProfile));
    }
}
