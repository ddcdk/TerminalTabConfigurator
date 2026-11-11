using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TerminalTabConfigurator.Core.Model;

/// <summary>A Windows Terminal tab made of split panes, published as one action.</summary>
public sealed class TabLayout : INotifyPropertyChanged
{
    private string _name;
    private string? _tabTitle;
    private string? _tabColor;
    private string? _icon;
    private LayoutNode _root;

    public TabLayout(string name, LayoutNode root)
    {
        _name = name;
        _root = root;
        _root.Attach(this, null);
        Renumber();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised on any change. The argument is true when panes were added or removed.</summary>
    public event EventHandler<bool>? Changed;

    /// <summary>Action name shown in the command palette and the new tab menu.</summary>
    public string Name { get => _name; set => Set(ref _name, value?.Trim() ?? string.Empty); }

    /// <summary>Id of the action in settings.json (<c>User.TTC.*</c>); null until published.</summary>
    public string? ActionId { get; set; }

    /// <summary>Tab title (written as a final <c>renameTab</c>), independent from the pane titles.</summary>
    public string? TabTitle { get => _tabTitle; set => Set(ref _tabTitle, string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }

    /// <summary>
    /// Icon of the action, shown in the command palette and the new tab menu: an image path,
    /// an emoji or a Segoe Fluent Icons glyph.
    /// </summary>
    public string? Icon { get => _icon; set => Set(ref _icon, string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }

    /// <summary>Tab color as <c>#RRGGBB</c>, or null.</summary>
    public string? TabColor { get => _tabColor; set => Set(ref _tabColor, string.IsNullOrWhiteSpace(value) ? null : value.Trim()); }

    public LayoutNode Root
    {
        get => _root;
        private set
        {
            _root = value;
            _root.Attach(this, null);
        }
    }

    public IEnumerable<PaneLeaf> Leaves() => Root.Leaves();

    public PaneLeaf PrimaryPane => Root.FirstLeaf();

    /// <summary>Splits <paramref name="pane"/> in two; returns the new pane (right or bottom).</summary>
    public PaneLeaf Split(PaneLeaf pane, SplitOrientation orientation)
    {
        EnsureOwned(pane);
        var newPane = pane.CloneSettings();
        var parent = pane.Parent;
        var split = new SplitNode(orientation, pane, newPane);
        if (parent is null) Root = split;
        else
        {
            parent.Replace(pane, split);
            split.Attach(this, parent);
        }

        StructureChanged();
        return newPane;
    }

    /// <summary>Removes a pane; its sibling takes the freed space. Returns the pane to select next, or null if it is the last one.</summary>
    public PaneLeaf? Remove(PaneLeaf pane)
    {
        EnsureOwned(pane);
        var parent = pane.Parent;
        if (parent is null) return null;

        var sibling = ReferenceEquals(parent.First, pane) ? parent.Second : parent.First;
        var grandParent = parent.Parent;
        if (grandParent is null) Root = sibling;
        else
        {
            grandParent.Replace(parent, sibling);
            sibling.Attach(this, grandParent);
        }

        StructureChanged();
        return sibling.FirstLeaf();
    }

    /// <summary>Gives every pane the same size along each run of same-orientation splits.</summary>
    public void Equalize()
    {
        Visit(Root);
        NotifyChanged(structural: false);

        static void Visit(LayoutNode node)
        {
            if (node is not SplitNode split) return;
            var first = Slots(split.First, split.Orientation);
            var second = Slots(split.Second, split.Orientation);
            split.Ratio = (double)first / (first + second);
            Visit(split.First);
            Visit(split.Second);
        }

        static int Slots(LayoutNode node, SplitOrientation orientation) =>
            node is SplitNode s && s.Orientation == orientation
                ? Slots(s.First, orientation) + Slots(s.Second, orientation)
                : 1;
    }

    internal void NotifyChanged(bool structural) => Changed?.Invoke(this, structural);

    private void StructureChanged()
    {
        Renumber();
        NotifyChanged(structural: true);
    }

    private void Renumber()
    {
        var i = 1;
        foreach (var leaf in Root.Leaves()) leaf.Number = i++;
    }

    private void EnsureOwned(PaneLeaf pane)
    {
        if (!ReferenceEquals(pane.Owner, this))
            throw new InvalidOperationException("The pane does not belong to this layout.");
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        NotifyChanged(structural: false);
    }
}
