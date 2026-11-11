using TerminalTabConfigurator.Core.Model;
using TerminalTabConfigurator.Mvvm;

namespace TerminalTabConfigurator.ViewModels;

/// <summary>One layout in the side list: published in settings.json, or a draft.</summary>
public sealed class LayoutEntry : ObservableObject
{
    private readonly string _fallbackName;
    private readonly string? _fallbackActionId;
    private bool _isPublished;
    private bool _isDirty;
    private string? _keys;
    private bool _inNewTabMenu;
    private bool _hasShortcut;

    public LayoutEntry(TabLayout? layout, string name, string? actionId = null, string? error = null)
    {
        Layout = layout;
        _fallbackName = name;
        _fallbackActionId = actionId;
        Error = error;
        if (layout is not null)
        {
            layout.Changed += (_, _) =>
            {
                IsDirty = true;
                Raise(nameof(Name));
                Raise(nameof(PaneCount));
            };
        }
    }

    /// <summary>Null when the action exists in settings.json but could not be read back.</summary>
    public TabLayout? Layout { get; }

    public string? Error { get; }

    public string Name => Layout?.Name is { Length: > 0 } n ? n : _fallbackName;

    public string? ActionId => Layout is null ? _fallbackActionId : Layout.ActionId;

    public int PaneCount => Layout?.Leaves().Count() ?? 0;

    public bool IsPublished { get => _isPublished; set { if (Set(ref _isPublished, value)) RaiseStatus(); } }

    public bool IsDirty { get => _isDirty; set { if (Set(ref _isDirty, value)) RaiseStatus(); } }

    public string? Keys { get => _keys; set => Set(ref _keys, value); }

    public bool InNewTabMenu { get => _inNewTabMenu; set => Set(ref _inNewTabMenu, value); }

    public bool HasShortcut { get => _hasShortcut; set => Set(ref _hasShortcut, value); }

    /// <summary>Name under which it was last published (the desktop shortcut is named after it).</summary>
    public string? PublishedName { get; set; }

    public string Status
    {
        get
        {
            if (Error is not null) return "illisible";
            var panes = PaneCount > 1 ? $"{PaneCount} volets" : "1 volet";
            var state = !IsPublished ? "non publié" : IsDirty ? "modifié" : "publié";
            return $"{panes} · {state}";
        }
    }

    public void RaiseActionId() => Raise(nameof(ActionId));

    private void RaiseStatus() => Raise(nameof(Status));
}
