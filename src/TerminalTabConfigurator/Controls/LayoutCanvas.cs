using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TerminalTabConfigurator.Core.Model;

namespace TerminalTabConfigurator.Controls;

/// <summary>
/// Draws a <see cref="TabLayout"/> as nested grids. In interactive mode, panes can be selected
/// and dividers dragged to change the split ratios.
/// </summary>
public sealed class LayoutCanvas : ContentControl
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout), typeof(TabLayout), typeof(LayoutCanvas),
        new PropertyMetadata(null, (d, e) => ((LayoutCanvas)d).OnLayoutChanged((TabLayout?)e.OldValue, (TabLayout?)e.NewValue)));

    public static readonly DependencyProperty SelectedPaneProperty = DependencyProperty.Register(
        nameof(SelectedPane), typeof(PaneLeaf), typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((LayoutCanvas)d).OnSelectedPaneChanged((PaneLeaf?)e.OldValue, (PaneLeaf?)e.NewValue)));

    public static readonly DependencyProperty PaneTemplateProperty = DependencyProperty.Register(
        nameof(PaneTemplate), typeof(DataTemplate), typeof(LayoutCanvas),
        new PropertyMetadata(null, (d, _) => ((LayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(LayoutCanvas), new PropertyMetadata(true, (d, _) => ((LayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(LayoutCanvas), new PropertyMetadata(12.0, (d, _) => ((LayoutCanvas)d).Rebuild()));

    public static readonly DependencyProperty PaneContextMenuProperty = DependencyProperty.Register(
        nameof(PaneContextMenu), typeof(ContextMenu), typeof(LayoutCanvas));

    private readonly List<SplitNode> _watchedSplits = [];

    public LayoutCanvas()
    {
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    public TabLayout? Layout { get => (TabLayout?)GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    public PaneLeaf? SelectedPane { get => (PaneLeaf?)GetValue(SelectedPaneProperty); set => SetValue(SelectedPaneProperty, value); }

    public DataTemplate? PaneTemplate { get => (DataTemplate?)GetValue(PaneTemplateProperty); set => SetValue(PaneTemplateProperty, value); }

    public bool IsInteractive { get => (bool)GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    /// <summary>Space between panes (the divider's grab area in interactive mode).</summary>
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public ContextMenu? PaneContextMenu { get => (ContextMenu?)GetValue(PaneContextMenuProperty); set => SetValue(PaneContextMenuProperty, value); }

    private void OnLayoutChanged(TabLayout? oldLayout, TabLayout? newLayout)
    {
        if (oldLayout is not null) oldLayout.Changed -= OnLayoutContentChanged;
        if (newLayout is not null) newLayout.Changed += OnLayoutContentChanged;
        Rebuild();
    }

    private void OnLayoutContentChanged(object? sender, bool structural)
    {
        if (structural) Rebuild();
    }

    private void OnSplitChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SplitNode.Ratio) or nameof(SplitNode.Orientation)) Rebuild();
    }

    private void OnSelectedPaneChanged(PaneLeaf? oldPane, PaneLeaf? newPane)
    {
        if (!IsInteractive) return;
        if (oldPane is not null) oldPane.IsSelected = false;
        if (newPane is not null) newPane.IsSelected = true;
    }

    private void Rebuild()
    {
        foreach (var split in _watchedSplits) split.PropertyChanged -= OnSplitChanged;
        _watchedSplits.Clear();

        Content = Layout is null ? null : Build(Layout.Root);

        if (IsInteractive && SelectedPane is not null) SelectedPane.IsSelected = true;
    }

    private UIElement Build(LayoutNode node)
    {
        if (node is PaneLeaf pane) return BuildPane(pane);

        var split = (SplitNode)node;
        split.PropertyChanged += OnSplitChanged;
        _watchedSplits.Add(split);

        var vertical = split.Orientation == SplitOrientation.Vertical;
        var grid = new Grid();
        var first = new GridLength(split.Ratio, GridUnitType.Star);
        var second = new GridLength(1 - split.Ratio, GridUnitType.Star);
        var gap = new GridLength(Gap);

        if (vertical)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = first, MinWidth = 40 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = gap });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = second, MinWidth = 40 });
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = first, MinHeight = 40 });
            grid.RowDefinitions.Add(new RowDefinition { Height = gap });
            grid.RowDefinitions.Add(new RowDefinition { Height = second, MinHeight = 40 });
        }

        var firstChild = Build(split.First);
        var secondChild = Build(split.Second);
        Place(firstChild, 0);
        Place(secondChild, 2);
        grid.Children.Add(firstChild);
        grid.Children.Add(secondChild);

        if (IsInteractive)
        {
            var splitter = new GridSplitter
            {
                ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Tag = $"{split.Ratio * 100:0} / {(1 - split.Ratio) * 100:0}",
                ToolTip = "Glisser pour redimensionner",
            };
            splitter.SetResourceReference(StyleProperty, vertical ? "VerticalPaneSplitterStyle" : "HorizontalPaneSplitterStyle");
            splitter.DragCompleted += (_, _) => split.Ratio = ReadRatio(grid, vertical);
            Panel.SetZIndex(splitter, 1);
            Place(splitter, 1);
            grid.Children.Add(splitter);
        }

        return grid;

        void Place(UIElement element, int index)
        {
            if (vertical) Grid.SetColumn(element, index);
            else Grid.SetRow(element, index);
        }
    }

    private UIElement BuildPane(PaneLeaf pane)
    {
        var presenter = new ContentPresenter { Content = pane, ContentTemplate = PaneTemplate };
        if (!IsInteractive) return presenter;

        presenter.PreviewMouseLeftButtonDown += (_, _) => SelectedPane = pane;
        presenter.MouseRightButtonUp += (_, e) =>
        {
            SelectedPane = pane;
            if (PaneContextMenu is { } menu)
            {
                menu.DataContext = DataContext;
                menu.PlacementTarget = presenter;
                menu.IsOpen = true;
                e.Handled = true;
            }
        };
        return presenter;
    }

    private static double ReadRatio(Grid grid, bool vertical)
    {
        double a, b;
        if (vertical)
        {
            a = grid.ColumnDefinitions[0].ActualWidth;
            b = grid.ColumnDefinitions[2].ActualWidth;
        }
        else
        {
            a = grid.RowDefinitions[0].ActualHeight;
            b = grid.RowDefinitions[2].ActualHeight;
        }

        return a + b > 0 ? a / (a + b) : 0.5;
    }
}
