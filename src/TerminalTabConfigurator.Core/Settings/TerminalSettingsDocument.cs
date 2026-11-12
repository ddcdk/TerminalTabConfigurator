// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TerminalTabConfigurator.Core.Generation;
using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Core.Parsing;

namespace TerminalTabConfigurator.Core.Settings;

/// <param name="Elevate">The profile runs as administrator (<c>"elevate": true</c>, possibly inherited from <c>profiles.defaults</c>).</param>
public sealed record TerminalProfile(string Name, string? Guid, string? Commandline, bool Hidden, bool Elevate = false, string? Icon = null)
{
    /// <summary>The profile only starts <c>wt.exe</c>: it opens a layout that can be imported (see <see cref="WtCommandLineParser"/>).</summary>
    public bool IsLayout => WtCommandLineParser.IsWtCommand(Commandline);

    /// <summary>Value written in <c>"profile"</c>: the GUID when known (survives a rename), else the name.</summary>
    public string Reference => Guid ?? Name;

    public bool Matches(string? reference) =>
        reference is not null &&
        (string.Equals(reference, Guid, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(reference, Name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A layout found in settings.json. <see cref="Layout"/> is null when it could not be read back (see <see cref="Error"/>).</summary>
/// <param name="HasProfile">A profile that opens the layout was published with it (see <see cref="TerminalSettingsDocument.LayoutProfileGuid"/>).</param>
public sealed record PublishedLayout(string ActionId, string Name, TabLayout? Layout, LayoutParseException? Error, string? Keys, bool InNewTabMenu, bool HasProfile = false);

/// <param name="AddToNewTabMenu">Show the layout in the "New tab" menu: the action, or the profile when there is one.</param>
/// <param name="CreateProfile">Also publish a profile whose command line opens the layout (usable as default profile, with <c>wt -p</c>, in the jump list).</param>
public sealed record PublishOptions(string? Keys, bool AddToNewTabMenu, bool CreateProfile = false);

/// <summary>
/// Reads and edits a Windows Terminal settings.json. Comments and trailing commas are accepted on
/// read; comments are not kept on write (a backup is made first).
/// </summary>
public sealed partial class TerminalSettingsDocument
{
    public const string ActionIdPrefix = "User.TTC.";

    /// <summary>Namespace of the name-based GUIDs of layout profiles (see <see cref="LayoutProfileGuid"/>).</summary>
    private static readonly Guid LayoutProfileNamespace = new("5b0d3f6e-6c1a-4f0e-9a43-7d2f1c8e4b21");

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private TerminalSettingsDocument(JsonObject root, string? path)
    {
        Root = root;
        FilePath = path;
    }

    public JsonObject Root { get; }

    public string? FilePath { get; }

    public static TerminalSettingsDocument Load(string path) => Parse(File.ReadAllText(path), path);

    public static TerminalSettingsDocument Parse(string json, string? path = null)
    {
        var node = JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject
                   ?? throw new InvalidDataException("settings.json ne contient pas un objet JSON.");
        return new TerminalSettingsDocument(node, path);
    }

    // ---------- Reading ----------

    public IReadOnlyList<TerminalProfile> GetProfiles()
    {
        var list = ProfileList;
        var defaultElevate = Root["profiles"] is JsonObject profiles && profiles["defaults"] is JsonObject defaults
                             && Bool(defaults, "elevate") == true;

        return list?.OfType<JsonObject>()
                   .Select(p => new TerminalProfile(
                       Str(p, "name") ?? "(sans nom)",
                       Str(p, "guid"),
                       Str(p, "commandline"),
                       Bool(p, "hidden") == true,
                       Bool(p, "elevate") ?? defaultElevate,
                       Str(p, "icon")))
                   .ToList()
               ?? [];
    }

    /// <summary>The <c>defaultProfile</c> setting (GUID or name), used by panes without an explicit profile.</summary>
    public string? DefaultProfile => Str(Root, "defaultProfile");

    public IReadOnlyList<PublishedLayout> GetLayouts()
    {
        var result = new List<PublishedLayout>();
        if (Root["actions"] is not JsonArray actions) return result;

        foreach (var action in actions.OfType<JsonObject>())
        {
            var id = Str(action, "id");
            if (id is null || !id.StartsWith(ActionIdPrefix, StringComparison.Ordinal)) continue;

            var name = Str(action, "name") ?? id[ActionIdPrefix.Length..];
            TabLayout? layout = null;
            LayoutParseException? error = null;
            try
            {
                layout = LayoutParser.Parse(name, action["command"]);
                layout.ActionId = id;
                layout.Icon = Str(action, "icon");
            }
            catch (Exception ex) when (ex is LayoutParseException or InvalidOperationException or FormatException)
            {
                error = ex as LayoutParseException ?? new LayoutParseException(LayoutParseError.Invalid, ex.Message);
            }

            // With a profile, the menu shows the profile (it is hidden when it should not appear).
            var profile = FindProfile(LayoutProfileGuid(id));
            var inMenu = profile is null ? IsInNewTabMenu(id) : Bool(profile, "hidden") != true;
            result.Add(new PublishedLayout(id, name, layout, error, GetKeys(id), inMenu, profile is not null));
        }

        return result;
    }

    /// <summary>Profiles that open a layout with <c>wt.exe</c> and were not published by this tool: they can be imported.</summary>
    public IReadOnlyList<TerminalProfile> GetImportableProfiles()
    {
        var published = LayoutActionIds().Select(LayoutProfileGuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. GetProfiles().Where(p => p.IsLayout && (p.Guid is null || !published.Contains(p.Guid)))];
    }

    /// <summary>True when the profile published with the layout is Windows Terminal's default profile (it must not be removed).</summary>
    public bool IsLayoutProfileDefault(string actionId)
    {
        if (DefaultProfile is not { } reference || FindProfile(LayoutProfileGuid(actionId)) is not { } profile) return false;
        return string.Equals(reference, Str(profile, "guid"), StringComparison.OrdinalIgnoreCase)
               || string.Equals(reference, Str(profile, "name"), StringComparison.OrdinalIgnoreCase);
    }

    public string? GetKeys(string actionId)
    {
        if (Root["keybindings"] is JsonArray bindings)
        {
            var binding = bindings.OfType<JsonObject>().FirstOrDefault(b => Str(b, "id") == actionId);
            if (binding is not null) return KeysToString(binding["keys"]);
        }

        // Older format: keys stored on the action itself.
        var action = FindAction(actionId);
        return action is null ? null : KeysToString(action["keys"]);
    }

    public bool IsInNewTabMenu(string actionId) =>
        Root["newTabMenu"] is JsonArray menu && MenuEntries(menu).Any(e => IsActionEntry(e, actionId));

    /// <summary>Returns the id of another action already bound to <paramref name="keys"/>, if any.</summary>
    public string? FindKeyConflict(string keys, string? exceptActionId)
    {
        var wanted = NormalizeKeys(keys);
        if (Root["keybindings"] is JsonArray bindings)
        {
            foreach (var b in bindings.OfType<JsonObject>())
            {
                var id = Str(b, "id");
                if (id != exceptActionId && NormalizeKeys(KeysToString(b["keys"])) == wanted) return id ?? "(sans id)";
            }
        }

        if (Root["actions"] is JsonArray actions)
        {
            foreach (var a in actions.OfType<JsonObject>())
            {
                var id = Str(a, "id");
                if (id != exceptActionId && NormalizeKeys(KeysToString(a["keys"])) == wanted) return id ?? "(sans id)";
            }
        }

        return null;
    }

    // ---------- Editing ----------

    /// <summary>Adds or updates the layout's action, key binding and new tab menu entry. Assigns <see cref="TabLayout.ActionId"/> when missing.</summary>
    public void Upsert(TabLayout layout, PublishOptions options)
    {
        layout.ActionId ??= GenerateActionId(layout.Name);
        var id = layout.ActionId;

        var actions = EnsureArray("actions");
        var action = FindAction(id);
        if (action is null)
        {
            action = new JsonObject { ["id"] = id };
            actions.Add(action);
        }

        action["name"] = layout.Name;
        if (layout.Icon is not null) action["icon"] = layout.Icon;
        else action.Remove("icon");
        action["command"] = JsonActionBuilder.BuildCommand(layout);
        action.Remove("keys");

        RemoveKeyBindings(id);
        if (!string.IsNullOrWhiteSpace(options.Keys))
            EnsureArray("keybindings").Add(new JsonObject { ["id"] = id, ["keys"] = options.Keys.Trim() });

        // The "New tab" menu shows the profile or the action, never both. A profile left out of
        // the menu is hidden, otherwise "remainingProfiles" would list it anyway.
        var guid = LayoutProfileGuid(id);
        if (options.CreateProfile)
        {
            UpsertProfile(BuildLayoutProfile(layout, id, hidden: !options.AddToNewTabMenu));
            RemoveFromNewTabMenu(e => IsActionEntry(e, id));
            if (options.AddToNewTabMenu) AddToNewTabMenu(new JsonObject { ["type"] = "profile", ["profile"] = guid }, e => IsProfileEntry(e, guid));
            else RemoveFromNewTabMenu(e => IsProfileEntry(e, guid));
        }
        else
        {
            RemoveLayoutProfile(guid);
            if (options.AddToNewTabMenu) AddToNewTabMenu(new JsonObject { ["type"] = "action", ["id"] = id }, e => IsActionEntry(e, id));
            else RemoveFromNewTabMenu(e => IsActionEntry(e, id));
        }
    }

    /// <summary>
    /// GUID of the profile published with a layout: a name-based UUID (version 5) of the action id,
    /// so the profile is found again from the action without storing anything else.
    /// </summary>
    public static string LayoutProfileGuid(string actionId)
    {
        var name = Encoding.UTF8.GetBytes(actionId);
        var data = new byte[16 + name.Length];
        LayoutProfileNamespace.TryWriteBytes(data, bigEndian: true, out _);
        name.CopyTo(data, 16);

        var hash = System.Security.Cryptography.SHA1.HashData(data);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(hash.AsSpan(0, 16), bigEndian: true).ToString("B");
    }

    /// <summary>The profile published with a layout: its command line is the equivalent <c>wt.exe</c> command.</summary>
    public static JsonObject BuildLayoutProfile(TabLayout layout, string actionId, bool hidden)
    {
        var profile = new JsonObject
        {
            ["guid"] = LayoutProfileGuid(actionId),
            ["name"] = layout.Name,
            ["commandline"] = WtCommandLineBuilder.BuildDisplay(layout),
        };
        if (TextValue.Clean(layout.Icon) is { } icon) profile["icon"] = icon;
        profile["hidden"] = hidden;
        return profile;
    }

    /// <summary>Removes the action and everything that points to it.</summary>
    public void Remove(string actionId)
    {
        if (Root["actions"] is JsonArray actions)
        {
            var action = FindAction(actionId);
            if (action is not null) actions.Remove(action);
        }

        RemoveKeyBindings(actionId);
        RemoveFromNewTabMenu(e => IsActionEntry(e, actionId));
        RemoveLayoutProfile(LayoutProfileGuid(actionId));
    }

    public string GenerateActionId(string name)
    {
        var sb = new StringBuilder();
        var upperNext = true;
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                upperNext = true;
            }
        }

        var baseId = ActionIdPrefix + (sb.Length > 0 ? sb.ToString() : "Layout");
        var candidate = baseId;
        for (var i = 2; FindAction(candidate) is not null; i++) candidate = baseId + i;
        return candidate;
    }

    public string ToJson() => Format(Root);

    /// <summary>
    /// Indented JSON as Windows Terminal writes it. System.Text.Json always escapes characters
    /// outside the BMP (emoji icons) as surrogate pairs; they are written back as is.
    /// </summary>
    public static string Format(JsonNode node) =>
        EscapedSurrogatePair().Replace(node.ToJsonString(WriteOptions), m =>
            m.Groups["bs"].Value + char.ConvertFromUtf32(char.ConvertToUtf32(
                (char)Convert.ToInt32(m.Groups["hi"].Value, 16), (char)Convert.ToInt32(m.Groups["lo"].Value, 16))));

    // An even number of backslashes before \u: the \u is an escape, not literal text.
    [GeneratedRegex(@"(?<!\\)(?<bs>(?:\\\\)*)\\u(?<hi>D[89AB][0-9A-F]{2})\\u(?<lo>D[C-F][0-9A-F]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex EscapedSurrogatePair();

    /// <summary>Writes the file back. Returns the backup path when <paramref name="backup"/> is true.</summary>
    public string? Save(bool backup)
    {
        if (FilePath is null) throw new InvalidOperationException("This document has no file path.");

        string? backupPath = null;
        if (backup && File.Exists(FilePath))
        {
            backupPath = $"{FilePath}.ttc-backup-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(FilePath, backupPath, overwrite: true);
        }

        var temp = FilePath + ".ttc-tmp";
        File.WriteAllText(temp, ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, FilePath, overwrite: true);
        return backupPath;
    }

    // ---------- Helpers ----------

    private JsonObject? FindAction(string actionId) =>
        (Root["actions"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(a => Str(a, "id") == actionId);

    private JsonArray EnsureArray(string key)
    {
        switch (Root[key])
        {
            case JsonArray existing:
                return existing;
            case JsonObject single:
                Root.Remove(key);
                var wrapped = new JsonArray { single };
                Root[key] = wrapped;
                return wrapped;
            default:
                var created = new JsonArray();
                Root[key] = created;
                return created;
        }
    }

    private void RemoveKeyBindings(string actionId)
    {
        if (Root["keybindings"] is not JsonArray bindings) return;
        foreach (var b in bindings.OfType<JsonObject>().Where(b => Str(b, "id") == actionId).ToList())
            bindings.Remove(b);
    }

    private JsonArray? ProfileList => Root["profiles"] switch
    {
        JsonObject o => o["list"] as JsonArray,
        JsonArray a => a,
        _ => null,
    };

    private JsonArray EnsureProfileList()
    {
        switch (Root["profiles"])
        {
            case JsonArray array:
                return array;
            case JsonObject profiles:
                if (profiles["list"] is JsonArray list) return list;
                var created = new JsonArray();
                profiles["list"] = created;
                return created;
            default:
                var newList = new JsonArray();
                Root["profiles"] = new JsonObject { ["defaults"] = new JsonObject(), ["list"] = newList };
                return newList;
        }
    }

    private JsonObject? FindProfile(string guid) =>
        ProfileList?.OfType<JsonObject>().FirstOrDefault(p => string.Equals(Str(p, "guid"), guid, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds the profile, or updates the keys this tool writes and keeps the others (colors, font…).</summary>
    private void UpsertProfile(JsonObject profile)
    {
        var existing = FindProfile(Str(profile, "guid")!);
        if (existing is null)
        {
            EnsureProfileList().Add(profile);
            return;
        }

        foreach (var key in new[] { "name", "commandline", "icon", "hidden" })
        {
            if (profile[key] is { } value) existing[key] = value.DeepClone();
            else existing.Remove(key);
        }
    }

    private void RemoveLayoutProfile(string guid)
    {
        if (FindProfile(guid) is { } profile) ProfileList!.Remove(profile);
        RemoveFromNewTabMenu(e => IsProfileEntry(e, guid));
    }

    private IEnumerable<string> LayoutActionIds() =>
        (Root["actions"] as JsonArray)?.OfType<JsonObject>()
            .Select(a => Str(a, "id"))
            .Where(id => id?.StartsWith(ActionIdPrefix, StringComparison.Ordinal) == true)
            .Select(id => id!)
        ?? [];

    private void AddToNewTabMenu(JsonObject entry, Func<JsonObject, bool> isSameEntry)
    {
        JsonArray menu;
        if (Root["newTabMenu"] is null)
        {
            // Without newTabMenu, Windows Terminal shows all profiles: keep that.
            menu = [new JsonObject { ["type"] = "remainingProfiles" }];
            Root["newTabMenu"] = menu;
        }
        else
        {
            menu = EnsureArray("newTabMenu");
        }

        if (MenuEntries(menu).Any(isSameEntry)) return;

        // Keep our entries (actions and layout profiles) grouped at the top, in publication order.
        var ownProfiles = LayoutActionIds().Select(LayoutProfileGuid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (index < menu.Count && menu[index] is JsonObject e && IsOwnEntry(e, ownProfiles))
            index++;

        menu.Insert(index, entry);
    }

    private void RemoveFromNewTabMenu(Func<JsonObject, bool> match)
    {
        if (Root["newTabMenu"] is JsonArray menu) RemoveFrom(menu);

        void RemoveFrom(JsonArray entries)
        {
            foreach (var e in entries.OfType<JsonObject>().ToList())
            {
                if (match(e)) entries.Remove(e);
                else if (e["entries"] is JsonArray children) RemoveFrom(children);
            }
        }
    }

    private static bool IsOwnEntry(JsonObject entry, HashSet<string> ownProfiles) => Str(entry, "type") switch
    {
        "action" => Str(entry, "id")?.StartsWith(ActionIdPrefix, StringComparison.Ordinal) == true,
        "profile" => Str(entry, "profile") is { } profile && ownProfiles.Contains(profile),
        _ => false,
    };

    private static bool IsProfileEntry(JsonObject entry, string guid) =>
        Str(entry, "type") == "profile" && string.Equals(Str(entry, "profile"), guid, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonObject> MenuEntries(JsonArray entries)
    {
        foreach (var e in entries.OfType<JsonObject>())
        {
            yield return e;
            if (e["entries"] is JsonArray children)
                foreach (var c in MenuEntries(children)) yield return c;
        }
    }

    private static bool IsActionEntry(JsonObject entry, string actionId) =>
        Str(entry, "type") == "action" && Str(entry, "id") == actionId;

    private static string? KeysToString(JsonNode? keys) => keys switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonArray a => a.Select(k => k?.ToString()).FirstOrDefault(k => !string.IsNullOrEmpty(k)),
        _ => null,
    };

    private static string? NormalizeKeys(string? keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return null;
        string[] modifierNames = ["ctrl", "alt", "shift", "win"];
        var parts = keys.ToLowerInvariant().Replace(" ", string.Empty).Split('+', StringSplitOptions.RemoveEmptyEntries);
        var modifiers = parts.Where(modifierNames.Contains).Order(StringComparer.Ordinal);
        var others = parts.Where(p => !modifierNames.Contains(p));
        return string.Join('+', modifiers.Concat(others));
    }

    private static string? Str(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? Bool(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
}
