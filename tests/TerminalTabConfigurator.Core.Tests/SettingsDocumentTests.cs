using System.Text.Json.Nodes;
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
}
