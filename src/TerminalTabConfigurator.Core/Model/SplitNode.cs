namespace TerminalTabConfigurator.Core.Model;

public enum SplitOrientation
{
    /// <summary>Side by side: the divider is vertical (<c>split: right</c>, <c>sp -V</c>).</summary>
    Vertical,

    /// <summary>Stacked: the divider is horizontal (<c>split: down</c>, <c>sp -H</c>).</summary>
    Horizontal,
}

/// <summary>An area divided in two. <see cref="First"/> is left/top, <see cref="Second"/> is right/bottom.</summary>
public sealed class SplitNode : LayoutNode
{
    public const double MinRatio = 0.05;
    public const double MaxRatio = 0.95;

    private SplitOrientation _orientation;
    private double _ratio;

    public SplitNode(SplitOrientation orientation, LayoutNode first, LayoutNode second, double ratio = 0.5)
    {
        _orientation = orientation;
        _ratio = Clamp(ratio);
        First = first;
        Second = second;
    }

    public SplitOrientation Orientation
    {
        get => _orientation;
        set => Set(ref _orientation, value);
    }

    /// <summary>Share of the area given to <see cref="First"/> (0–1).</summary>
    public double Ratio
    {
        get => _ratio;
        set => Set(ref _ratio, Clamp(value));
    }

    public LayoutNode First { get; internal set; }

    public LayoutNode Second { get; internal set; }

    internal void Replace(LayoutNode oldChild, LayoutNode newChild)
    {
        if (ReferenceEquals(First, oldChild)) First = newChild;
        else if (ReferenceEquals(Second, oldChild)) Second = newChild;
        else throw new InvalidOperationException("The node is not a child of this split.");
    }

    private static double Clamp(double ratio) => Math.Round(Math.Clamp(ratio, MinRatio, MaxRatio), 3);
}
