using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TerminalTabConfigurator.Core.Model;

/// <summary>
/// A node of the split tree: either a <see cref="PaneLeaf"/> (one terminal) or a
/// <see cref="SplitNode"/> (an area divided in two).
/// </summary>
public abstract class LayoutNode : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SplitNode? Parent { get; internal set; }

    internal TabLayout? Owner { get; private set; }

    internal void Attach(TabLayout? owner, SplitNode? parent)
    {
        Owner = owner;
        Parent = parent;
        if (this is SplitNode split)
        {
            split.First.Attach(owner, split);
            split.Second.Attach(owner, split);
        }
    }

    /// <summary>Leaves of this subtree, from top-left to bottom-right.</summary>
    public IEnumerable<PaneLeaf> Leaves()
    {
        if (this is PaneLeaf leaf)
        {
            yield return leaf;
            yield break;
        }

        var split = (SplitNode)this;
        foreach (var l in split.First.Leaves()) yield return l;
        foreach (var l in split.Second.Leaves()) yield return l;
    }

    /// <summary>The pane that occupies this area before it gets divided (top-left leaf).</summary>
    public PaneLeaf FirstLeaf() => this is PaneLeaf leaf ? leaf : ((SplitNode)this).First.FirstLeaf();

    /// <summary>Sets a field and raises change notifications. UI-only state passes <paramref name="affectsLayout"/> = false.</summary>
    protected bool Set<T>(ref T field, T value, bool affectsLayout = true, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        if (affectsLayout) Owner?.NotifyChanged(structural: false);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
