// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

namespace TerminalTabConfigurator.Core.Model;

/// <summary>
/// Text field helpers. Edited values are stored as typed (WPF reads the value back after each
/// keystroke, so trimming in a setter would swallow a space typed at the end); they are trimmed
/// when generated or published.
/// </summary>
public static class TextValue
{
    /// <summary>Blank becomes null; anything else is kept as typed.</summary>
    public static string? Keep(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Blank becomes null; anything else is trimmed.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
