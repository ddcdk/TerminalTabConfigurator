// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerminalTabConfigurator.Services;

/// <summary>User preferences, saved in %APPDATA%\TerminalTabConfigurator\preferences.json.</summary>
public sealed class Preferences
{
    public const string System = "system";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>"system", "fr" or "en".</summary>
    public string Language { get; set; } = System;

    /// <summary>"system", "dark" or "light".</summary>
    public string Theme { get; set; } = System;

    [JsonIgnore]
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TerminalTabConfigurator", "preferences.json");

    public static Preferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath), JsonOptions) ?? new Preferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A broken preferences file falls back to the defaults.
        }

        return new Preferences();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a preference is not worth an error message.
        }
    }
}
