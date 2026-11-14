// TerminalTabConfigurator
// Copyright (C) 2026 Delfim Da Costa
// SPDX-License-Identifier: GPL-3.0-or-later
// See the LICENSE file at the root of the project.

namespace TerminalTabConfigurator.Core.Model;

/// <summary>PowerShell prompt of a pane (see <see cref="Generation.PowerShellPrompt"/>).</summary>
public enum PromptStyle
{
    /// <summary>Unchanged: PowerShell's own prompt, or the one set by the user's profile.</summary>
    Default,

    /// <summary>Only the name of the current folder: <c>PS front&gt; </c>.</summary>
    FolderName,

    /// <summary>The full path on one line, the input on the next one.</summary>
    TwoLines,

    /// <summary>A prompt function body written by the user.</summary>
    Custom,
}
