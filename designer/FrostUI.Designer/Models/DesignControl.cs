using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace FrostUI.Designer.Models;

public sealed class DesignControl : ObservableObject
{
    private string _name = "按钮1";
    private ControlKind _kind = ControlKind.Button;
    private double _x;
    private double _y;
    private double _width = 120;
    private double _height = 36;
    private string _text = "按钮";
    private double _fontSize = 13;
    private string _fontFamily = string.Empty;
    private string _textBinding = string.Empty;
    private string _textBindingFormat = string.Empty;
    private string _textBindingFallback = string.Empty;
    private string _visibilityBinding = string.Empty;
    private string _enabledBinding = string.Empty;
    private string _xBinding = string.Empty;
    private string _yBinding = string.Empty;
    private string _widthBinding = string.Empty;
    private string _heightBinding = string.Empty;
    private string _opacityBinding = string.Empty;
    private string _fontSizeBinding = string.Empty;
    private string _cornerRadiusBinding = string.Empty;
    private string _valueBinding = string.Empty;
    private string _isOnBinding = string.Empty;
    private string _imagePath = string.Empty;
    private string _imageBinding = string.Empty;
    private string _parentName = string.Empty;
    private string _stackOrientation = "Vertical";
    private string _dockMode = "Top";
    private int _gridRows = 1;
    private int _gridColumns = 1;
    private int _gridRow;
    private int _gridColumn;
    private int _gridRowSpan = 1;
    private int _gridColumnSpan = 1;
    private double _containerPadding = 12;
    private double _containerSpacing = 8;
    private string _componentSource = string.Empty;
    private string _componentVariant = string.Empty;
    private string _componentInstanceId = string.Empty;
    private string _componentSourceControl = string.Empty;
    private string _instanceOverrides = string.Empty;
    private string _styleToken = string.Empty;
    private string _listItems = "项目一\n项目二";
    private string _groupName = "选项组1";
    private string _selectedItem = string.Empty;
    private int _selectedIndex;
    private int _tabIndex = 1;
    private int _paneIndex;
    private string _severity = "Info";
    private string _tableColumns = "名称\n状态\n数量";
    private string _chartType = "Bar";
    private bool _gridEditable = true;
    private bool _gridSortable = true;
    private string _toolTipText = string.Empty;
    private string _contextMenuItems = string.Empty;
    private string _accentColor = string.Empty;
    private double _minimum;
    private double _maximum = 100;
    private double _step = 1;
    private string _validationRule = "None";
    private string _validationMessage = string.Empty;
    private ObservableCollection<AnimationKeyframe> _timeline = [];
    private string _pageAction = "None";
    private string _targetPageId = string.Empty;
    private string _navigationArgs = string.Empty;
    private string _dialogDisplay = "Window";
    private bool _dialogModal = true;
    private string _imageStretch = "Uniform";
    private string _imageCrop = "0,0,1,1";
    private string _animation = "None";
    private string _anchor = "None";
    private int _animationDurationMs = 320;
    private int _animationDelayMs;
    private bool _animationRepeat;
    private double _opacity = 1;
    private double _minWidth;
    private double _minHeight;
    private double _maxWidth = 100000;
    private double _maxHeight = 100000;
    private double _marginLeft;
    private double _marginTop;
    private double _marginRight;
    private double _marginBottom;
    private bool _isBold;
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private bool _isLocked;
    private bool _isOn;
    private double _value = 50;
    private double _cornerRadius = 16;
    private bool _isSelected;
    private Dictionary<string, string> _scripts = new(StringComparer.Ordinal);

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public ControlKind Kind
    {
        get => _kind;
        set
        {
            if (Set(ref _kind, value))
            {
                if (value == ControlKind.Menu && _stackOrientation == "Vertical")
                {
                    _stackOrientation = "Horizontal";
                    OnPropertyChanged(nameof(StackOrientation));
                }

                OnPropertyChanged(nameof(KindTitle));
                OnPropertyChanged(nameof(KindIcon));
                OnPropertyChanged(nameof(SupportsTextEditor));
                OnPropertyChanged(nameof(SupportsFontEditor));
                OnPropertyChanged(nameof(SupportsValueEditor));
                OnPropertyChanged(nameof(SupportsToggleEditor));
                OnPropertyChanged(nameof(SupportsCornerEditor));
                OnPropertyChanged(nameof(SupportsBoldEditor));
                OnPropertyChanged(nameof(SupportsListEditor));
                OnPropertyChanged(nameof(SupportsValidationEditor));
                OnPropertyChanged(nameof(SupportsSelectionEditor));
                OnPropertyChanged(nameof(SupportsSelectedIndexEditor));
                OnPropertyChanged(nameof(SupportsTableColumns));
                OnPropertyChanged(nameof(SupportsGridOptions));
                OnPropertyChanged(nameof(SupportsChartType));
                OnPropertyChanged(nameof(SupportsSeverityEditor));
                OnPropertyChanged(nameof(SupportsExpandedEditor));
                OnPropertyChanged(nameof(SupportsGroupEditor));
                OnPropertyChanged(nameof(SupportsNumberEditor));
                OnPropertyChanged(nameof(SupportsSliderEditor));
                OnPropertyChanged(nameof(SupportsDateEditor));
                OnPropertyChanged(nameof(AvailableEvents));
            }
        }
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, Math.Round(value, 0));
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, Math.Round(value, 0));
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, Math.Max(4, Math.Round(value, 0)));
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, Math.Max(4, Math.Round(value, 0)));
    }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? string.Empty);
    }

    public double FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, Math.Clamp(value, 6, 96));
    }

    public string FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, value ?? string.Empty);
    }

    public string TextBinding
    {
        get => _textBinding;
        set => Set(ref _textBinding, value ?? string.Empty);
    }

    public string TextBindingFormat
    {
        get => _textBindingFormat;
        set => Set(ref _textBindingFormat, value ?? string.Empty);
    }

    public string TextBindingFallback
    {
        get => _textBindingFallback;
        set => Set(ref _textBindingFallback, value ?? string.Empty);
    }

    public string VisibilityBinding
    {
        get => _visibilityBinding;
        set => Set(ref _visibilityBinding, value ?? string.Empty);
    }

    public string EnabledBinding
    {
        get => _enabledBinding;
        set => Set(ref _enabledBinding, value ?? string.Empty);
    }

    public string XBinding
    {
        get => _xBinding;
        set => Set(ref _xBinding, value ?? string.Empty);
    }

    public string YBinding
    {
        get => _yBinding;
        set => Set(ref _yBinding, value ?? string.Empty);
    }

    public string WidthBinding
    {
        get => _widthBinding;
        set => Set(ref _widthBinding, value ?? string.Empty);
    }

    public string HeightBinding
    {
        get => _heightBinding;
        set => Set(ref _heightBinding, value ?? string.Empty);
    }

    public string OpacityBinding
    {
        get => _opacityBinding;
        set => Set(ref _opacityBinding, value ?? string.Empty);
    }

    public string FontSizeBinding
    {
        get => _fontSizeBinding;
        set => Set(ref _fontSizeBinding, value ?? string.Empty);
    }

    public string CornerRadiusBinding
    {
        get => _cornerRadiusBinding;
        set => Set(ref _cornerRadiusBinding, value ?? string.Empty);
    }

    public string ValueBinding
    {
        get => _valueBinding;
        set => Set(ref _valueBinding, value ?? string.Empty);
    }

    public string IsOnBinding
    {
        get => _isOnBinding;
        set => Set(ref _isOnBinding, value ?? string.Empty);
    }

    public bool IsContainer => Kind is ControlKind.Stack or ControlKind.Grid
        or ControlKind.Dock or ControlKind.Scroll or ControlKind.Tabs
        or ControlKind.Expander or ControlKind.SplitView;

    public string ParentName
    {
        get => _parentName;
        set
        {
            if (Set(ref _parentName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsCanvasVisible));
            }
        }
    }

    [JsonIgnore]
    public bool IsCanvasVisible => string.IsNullOrWhiteSpace(ParentName);

    public string StackOrientation
    {
        get => _stackOrientation;
        set => Set(ref _stackOrientation, value == "Horizontal" ? "Horizontal" : "Vertical");
    }

    public string DockMode
    {
        get => _dockMode;
        set => Set(ref _dockMode,
            value is "Top" or "Bottom" or "Left" or "Right" or "Fill" ? value : "Top");
    }

    public int GridRows
    {
        get => _gridRows;
        set => Set(ref _gridRows, Math.Clamp(value, 1, 12));
    }

    public int GridColumns
    {
        get => _gridColumns;
        set => Set(ref _gridColumns, Math.Clamp(value, 1, 12));
    }

    public int GridRow
    {
        get => _gridRow;
        set => Set(ref _gridRow, Math.Max(0, value));
    }

    public int GridColumn
    {
        get => _gridColumn;
        set => Set(ref _gridColumn, Math.Max(0, value));
    }

    public int GridRowSpan
    {
        get => _gridRowSpan;
        set => Set(ref _gridRowSpan, Math.Max(1, value));
    }

    public int GridColumnSpan
    {
        get => _gridColumnSpan;
        set => Set(ref _gridColumnSpan, Math.Max(1, value));
    }

    public double ContainerPadding
    {
        get => _containerPadding;
        set => Set(ref _containerPadding, Math.Clamp(value, 0, 80));
    }

    public double ContainerSpacing
    {
        get => _containerSpacing;
        set => Set(ref _containerSpacing, Math.Clamp(value, 0, 80));
    }

    public string ComponentSource
    {
        get => _componentSource;
        set => Set(ref _componentSource, value ?? string.Empty);
    }

    public string ComponentVariant
    {
        get => _componentVariant;
        set => Set(ref _componentVariant, value ?? string.Empty);
    }

    public string ComponentInstanceId
    {
        get => _componentInstanceId;
        set => Set(ref _componentInstanceId, value ?? string.Empty);
    }

    public string ComponentSourceControl
    {
        get => _componentSourceControl;
        set => Set(ref _componentSourceControl, value ?? string.Empty);
    }

    public string InstanceOverrides
    {
        get => _instanceOverrides;
        set => Set(ref _instanceOverrides, value ?? string.Empty);
    }

    public string StyleToken
    {
        get => _styleToken;
        set => Set(ref _styleToken, value ?? string.Empty);
    }

    public string ListItems
    {
        get => _listItems;
        set => Set(ref _listItems, value ?? string.Empty);
    }

    public string GroupName
    {
        get => _groupName;
        set => Set(ref _groupName, string.IsNullOrWhiteSpace(value) ? "选项组1" : value);
    }

    public string SelectedItem
    {
        get => _selectedItem;
        set => Set(ref _selectedItem, value ?? string.Empty);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Set(ref _selectedIndex, Math.Clamp(value, 0, 100));
    }

    public int TabIndex
    {
        get => _tabIndex;
        set => Set(ref _tabIndex, Math.Clamp(value, 1, 50));
    }

    public int PaneIndex
    {
        get => _paneIndex;
        set => Set(ref _paneIndex, Math.Clamp(value, 0, 1));
    }

    public string Severity
    {
        get => _severity;
        set => Set(ref _severity, value is "Success" or "Warning" or "Error" ? value : "Info");
    }

    public string TableColumns
    {
        get => _tableColumns;
        set => Set(ref _tableColumns, value ?? string.Empty);
    }

    public string ChartType
    {
        get => _chartType;
        set => Set(ref _chartType, value is "Bar" or "Line" or "Pie" ? value : "Bar");
    }

    public bool GridEditable
    {
        get => _gridEditable;
        set => Set(ref _gridEditable, value);
    }

    public bool GridSortable
    {
        get => _gridSortable;
        set => Set(ref _gridSortable, value);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        set => Set(ref _toolTipText, value ?? string.Empty);
    }

    public string ContextMenuItems
    {
        get => _contextMenuItems;
        set => Set(ref _contextMenuItems, value ?? string.Empty);
    }

    public string AccentColor
    {
        get => _accentColor;
        set => Set(ref _accentColor, value ?? string.Empty);
    }

    public double Minimum
    {
        get => _minimum;
        set => Set(ref _minimum, Math.Round(value, 4));
    }

    public double Maximum
    {
        get => _maximum;
        set => Set(ref _maximum, Math.Round(value, 4));
    }

    public double Step
    {
        get => _step;
        set => Set(ref _step, Math.Abs(Math.Round(value, 4)) < 0.0001 ? 1 : Math.Round(value, 4));
    }

    public bool SupportsListEditor => Kind is ControlKind.List or ControlKind.ComboBox
        or ControlKind.Tabs or ControlKind.Menu or ControlKind.DataGrid
        or ControlKind.TreeView or ControlKind.Repeater or ControlKind.Chart;

    public bool SupportsSelectionEditor => Kind is ControlKind.ComboBox or ControlKind.Menu
        or ControlKind.DataGrid or ControlKind.TreeView or ControlKind.Repeater;

    public bool SupportsTableColumns => Kind == ControlKind.DataGrid;

    public bool SupportsGridOptions => Kind == ControlKind.DataGrid;

    public bool SupportsChartType => Kind == ControlKind.Chart;

    public bool SupportsSelectedIndexEditor => Kind == ControlKind.Tabs;

    public bool SupportsSeverityEditor => Kind == ControlKind.InfoBar;

    public bool SupportsExpandedEditor => Kind == ControlKind.Expander;

    public bool SupportsGroupEditor => Kind == ControlKind.RadioButton;

    public bool SupportsNumberEditor => Kind == ControlKind.NumberBox;

    public bool SupportsSliderEditor => Kind is ControlKind.Slider or ControlKind.ProgressBar
        or ControlKind.ProgressRing;

    public bool SupportsDateEditor => Kind == ControlKind.DatePicker;

    public string ValidationRule
    {
        get => _validationRule;
        set => Set(ref _validationRule,
            value is "Required" or "Email" or "Number" ? value : "None");
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => Set(ref _validationMessage, value ?? string.Empty);
    }

    public bool SupportsValidationEditor => Kind is ControlKind.TextBox
        or ControlKind.PasswordBox or ControlKind.TextArea;

    public ObservableCollection<AnimationKeyframe> Timeline
    {
        get => _timeline;
        set => Set(ref _timeline, value ?? []);
    }

    public bool HasTimeline => Timeline.Count > 0;

    public string PageAction
    {
        get => _pageAction;
        set => Set(ref _pageAction,
            value is "Switch" or "Window" or "Dialog" or "Open" or "Close" ? value : "None");
    }

    public string TargetPageId
    {
        get => _targetPageId;
        set => Set(ref _targetPageId, value ?? string.Empty);
    }

    public string NavigationArgs
    {
        get => _navigationArgs;
        set => Set(ref _navigationArgs, value ?? string.Empty);
    }

    public string DialogDisplay
    {
        get => _dialogDisplay;
        set => Set(ref _dialogDisplay, value == "Overlay" ? "Overlay" : "Window");
    }

    public bool DialogModal
    {
        get => _dialogModal;
        set => Set(ref _dialogModal, value);
    }

    public string ImageBinding
    {
        get => _imageBinding;
        set => Set(ref _imageBinding, value ?? string.Empty);
    }

    public double Opacity
    {
        get => _opacity;
        set => Set(ref _opacity, Math.Clamp(value, 0, 1));
    }

    public double MinWidth
    {
        get => _minWidth;
        set => Set(ref _minWidth, Math.Max(0, value));
    }

    public double MinHeight
    {
        get => _minHeight;
        set => Set(ref _minHeight, Math.Max(0, value));
    }

    public double MaxWidth
    {
        get => _maxWidth;
        set => Set(ref _maxWidth, Math.Max(0, value));
    }

    public double MaxHeight
    {
        get => _maxHeight;
        set => Set(ref _maxHeight, Math.Max(0, value));
    }

    public double MarginLeft
    {
        get => _marginLeft;
        set => Set(ref _marginLeft, Math.Max(0, value));
    }

    public double MarginTop
    {
        get => _marginTop;
        set => Set(ref _marginTop, Math.Max(0, value));
    }

    public double MarginRight
    {
        get => _marginRight;
        set => Set(ref _marginRight, Math.Max(0, value));
    }

    public double MarginBottom
    {
        get => _marginBottom;
        set => Set(ref _marginBottom, Math.Max(0, value));
    }

    public string ImagePath
    {
        get => _imagePath;
        set => Set(ref _imagePath, value ?? string.Empty);
    }

    public string ImageStretch
    {
        get => _imageStretch;
        set => Set(ref _imageStretch,
            value is "Uniform" or "UniformToFill" or "Fill" ? value : "Uniform");
    }

    public string ImageCrop
    {
        get => _imageCrop;
        set => Set(ref _imageCrop, string.IsNullOrWhiteSpace(value) ? "0,0,1,1" : value.Trim());
    }

    public string Animation
    {
        get => _animation;
        set => Set(ref _animation,
            value is "FadeIn" or "SlideUp" or "ScaleIn" ? value : "None");
    }

    public string Anchor
    {
        get => _anchor;
        set => Set(ref _anchor,
            value is "Left" or "Top" or "Right" or "Bottom" or "RightBottom"
                or "CenterX" or "CenterY"
                or "StretchHorizontal" or "StretchVertical" or "StretchAll"
                or "StretchLeftRight" or "StretchTopBottom" or "Fill"
                ? value
                : "None");
    }

    public int AnimationDurationMs
    {
        get => _animationDurationMs;
        set => Set(ref _animationDurationMs, Math.Clamp(value, 80, 5000));
    }

    public int AnimationDelayMs
    {
        get => _animationDelayMs;
        set => Set(ref _animationDelayMs, Math.Clamp(value, 0, 5000));
    }

    public bool AnimationRepeat
    {
        get => _animationRepeat;
        set => Set(ref _animationRepeat, value);
    }

    public bool IsBold
    {
        get => _isBold;
        set => Set(ref _isBold, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (Set(ref _isVisible, value))
            {
                OnPropertyChanged(nameof(VisibilityLabel));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (Set(ref _isLocked, value))
            {
                OnPropertyChanged(nameof(LockLabel));
            }
        }
    }

    public bool IsOn
    {
        get => _isOn;
        set => Set(ref _isOn, value);
    }

    public double Value
    {
        get => _value;
        set => Set(ref _value, Math.Clamp(value, -1_000_000, 1_000_000));
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set => Set(ref _cornerRadius, Math.Clamp(value, 0, 64));
    }

    public Dictionary<string, string> Scripts
    {
        get => _scripts;
        set => Set(ref _scripts, value is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(value, StringComparer.Ordinal));
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    [JsonIgnore]
    public string VisibilityLabel => IsVisible ? "显" : "隐";

    [JsonIgnore]
    public string LockLabel => IsLocked ? "锁" : "解";

    [JsonIgnore]
    public string KindTitle => ControlCatalog.Get(Kind).Title;

    [JsonIgnore]
    public string KindIcon => ControlCatalog.Get(Kind).Icon;

    [JsonIgnore]
    public IReadOnlyList<string> AvailableEvents => ControlCatalog.Get(Kind).Events;

    [JsonIgnore]
    public string PrimaryEvent => ControlCatalog.Get(Kind).PrimaryEvent;

    [JsonIgnore]
    public bool SupportsTextEditor => Kind is ControlKind.Button or ControlKind.Label
        or ControlKind.TextBox or ControlKind.Card or ControlKind.PasswordBox
        or ControlKind.TextArea or ControlKind.SearchBox or ControlKind.DatePicker
        or ControlKind.CheckBox or ControlKind.RadioButton or ControlKind.Expander
        or ControlKind.InfoBar;

    [JsonIgnore]
    public bool SupportsImageEditor => Kind == ControlKind.Image;

    public bool SupportsAccentEditor => true;

    [JsonIgnore]
    public bool SupportsFontEditor => SupportsTextEditor;

    [JsonIgnore]
    public bool SupportsBoldEditor => Kind is ControlKind.Label or ControlKind.Card or ControlKind.TextBox;

    [JsonIgnore]
    public bool SupportsValueEditor => Kind is ControlKind.Slider or ControlKind.ProgressBar
        or ControlKind.ProgressRing or ControlKind.NumberBox;

    [JsonIgnore]
    public bool SupportsToggleEditor => Kind is ControlKind.ToggleSwitch or ControlKind.CheckBox
        or ControlKind.RadioButton;

    [JsonIgnore]
    public bool SupportsCornerEditor => Kind is ControlKind.Card or ControlKind.Image;

    public string GetScript(string eventName) =>
        Scripts.TryGetValue(eventName, out var code) ? code : string.Empty;

    public void SetScript(string eventName, string code)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return;
        }

        Scripts[eventName] = code ?? string.Empty;
    }

    public DesignControl Clone(string newName)
    {
        var clone = new DesignControl
        {
            Name = newName,
            Kind = Kind,
            X = X + 16,
            Y = Y + 16,
            Width = Width,
            Height = Height,
            Text = Text,
            FontSize = FontSize,
            FontFamily = FontFamily,
            TextBinding = TextBinding,
            TextBindingFormat = TextBindingFormat,
            TextBindingFallback = TextBindingFallback,
            Opacity = Opacity,
            VisibilityBinding = VisibilityBinding,
            EnabledBinding = EnabledBinding,
            XBinding = XBinding,
            YBinding = YBinding,
            WidthBinding = WidthBinding,
            HeightBinding = HeightBinding,
            ParentName = ParentName,
            StackOrientation = StackOrientation,
            DockMode = DockMode,
            GridRows = GridRows,
            GridColumns = GridColumns,
            GridRow = GridRow,
            GridColumn = GridColumn,
            GridRowSpan = GridRowSpan,
            GridColumnSpan = GridColumnSpan,
            ContainerPadding = ContainerPadding,
            ContainerSpacing = ContainerSpacing,
            ComponentSource = ComponentSource,
            ComponentVariant = ComponentVariant,
            ComponentInstanceId = ComponentInstanceId,
            ComponentSourceControl = ComponentSourceControl,
            InstanceOverrides = InstanceOverrides,
            StyleToken = StyleToken,
            ListItems = ListItems,
            GroupName = GroupName,
            SelectedItem = SelectedItem,
            SelectedIndex = SelectedIndex,
            TabIndex = TabIndex,
            PaneIndex = PaneIndex,
            Severity = Severity,
            TableColumns = TableColumns,
            ChartType = ChartType,
            GridEditable = GridEditable,
            GridSortable = GridSortable,
            ToolTipText = ToolTipText,
            ContextMenuItems = ContextMenuItems,
            AccentColor = AccentColor,
            Minimum = Minimum,
            Maximum = Maximum,
            Step = Step,
            ValidationRule = ValidationRule,
            ValidationMessage = ValidationMessage,
            Timeline = new ObservableCollection<AnimationKeyframe>(
                Timeline.Select(keyframe => keyframe.Clone())),
            PageAction = PageAction,
            TargetPageId = TargetPageId,
            NavigationArgs = NavigationArgs,
            DialogDisplay = DialogDisplay,
            DialogModal = DialogModal,
            OpacityBinding = OpacityBinding,
            FontSizeBinding = FontSizeBinding,
            CornerRadiusBinding = CornerRadiusBinding,
            ValueBinding = ValueBinding,
            IsOnBinding = IsOnBinding,
            ImageBinding = ImageBinding,
            MinWidth = MinWidth,
            MinHeight = MinHeight,
            MaxWidth = MaxWidth,
            MaxHeight = MaxHeight,
            MarginLeft = MarginLeft,
            MarginTop = MarginTop,
            MarginRight = MarginRight,
            MarginBottom = MarginBottom,
            ImagePath = ImagePath,
            ImageStretch = ImageStretch,
            ImageCrop = ImageCrop,
            Animation = Animation,
            Anchor = Anchor,
            AnimationDurationMs = AnimationDurationMs,
            AnimationDelayMs = AnimationDelayMs,
            AnimationRepeat = AnimationRepeat,
            IsBold = IsBold,
            IsVisible = IsVisible,
            IsEnabled = IsEnabled,
            IsLocked = IsLocked,
            IsOn = IsOn,
            Value = Value,
            CornerRadius = CornerRadius,
            Scripts = new Dictionary<string, string>(Scripts, StringComparer.Ordinal),
        };
        return clone;
    }
}
