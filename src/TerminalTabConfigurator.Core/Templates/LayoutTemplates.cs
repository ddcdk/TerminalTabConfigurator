using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Core.Templates;

/// <summary>A starting layout offered when creating a new one.</summary>
public sealed record LayoutTemplate(string Name, string Description, Func<LayoutNode> CreateRoot)
{
    /// <summary>Creates a layout whose panes all start in <paramref name="folder"/> and run <paramref name="profile"/>.</summary>
    public TabLayout Create(string layoutName, string? folder, string? profile)
    {
        var root = CreateRoot();
        foreach (var pane in root.Leaves())
        {
            pane.StartingDirectory = folder;
            pane.Profile = profile;
        }

        return new TabLayout(layoutName, root) { TabTitle = layoutName };
    }
}

public static class LayoutTemplates
{
    private const SplitOrientation V = SplitOrientation.Vertical;
    private const SplitOrientation H = SplitOrientation.Horizontal;

    private static PaneLeaf P() => new();

    public static IReadOnlyList<LayoutTemplate> All { get; } =
    [
        new("Volet unique", "1 volet", P),
        new("2 colonnes", "2 volets côte à côte", () => new SplitNode(V, P(), P())),
        new("2 lignes", "2 volets empilés", () => new SplitNode(H, P(), P())),
        new("1 + 2 à droite", "3 volets", () => new SplitNode(V, P(), new SplitNode(H, P(), P()))),
        new("2 à gauche + 1", "3 volets", () => new SplitNode(V, new SplitNode(H, P(), P()), P())),
        new("1 en haut + 2 en bas", "3 volets", () => new SplitNode(H, P(), new SplitNode(V, P(), P()), 0.6)),
        new("3 colonnes", "3 volets", () => new SplitNode(V, P(), new SplitNode(V, P(), P()), 1 / 3.0)),
        new("Grille 2 × 2", "4 volets", () => new SplitNode(V, new SplitNode(H, P(), P()), new SplitNode(H, P(), P()))),
    ];
}
