// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Parsing;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Core.Tests;

public class TitleAndIconTests
{
    [Fact]
    public void First_pane_keeps_its_own_title_next_to_the_tab_title()
    {
        var layout = GenerationTests.DevWeb();
        layout.PrimaryPane.Title = "Front";

        var command = JsonActionBuilder.BuildCommand(layout);
        var actions = command["actions"]!.AsArray();
        Assert.Equal("Front", actions[0]!["tabTitle"]!.GetValue<string>());
        Assert.Equal("renameTab", actions[^1]!["action"]!.GetValue<string>());
        Assert.Equal("Dev Web", actions[^1]!["title"]!.GetValue<string>());

        var parsed = LayoutParser.Parse("Dev Web", command);
        Assert.Equal("Dev Web", parsed.TabTitle);
        Assert.Equal(["Front", "API", "Logs"], parsed.Leaves().Select(p => p.Title));
    }

    [Fact]
    public void No_renameTab_without_tab_title()
    {
        var layout = GenerationTests.DevWeb();
        layout.TabTitle = null;

        var actions = JsonActionBuilder.BuildCommand(layout)["actions"]!.AsArray();
        Assert.DoesNotContain(actions, a => a!["action"]!.GetValue<string>() == "renameTab");
        Assert.Null(LayoutParser.Parse("x", JsonActionBuilder.BuildCommand(layout)).TabTitle);
    }

    [Fact]
    public void Focus_on_start_survives_the_final_renameTab()
    {
        var layout = GenerationTests.DevWeb();
        var api = layout.Leaves().ElementAt(1);
        api.FocusOnStart = true;

        var parsed = LayoutParser.Parse("x", JsonActionBuilder.BuildCommand(layout));
        Assert.True(parsed.Leaves().ElementAt(1).FocusOnStart);
    }

    [Fact]
    public void Reads_layouts_published_before_renameTab()
    {
        // Older format: the tab title rode on newTab and was repeated by untitled panes.
        var command = JsonNode.Parse("""
            {
              "action": "multipleActions",
              "actions": [
                { "action": "newTab", "tabTitle": "Guik" },
                { "action": "splitPane", "split": "right", "tabTitle": "Helm" },
                { "action": "splitPane", "split": "down", "tabTitle": "Guik" }
              ]
            }
            """);

        var layout = LayoutParser.Parse("Guik", command);
        Assert.Equal("Guik", layout.TabTitle);
        Assert.Equal([null, "Helm", null], layout.Leaves().Select(p => p.Title));
    }

    [Fact]
    public void Icon_is_written_read_back_and_removed()
    {
        var doc = TerminalSettingsDocument.Parse("""{ "actions": [] }""");
        var layout = GenerationTests.DevWeb();
        layout.Icon = "🚀";

        doc.Upsert(layout, new PublishOptions(null, false));
        var action = doc.Root["actions"]!.AsArray().Single()!;
        Assert.Equal("🚀", action["icon"]!.GetValue<string>());
        Assert.Equal("🚀", doc.GetLayouts().Single().Layout!.Icon);

        layout.Icon = null;
        doc.Upsert(layout, new PublishOptions(null, false));
        Assert.Null(doc.Root["actions"]!.AsArray().Single()!["icon"]);
    }

    [Fact]
    public void Emoji_are_written_as_is_and_literal_backslashes_are_untouched()
    {
        var node = new System.Text.Json.Nodes.JsonObject
        {
            ["icon"] = "🚀",
            ["path"] = @"C:\uD83D\uDE80", // literal text, not an escape
        };

        var json = TerminalSettingsDocument.Format(node);

        Assert.Contains("\"icon\": \"🚀\"", json);
        Assert.Contains(@"""path"": ""C:\\uD83D\\uDE80""", json);
        Assert.Equal("🚀", TerminalSettingsDocument.Parse(json).Root["icon"]!.GetValue<string>());
    }
}
