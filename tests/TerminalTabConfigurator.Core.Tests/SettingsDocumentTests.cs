// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text.Json.Nodes;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Settings;

namespace TerminalTabConfigurator.Core.Tests;

public class SettingsDocumentTests
{
    private const string Sample = """
        // Windows Terminal settings, with a comment and trailing commas
        {
            "$schema": "https://aka.ms/terminal-profiles-schema",
            "actions": [
                { "command": "paste", "id": "User.paste" },
            ],
            "keybindings": [
                { "id": "User.paste", "keys": "ctrl+v" },
            ],
            "profiles": {
                "defaults": {},
                "list": [
                    { "name": "PowerShell", "guid": "{574e775e-4f2a-5b96-ac1e-a2962a402336}", "source": "Windows.Terminal.PowershellCore" },
                    { "name": "Invite de commandes", "guid": "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}", "commandline": "%SystemRoot%\\System32\\cmd.exe", "hidden": true },
                ]
            }
        }
        """;

    [Fact]
    public void Reads_profiles_from_jsonc()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);

        var profiles = doc.GetProfiles();

        Assert.Equal(["PowerShell", "Invite de commandes"], profiles.Select(p => p.Name));
        Assert.True(profiles[1].Hidden);
        Assert.Equal("{574e775e-4f2a-5b96-ac1e-a2962a402336}", profiles[0].Reference);
    }

    [Fact]
    public void Publish_then_read_back()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();

        doc.Upsert(layout, new PublishOptions("ctrl+alt+1", AddToNewTabMenu: true));
        var reread = TerminalSettingsDocument.Parse(doc.ToJson());

        Assert.Equal("User.TTC.DevWeb", layout.ActionId);
        var published = Assert.Single(reread.GetLayouts());
        Assert.Equal("Dev Web", published.Name);
        Assert.Null(published.Error);
        Assert.Equal("ctrl+alt+1", published.Keys);
        Assert.True(published.InNewTabMenu);
        Assert.Equal(3, published.Layout!.Leaves().Count());
        Assert.Equal("Logs", published.Layout.Leaves().Last().Title);

        // Without newTabMenu, Windows Terminal lists every profile: that must stay true.
        var menu = Assert.IsType<JsonArray>(reread.Root["newTabMenu"]);
        Assert.Equal("remainingProfiles", menu[1]!["type"]!.GetValue<string>());

        // Untouched content survives.
        Assert.Contains(reread.Root["actions"]!.AsArray(), a => a!["id"]!.GetValue<string>() == "User.paste");
    }

    [Fact]
    public void Republish_updates_in_place_and_removes_options()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions("ctrl+alt+1", true));

        layout.Name = "Dev Web 2";
        layout.Leaves().First().Commandline = "cmd.exe";
        doc.Upsert(layout, new PublishOptions(null, false));

        var published = Assert.Single(doc.GetLayouts());
        Assert.Equal("User.TTC.DevWeb", published.ActionId); // id is stable across renames
        Assert.Equal("Dev Web 2", published.Name);
        Assert.Equal("cmd.exe", published.Layout!.Leaves().First().Commandline);
        Assert.Null(published.Keys);
        Assert.False(published.InNewTabMenu);
        Assert.Equal(2, doc.Root["actions"]!.AsArray().Count);
    }

    [Fact]
    public void Remove_cleans_action_binding_and_menu()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions("ctrl+alt+1", true));

        doc.Remove(layout.ActionId!);

        Assert.Empty(doc.GetLayouts());
        Assert.DoesNotContain("User.TTC", doc.ToJson());
    }

    [Fact]
    public void Detects_key_conflicts_whatever_the_modifier_order()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);

        Assert.Equal("User.paste", doc.FindKeyConflict("V+Ctrl", exceptActionId: null));
        Assert.Null(doc.FindKeyConflict("ctrl+v", exceptActionId: "User.paste"));
        Assert.Null(doc.FindKeyConflict("ctrl+alt+1", exceptActionId: null));
    }

    [Fact]
    public void Generated_ids_are_ascii_and_unique()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        doc.Upsert(new Model.TabLayout("Équipe déploiement", new Model.PaneLeaf()), new PublishOptions(null, false));

        Assert.Equal("User.TTC.EquipeDeploiement", doc.GetLayouts()[0].ActionId);
        Assert.Equal("User.TTC.EquipeDeploiement2", doc.GenerateActionId("équipe-déploiement"));
    }

    [Fact]
    public void Save_writes_file_and_backup()
    {
        var dir = Directory.CreateTempSubdirectory("ttc-tests");
        try
        {
            var path = Path.Combine(dir.FullName, "settings.json");
            File.WriteAllText(path, Sample);
            var doc = TerminalSettingsDocument.Load(path);
            doc.Upsert(GenerationTests.DevWeb(), new PublishOptions(null, false));

            var backup = doc.Save(backup: true);

            Assert.NotNull(backup);
            Assert.Equal(Sample, File.ReadAllText(backup));
            Assert.Single(TerminalSettingsDocument.Load(path).GetLayouts());
            Assert.Contains("\"name\": \"Dev Web\"", File.ReadAllText(path));
            Assert.Contains("C:\\\\Dev\\\\webapp", File.ReadAllText(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static JsonObject? Profile(TerminalSettingsDocument doc, string guid) =>
        doc.Root["profiles"]!["list"]!.AsArray().OfType<JsonObject>()
            .FirstOrDefault(p => p["guid"]!.GetValue<string>() == guid);

    private static IEnumerable<string> MenuTypes(TerminalSettingsDocument doc) =>
        doc.Root["newTabMenu"]!.AsArray().Select(e => e!["type"]!.GetValue<string>());

    [Fact]
    public void Profile_replaces_the_action_in_the_new_tab_menu()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();

        doc.Upsert(layout, new PublishOptions("ctrl+alt+1", AddToNewTabMenu: true, CreateProfile: true));
        var reread = TerminalSettingsDocument.Parse(doc.ToJson());

        var guid = TerminalSettingsDocument.LayoutProfileGuid("User.TTC.DevWeb");
        var profile = Profile(reread, guid)!;
        Assert.Equal("Dev Web", profile["name"]!.GetValue<string>());
        Assert.Equal(WtCommandLineBuilder.BuildDisplay(layout), profile["commandline"]!.GetValue<string>());
        Assert.False(profile["hidden"]!.GetValue<bool>());

        // The menu shows the profile, not the action: a single entry for the layout.
        Assert.Equal(["profile", "remainingProfiles"], MenuTypes(reread));
        Assert.Equal(guid, reread.Root["newTabMenu"]![0]!["profile"]!.GetValue<string>());
        Assert.False(reread.IsInNewTabMenu("User.TTC.DevWeb"));

        // The action keeps its shortcut.
        var published = Assert.Single(reread.GetLayouts());
        Assert.True(published.HasProfile);
        Assert.True(published.InNewTabMenu);
        Assert.Equal("ctrl+alt+1", published.Keys);

        // A profile published by the tool is not offered for import.
        Assert.DoesNotContain(reread.GetImportableProfiles(), p => p.Guid == guid);
    }

    [Fact]
    public void Profile_out_of_the_menu_is_hidden()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        doc.Upsert(GenerationTests.DevWeb(), new PublishOptions(null, AddToNewTabMenu: false, CreateProfile: true));

        var guid = TerminalSettingsDocument.LayoutProfileGuid("User.TTC.DevWeb");
        Assert.True(Profile(doc, guid)!["hidden"]!.GetValue<bool>());
        Assert.Null(doc.Root["newTabMenu"]);
        var published = Assert.Single(doc.GetLayouts());
        Assert.True(published.HasProfile);
        Assert.False(published.InNewTabMenu);
    }

    [Fact]
    public void Republish_updates_the_profile_and_keeps_its_other_settings()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: true));
        var guid = TerminalSettingsDocument.LayoutProfileGuid(layout.ActionId!);
        Profile(doc, guid)!["colorScheme"] = "Campbell";

        layout.Name = "Dev Web 2";
        layout.Leaves().First().Commandline = "cmd.exe";
        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: true));

        var profile = Profile(doc, guid)!;
        Assert.Single(doc.Root["profiles"]!["list"]!.AsArray(), p => p!["guid"]!.GetValue<string>() == guid);
        Assert.Equal("Dev Web 2", profile["name"]!.GetValue<string>());
        Assert.Contains("cmd.exe", profile["commandline"]!.GetValue<string>());
        Assert.Equal("Campbell", profile["colorScheme"]!.GetValue<string>());
        Assert.Equal(["profile", "remainingProfiles"], MenuTypes(doc));
    }

    [Fact]
    public void Unchecking_the_profile_removes_it_and_puts_the_action_back()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: true));

        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: false));

        Assert.Null(Profile(doc, TerminalSettingsDocument.LayoutProfileGuid(layout.ActionId!)));
        Assert.Equal(["action", "remainingProfiles"], MenuTypes(doc));
        Assert.False(doc.GetLayouts()[0].HasProfile);
    }

    [Fact]
    public void Remove_deletes_the_profile_too()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: true));

        doc.Remove(layout.ActionId!);

        Assert.Equal(2, doc.Root["profiles"]!["list"]!.AsArray().Count);
        Assert.Equal(["remainingProfiles"], MenuTypes(doc));
    }

    [Fact]
    public void Layout_profile_guid_is_stable_and_unique()
    {
        var guid = TerminalSettingsDocument.LayoutProfileGuid("User.TTC.DevWeb");

        Assert.Equal(guid, TerminalSettingsDocument.LayoutProfileGuid("User.TTC.DevWeb"));
        Assert.NotEqual(guid, TerminalSettingsDocument.LayoutProfileGuid("User.TTC.DevWeb2"));
        Assert.Matches("^\\{[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\}$", guid);
    }

    [Fact]
    public void Detects_a_layout_profile_used_as_default_profile()
    {
        var doc = TerminalSettingsDocument.Parse(Sample);
        var layout = GenerationTests.DevWeb();
        doc.Upsert(layout, new PublishOptions(null, true, CreateProfile: true));
        Assert.False(doc.IsLayoutProfileDefault(layout.ActionId!));

        doc.Root["defaultProfile"] = TerminalSettingsDocument.LayoutProfileGuid(layout.ActionId!);

        Assert.True(doc.IsLayoutProfileDefault(layout.ActionId!));
    }
}
