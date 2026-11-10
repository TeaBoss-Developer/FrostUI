using System.Collections.ObjectModel;
using FrostUI.Interop;

namespace FrostUI.Designer.Models;

public sealed class DesignPage : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "页面";
    private string? _title;
    private DesignPageKind _kind = DesignPageKind.Page;
    private string _dialogDisplay = "Window";
    private string _transition = "Fade";
    private int _transitionDurationMs = 260;
    private double? _width;
    private double? _height;
    private WindowBackdrop? _backdrop;
    private double? _tintOpacity;
    private bool? _scaleContent;
    private bool? _anchorLayout;
    private string? _breakpointSpec;
    private string? _titleBinding;
    private ObservableCollection<DesignControl> _controls = [];
    private ObservableCollection<DesignGuide> _guides = [];
    private Dictionary<string, string> _scripts = new(StringComparer.Ordinal);

    public string Id
    {
        get => _id;
        set => Set(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value);
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, string.IsNullOrWhiteSpace(value) ? "页面" : value);
    }

    public string? Title
    {
        get => _title;
        set => Set(ref _title, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public DesignPageKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public string Transition
    {
        get => _transition;
        set => Set(ref _transition,
            value is "None" or "Fade" or "SlideLeft" or "SlideRight" or "Scale"
                ? value
                : "Fade");
    }

    public int TransitionDurationMs
    {
        get => _transitionDurationMs;
        set => Set(ref _transitionDurationMs, Math.Clamp(value, 80, 2000));
    }

    public string DialogDisplay
    {
        get => _dialogDisplay;
        set => Set(ref _dialogDisplay, value == "Overlay" ? "Overlay" : "Window");
    }

    public double? Width
    {
        get => _width;
        set => Set(ref _width, value is > 0 ? Math.Clamp(value.Value, 360, 4096) : null);
    }

    public double? Height
    {
        get => _height;
        set => Set(ref _height, value is > 0 ? Math.Clamp(value.Value, 280, 4096) : null);
    }

    public WindowBackdrop? Backdrop
    {
        get => _backdrop;
        set => Set(ref _backdrop, value);
    }

    public double? TintOpacity
    {
        get => _tintOpacity;
        set => Set(ref _tintOpacity, value.HasValue ? Math.Clamp(value.Value, 0, 1) : null);
    }

    public bool? ScaleContent
    {
        get => _scaleContent;
        set => Set(ref _scaleContent, value);
    }

    public bool? AnchorLayout
    {
        get => _anchorLayout;
        set => Set(ref _anchorLayout, value);
    }

    public string? BreakpointSpec
    {
        get => _breakpointSpec;
        set => Set(ref _breakpointSpec, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public string? TitleBinding
    {
        get => _titleBinding;
        set => Set(ref _titleBinding, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public ObservableCollection<DesignControl> Controls
    {
        get => _controls;
        set => Set(ref _controls, value ?? []);
    }

    public ObservableCollection<DesignGuide> Guides
    {
        get => _guides;
        set => Set(ref _guides, value ?? []);
    }

    public Dictionary<string, string> Scripts
    {
        get => _scripts;
        set => Set(ref _scripts, value ?? new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public DesignPage Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Name,
        Title = Title,
        Kind = Kind,
        DialogDisplay = DialogDisplay,
        Transition = Transition,
        TransitionDurationMs = TransitionDurationMs,
        Width = Width,
        Height = Height,
        Backdrop = Backdrop,
        TintOpacity = TintOpacity,
        ScaleContent = ScaleContent,
        AnchorLayout = AnchorLayout,
        BreakpointSpec = BreakpointSpec,
        TitleBinding = TitleBinding,
        Controls = new ObservableCollection<DesignControl>(
            Controls.Select(control => control.Clone(control.Name))),
        Guides = new ObservableCollection<DesignGuide>(
            Guides.Select(guide => new DesignGuide { Orientation = guide.Orientation, Position = guide.Position })),
        Scripts = new Dictionary<string, string>(Scripts, StringComparer.Ordinal),
    };
}
