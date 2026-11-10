using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;
using FrostUI.Designer.Models;
using FrostUI.Designer.Scripting;
using FrostUI.Designer.Services;
using FrostUI.Designer.Views;
using FrostUI.Controls;
using FrostUI.Interop;
using FrostUI.Theme;

namespace FrostUI.Designer;

public partial class MainWindow
{
    private const string ControlDragFormat = "FrostUI.Designer.ControlKind";

    private static readonly Dictionary<string, string> BuiltinSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["输出"] = "输出(值)",
        ["log"] = "输出(值)",
        ["提示"] = "提示(文本)",
        ["toast"] = "提示(文本)",
        ["长度"] = "长度(值)",
        ["len"] = "长度(值)",
        ["类型"] = "类型(值)",
        ["type"] = "类型(值)",
        ["文本"] = "文本(值)",
        ["str"] = "文本(值)",
        ["整数"] = "整数(值)",
        ["int"] = "整数(值)",
        ["小数"] = "小数(值)",
        ["double"] = "小数(值)",
        ["逻辑"] = "逻辑(值)",
        ["bool"] = "逻辑(值)",
        ["随机"] = "随机(最小值, 最大值)",
        ["random"] = "随机(最小值, 最大值)",
        ["等待"] = "等待(毫秒)",
        ["wait"] = "等待(毫秒)",
        ["json.解析"] = "JSON.解析(文本)",
        ["json.parse"] = "JSON.解析(文本)",
        ["json.取值"] = "JSON.取值(JSON, 路径)",
        ["json.get"] = "JSON.取值(JSON, 路径)",
        ["json.文本"] = "JSON.文本(JSON, 是否缩进)",
        ["json.stringify"] = "JSON.文本(JSON, 是否缩进)",
        ["http.获取"] = "HTTP.获取(URL)",
        ["http.get"] = "HTTP.获取(URL)",
        ["http.获取json"] = "HTTP.获取JSON(URL)",
        ["http.getjson"] = "HTTP.获取JSON(URL)",
        ["http.提交json"] = "HTTP.提交JSON(URL, JSON文本)",
        ["http.postjson"] = "HTTP.提交JSON(URL, JSON文本)",
    };

    private DesignDocument _document = null!;
    private DesignControl? _selected;
    private readonly List<DesignControl> _selection = [];
    private DesignControl? _scriptControl;
    private string _scriptEvent = "启动";
    private string? _currentPath;
    private bool _dirty;
    private bool _loadingDocument;
    private bool _loadingScript;
    private bool _syncingWindowMaterial;
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private bool _fitCanvas = true;
    private double _canvasZoom = 1.0;
    private bool _panning;
    private Point _panStart;
    private double _panHorizontal;
    private double _panVertical;
    private string _nameBeforeEdit = string.Empty;

    private Point _pointerStart;
    private Point _originStart;
    private bool _boxSelecting;
    private bool _boxAdditive;
    private Point _boxStart;
    private bool _addingGuide;
    private GuideOrientation _guideOrientation;
    private double _guidePreviewPosition;
    private UIElement? _guideCapture;
    private DesignGuide? _draggingGuide;
    private bool _surfaceChanged;
    private readonly Dictionary<DesignControl, Point> _dragOrigins = [];
    private DesignControl? _dragControl;
    private DesignControl? _resizeControl;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private ControlDefinition? _toolDragDefinition;
    private Point _toolDragStart;
    private PreviewWindow? _previewWindow;
    private readonly ObservableCollection<CompletionItem> _completionItems = [];
    private readonly DispatcherTimer _completionTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(110),
    };
    private CompletionResult _completionResult = CompletionResult.Empty(0);
    private bool _insertingCompletion;

    private readonly DesignHistory _history = new();
    private readonly ObservableCollection<HistoryEntry> _historyItems = [];
    private readonly ObservableCollection<ScriptDiagnostic> _scriptDiagnostics = [];
    private readonly ObservableCollection<CommandItem> _commandItems = [];
    private readonly ObservableCollection<ComponentEntry> _components = [];
    private readonly HashSet<int> _breakpoints = [];
    private bool _applyingToken;
    private bool _switchingPage;
    private ComponentLibraryWatcher? _componentWatcher;
    private ScriptReferenceWindow? _referenceWindow;
    private TimelineWindow? _timelineWindow;
    private ICollectionView? _layerView;
    private bool _syncingLayerSelection;
    private readonly DispatcherTimer _historyTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(380),
    };
    private readonly DispatcherTimer _autosaveTimer = new()
    {
        Interval = TimeSpan.FromSeconds(9),
    };
    private readonly DispatcherTimer _diagnosticsTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(280),
    };
    private readonly DispatcherTimer _hoverTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(520),
    };
    private Point _hoverPoint;
    private string _pendingHistoryLabel = "编辑";
    private bool _historyPending;
    private bool _syncingHistoryList;
    private bool _restoringHistory;

    public MainWindow()
    {
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 32);
        Height = Math.Min(Height, workArea.Height - 24);

        CompletionList.ItemsSource = _completionItems;
        _completionTimer.Tick += (_, _) =>
        {
            _completionTimer.Stop();
            ShowCompletions(false);
        };
        HistoryList.ItemsSource = _historyItems;
        ScriptDiagnosticsList.ItemsSource = _scriptDiagnostics;
        CommandList.ItemsSource = _commandItems;
        ComponentList.ItemsSource = _components;
        try
        {
            _componentWatcher = new ComponentLibraryWatcher(ComponentLibraryService.LibraryDirectory);
            _componentWatcher.ComponentChanged += name =>
                Dispatcher.InvokeAsync(() => RefreshInstancesForChangedComponent(name));
            Closed += (_, _) => _componentWatcher?.Dispose();
        }
        catch
        {
            // Live component refresh is optional; manual refresh still works.
        }
        _historyTimer.Tick += (_, _) =>
        {
            _historyTimer.Stop();
            FlushHistory();
        };
        _autosaveTimer.Tick += (_, _) => AutosaveNow();
        _autosaveTimer.Start();
        _diagnosticsTimer.Tick += (_, _) =>
        {
            _diagnosticsTimer.Stop();
            UpdateScriptDiagnostics();
        };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            ShowScriptDocumentation(_hoverPoint);
        };
        ScriptEditor.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, _) => RedrawScriptOverlay()));

        InspectorFontFamilyBox.ItemsSource = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .OrderBy(name => name, StringComparer.CurrentCulture);
        BasicTools.ItemsSource = ControlCatalog.Basic;
        InteractiveTools.ItemsSource = ControlCatalog.Interactive;
        LoadDocument(DesignDocument.CreateSample(), null, keepAutosave: true);
        if (!App.IsAutomationMode)
        {
            TryRecoverAutosave();
        }

        ThemeManager.ThemeChanged += OnThemeChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnClosed;
        UpdateThemeGlyph();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        GridOverlay.Visibility = _showGrid ? Visibility.Visible : Visibility.Collapsed;
        ApplyGridBrush();
        _ = Dispatcher.BeginInvoke(() =>
        {
            FitCanvasToViewport();
            UpdateRulers();
            AppendLog("设计器就绪 · 已载入示例设计", ScriptLogLevel.Success);
            StatusText.Text = "就绪 · 拖入控件开始设计";
        });
    }

    private void FitCanvasToViewport()
    {
        if (_document is null || CanvasScroll.ViewportWidth <= 0 || CanvasScroll.ViewportHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(
            (CanvasScroll.ViewportWidth - 52) / Math.Max(1, _document.WindowWidth),
            (CanvasScroll.ViewportHeight - 44) / Math.Max(1, _document.WindowHeight));
        ApplyCanvasZoom(Math.Clamp(scale, 0.2, 2.2), fit: true);
    }

    private void ApplyCanvasZoom(double zoom, bool fit)
    {
        _canvasZoom = Math.Clamp(zoom, 0.2, 3.0);
        _fitCanvas = fit;
        ArtboardScale.ScaleX = _canvasZoom;
        ArtboardScale.ScaleY = _canvasZoom;
        CanvasZoomText.Text = $"{_canvasZoom * 100:0}%";
        UpdateRulers();
    }

    private void OnGuidesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MarkDirty("参考线");
        RefreshUserGuides();
    }

    private void HorizontalRuler_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        BeginGuide(GuideOrientation.Horizontal, (UIElement)sender, e);

    private void VerticalRuler_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        BeginGuide(GuideOrientation.Vertical, (UIElement)sender, e);

    private void BeginGuide(GuideOrientation orientation, UIElement source, MouseButtonEventArgs e)
    {
        _addingGuide = true;
        _guideOrientation = orientation;
        _guideCapture = source;
        _guidePreviewPosition = 0;
        source.CaptureMouse();
        e.Handled = true;
    }

    private void Ruler_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_addingGuide)
        {
            return;
        }

        var point = e.GetPosition(ControlItems);
        _guidePreviewPosition = _guideOrientation == GuideOrientation.Horizontal
            ? Math.Clamp(point.Y, 0, _document.ContentHeight)
            : Math.Clamp(point.X, 0, _document.WindowWidth);
        RefreshUserGuides(preview: true);
        e.Handled = true;
    }

    private void Ruler_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_addingGuide)
        {
            return;
        }

        var point = e.GetPosition(ControlItems);
        var position = _guideOrientation == GuideOrientation.Horizontal
            ? Math.Clamp(point.Y, 0, _document.ContentHeight)
            : Math.Clamp(point.X, 0, _document.WindowWidth);
        _addingGuide = false;
        _guideCapture?.ReleaseMouseCapture();
        _guideCapture = null;
        _document.Guides.Add(new DesignGuide(_guideOrientation, position));
        RefreshUserGuides();
        CommitHistory("添加参考线");
        StatusText.Text = $"已添加{( _guideOrientation == GuideOrientation.Horizontal ? "水平" : "垂直")}参考线";
        e.Handled = true;
    }

    private void RefreshUserGuides(bool preview = false)
    {
        if (UserGuideLayer is null)
        {
            return;
        }

        UserGuideLayer.Children.Clear();
        if (_document is null)
        {
            return;
        }

        foreach (var guide in _document.Guides)
        {
            AddGuideVisual(guide.Orientation, guide.Position, 0.7, guide);
        }

        if (preview)
        {
            AddGuideVisual(_guideOrientation, _guidePreviewPosition, 0.35);
        }
    }

    private void AddGuideVisual(
        GuideOrientation orientation,
        double position,
        double opacity,
        DesignGuide? guide = null)
    {
        if (guide is not null)
        {
            var hit = new Line
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = 10,
                Tag = guide,
                Cursor = orientation == GuideOrientation.Vertical ? Cursors.SizeWE : Cursors.SizeNS,
            };
            if (orientation == GuideOrientation.Vertical)
            {
                hit.X1 = position;
                hit.X2 = position;
                hit.Y1 = 0;
                hit.Y2 = _document.ContentHeight;
            }
            else
            {
                hit.X1 = 0;
                hit.X2 = _document.WindowWidth;
                hit.Y1 = position;
                hit.Y2 = position;
            }

            UserGuideLayer.Children.Add(hit);
        }

        var brush = (Brush)FindResource("AccentBrush");
        var line = new Line
        {
            Stroke = brush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Opacity = opacity,
            IsHitTestVisible = false,
        };
        if (orientation == GuideOrientation.Vertical)
        {
            line.X1 = position;
            line.X2 = position;
            line.Y1 = 0;
            line.Y2 = _document.ContentHeight;
        }
        else
        {
            line.X1 = 0;
            line.X2 = _document.WindowWidth;
            line.Y1 = position;
            line.Y2 = position;
        }

        UserGuideLayer.Children.Add(line);
    }

    private void UserGuideLayer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { Tag: DesignGuide guide })
        {
            return;
        }

        _draggingGuide = guide;
        _guideCapture = UserGuideLayer;
        UserGuideLayer.CaptureMouse();
        e.Handled = true;
    }

    private void UserGuideLayer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingGuide is null)
        {
            return;
        }

        var point = e.GetPosition(ControlItems);
        _draggingGuide.Position = _draggingGuide.Orientation == GuideOrientation.Vertical
            ? Math.Clamp(point.X, 0, _document.WindowWidth)
            : Math.Clamp(point.Y, 0, _document.ContentHeight);
        RefreshUserGuides();
        e.Handled = true;
    }

    private void UserGuideLayer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingGuide is null)
        {
            return;
        }

        _draggingGuide = null;
        _guideCapture = null;
        UserGuideLayer.ReleaseMouseCapture();
        RefreshUserGuides();
        CommitHistory("移动参考线");
        e.Handled = true;
    }

    private void UserGuideLayer_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { Tag: DesignGuide guide })
        {
            return;
        }

        _document.Guides.Remove(guide);
        RefreshUserGuides();
        CommitHistory("删除参考线");
        StatusText.Text = "已删除参考线";
        e.Handled = true;
    }

    private void UpdateRulers()
    {
        if (HorizontalRuler is null || VerticalRuler is null || _document is null)
        {
            return;
        }

        HorizontalRuler.Children.Clear();
        VerticalRuler.Children.Clear();
        var scale = _canvasZoom;
        var width = _document.WindowWidth * scale;
        var height = _document.WindowHeight * scale;
        HorizontalRuler.Width = width;
        VerticalRuler.Height = height;

        var minor = (Brush)FindResource("TextDisabledBrush");
        var major = (Brush)FindResource("TextTertiaryBrush");
        for (var unit = 0; unit <= _document.WindowWidth; unit += 50)
        {
            var x = unit * scale;
            var isMajor = unit % 100 == 0;
            HorizontalRuler.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = isMajor ? 2 : 10,
                Y2 = 18,
                Stroke = isMajor ? major : minor,
                StrokeThickness = 0.6,
            });
            if (isMajor && unit < _document.WindowWidth)
            {
                var label = new TextBlock
                {
                    Text = unit.ToString("0"),
                    FontSize = 5.5,
                    Foreground = minor,
                };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 1);
                HorizontalRuler.Children.Add(label);
            }
        }

        for (var unit = 0; unit <= _document.WindowHeight; unit += 50)
        {
            var y = unit * scale;
            var isMajor = unit % 100 == 0;
            VerticalRuler.Children.Add(new Line
            {
                X1 = isMajor ? 2 : 10,
                X2 = 18,
                Y1 = y,
                Y2 = y,
                Stroke = isMajor ? major : minor,
                StrokeThickness = 0.6,
            });
            if (isMajor && unit < _document.WindowHeight)
            {
                var label = new TextBlock
                {
                    Text = unit.ToString("0"),
                    FontSize = 5.5,
                    Foreground = minor,
                };
                Canvas.SetLeft(label, 1);
                Canvas.SetTop(label, y + 1);
                VerticalRuler.Children.Add(label);
            }
        }
    }

    private void CanvasScroll_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_fitCanvas)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(FitCanvasToViewport));
        }
    }

    private void CanvasScroll_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitCanvas)
        {
            _ = Dispatcher.BeginInvoke(new Action(FitCanvasToViewport));
        }
    }

    private void CanvasScroll_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        e.Handled = true;
        var factor = e.Delta > 0 ? 1.1 : 0.9;
        ApplyCanvasZoom(_canvasZoom * factor, fit: false);
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) =>
        ApplyCanvasZoom(_canvasZoom * 1.1, fit: false);

    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) =>
        ApplyCanvasZoom(_canvasZoom * 0.9, fit: false);

    private void FitCanvas_OnClick(object sender, RoutedEventArgs e) => FitCanvasToViewport();

    private void CanvasScroll_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            return;
        }

        _panning = true;
        _panStart = e.GetPosition(CanvasScroll);
        _panHorizontal = CanvasScroll.HorizontalOffset;
        _panVertical = CanvasScroll.VerticalOffset;
        CanvasScroll.CaptureMouse();
        e.Handled = true;
    }

    private void CanvasScroll_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        var current = e.GetPosition(CanvasScroll);
        CanvasScroll.ScrollToHorizontalOffset(_panHorizontal - (current.X - _panStart.X));
        CanvasScroll.ScrollToVerticalOffset(_panVertical - (current.Y - _panStart.Y));
        e.Handled = true;
    }

    private void CanvasScroll_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        CanvasScroll.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        AutosaveService.Delete();
        _previewWindow?.Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    public void LoadDesignFromPath(string path)
    {
        var document = DesignSerializer.Load(path);
        LoadDocument(document, path);
    }

    private void LoadDocument(DesignDocument document, string? path, bool keepAutosave = false)
    {
        if (Keyboard.FocusedElement is TextBox)
        {
            Keyboard.ClearFocus();
        }

        _loadingDocument = true;
        DetachDocument();

        _document = document;
        if (ThemeColorHelper.TryParse(document.AccentColor, out var documentAccent))
        {
            ThemeManager.SetAccent(documentAccent);
        }

        _selected = null;
        _selection.Clear();
        _layerView = null;
        _scriptControl = null;
        _scriptEvent = "启动";
        _currentPath = path;
        DataContext = _document;
        RefreshPageSelector();
        ApplyGridBrush();
        _fitCanvas = true;
        _historyPending = false;
        _historyTimer.Stop();
        _ = Dispatcher.BeginInvoke(new Action(FitCanvasToViewport));
        SelectionBox.Visibility = Visibility.Collapsed;
        ClearGuides();

        AttachDocument();
        UpdateWindowMaterialChecks();
        RefreshUserGuides();
        SelectWindow();
        _loadingDocument = false;
        _dirty = false;
        UpdateDirtyUi();
        UpdateStatusSelection();
        UpdateRulers();
        RefreshLayerView();
        _history.Reset(_document, _selected?.Name);
        UpdateHistoryUi();
        if (!keepAutosave)
        {
            AutosaveService.Delete();
        }
    }

    private void RefreshPageSelector()
    {
        if (PageSelector is null)
        {
            return;
        }

        _switchingPage = true;
        PageSelector.ItemsSource = _document.Pages;
        PageSelector.SelectedIndex = _document.ActivePageIndex;
        RefreshPageBackdropBox();
        _switchingPage = false;
    }

    private void RefreshPageBackdropBox()
    {
        if (PageBackdropBox is null)
        {
            return;
        }

        _switchingPage = true;
        PageBackdropBox.ItemsSource = new object?[]
        {
            "继承工程",
            FrostUI.Interop.WindowBackdrop.Acrylic,
            FrostUI.Interop.WindowBackdrop.Mica,
            FrostUI.Interop.WindowBackdrop.MicaAlt,
            FrostUI.Interop.WindowBackdrop.Blur,
        };
        PageBackdropBox.SelectedItem = _document.ActivePage.Backdrop is null
            ? "继承工程"
            : _document.ActivePage.Backdrop;
        _switchingPage = false;
    }

    private void PageBackdropBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switchingPage || PageBackdropBox.SelectedItem is null)
        {
            return;
        }

        _document.ActivePage.Backdrop = PageBackdropBox.SelectedItem is FrostUI.Interop.WindowBackdrop backdrop
            ? backdrop
            : null;
    }

    private void PageSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switchingPage || PageSelector.SelectedIndex < 0)
        {
            return;
        }

        _document.ActivatePage(PageSelector.SelectedIndex);
        RefreshPageBackdropBox();
        _selected = null;
        _selection.Clear();
        _layerView = null;
        ClearGuides();
        RefreshUserGuides();
        RefreshLayerView();
        SelectWindow();
        UpdateStatusSelection();
        StatusText.Text = $"已切换到页面：{_document.ActivePage.Name}";
    }

    private void AddPage_OnClick(object sender, RoutedEventArgs e)
    {
        var page = new DesignPage { Name = $"页面 {_document.Pages.Count + 1}" };
        _document.Pages.Add(page);
        _document.ActivatePage(_document.Pages.Count - 1);
        RefreshPageSelector();
        _selected = null;
        _selection.Clear();
        SelectWindow();
        RefreshLayerView();
        CommitHistory("新建页面");
        StatusText.Text = $"已新建页面：{page.Name}";
    }

    private void CopyPage_OnClick(object sender, RoutedEventArgs e)
    {
        var copy = _document.ActivePage.Clone();
        copy.Name = $"{_document.ActivePage.Name} 副本";
        var index = _document.ActivePageIndex + 1;
        _document.Pages.Insert(index, copy);
        _document.ActivatePage(index);
        RefreshPageSelector();
        _selected = null;
        _selection.Clear();
        RefreshLayerView();
        SelectWindow();
        CommitHistory("复制页面");
    }

    private void MovePageUp_OnClick(object sender, RoutedEventArgs e) => MovePage(-1);

    private void MovePageDown_OnClick(object sender, RoutedEventArgs e) => MovePage(1);

    private void MovePage(int delta)
    {
        var index = _document.ActivePageIndex;
        var target = index + delta;
        if (target < 0 || target >= _document.Pages.Count)
        {
            return;
        }

        var page = _document.Pages[index];
        _document.Pages.RemoveAt(index);
        _document.Pages.Insert(target, page);
        _document.ActivatePage(target);
        RefreshPageSelector();
        CommitHistory(delta < 0 ? "页面上移" : "页面下移");
    }

    private void DeletePage_OnClick(object sender, RoutedEventArgs e)
    {
        if (_document.Pages.Count <= 1)
        {
            StatusText.Text = "至少保留一个页面";
            return;
        }

        var page = _document.ActivePage;
        if (FrostMessageBox.Show(
                this,
                $"确定删除页面“{page.Name}”？页面内控件和脚本会一起删除。",
                "删除页面",
                FrostMessageBoxButtons.YesNo,
                danger: true) != FrostMessageBoxResult.Yes)
        {
            return;
        }

        var index = _document.ActivePageIndex;
        _document.Pages.RemoveAt(index);
        _document.ActivatePage(Math.Clamp(index, 0, _document.Pages.Count - 1));
        RefreshPageSelector();
        _selected = null;
        _selection.Clear();
        ClearGuides();
        RefreshUserGuides();
        RefreshLayerView();
        SelectWindow();
        CommitHistory("删除页面");
    }

    private void AttachDocument()
    {
        _document.PropertyChanged += OnDocumentChanged;
        _document.Controls.CollectionChanged += OnControlsCollectionChanged;
        _document.Guides.CollectionChanged += OnGuidesCollectionChanged;
        foreach (var control in _document.Controls)
        {
            control.PropertyChanged += OnControlChanged;
        }
    }

    private void DetachDocument()
    {
        if (_document is null)
        {
            return;
        }

        _document.PropertyChanged -= OnDocumentChanged;
        _document.Controls.CollectionChanged -= OnControlsCollectionChanged;
        _document.Guides.CollectionChanged -= OnGuidesCollectionChanged;
        foreach (var control in _document.Controls)
        {
            control.PropertyChanged -= OnControlChanged;
        }
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DesignDocument.ControlCountText) or
            nameof(DesignDocument.ContentHeight) or
            nameof(DesignDocument.WindowSizeText))
        {
            UpdateStatusSelection();
            return;
        }

        if (e.PropertyName == nameof(DesignDocument.Backdrop))
        {
            UpdateWindowMaterialChecks();
        }

        MarkDirty(e.PropertyName == nameof(DesignDocument.Backdrop) ? "窗口材质" : "窗口属性");
        if (e.PropertyName == nameof(DesignDocument.GridSize))
        {
            ApplyGridBrush();
        }

        UpdateStatusSelection();
        if (e.PropertyName is nameof(DesignDocument.WindowWidth) or nameof(DesignDocument.WindowHeight))
        {
            UpdateRulers();
        }

        if (_fitCanvas &&
            e.PropertyName is nameof(DesignDocument.WindowWidth) or nameof(DesignDocument.WindowHeight))
        {
            _ = Dispatcher.BeginInvoke(new Action(FitCanvasToViewport));
        }
    }

    private void OnControlsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DesignControl control in e.OldItems)
            {
                control.PropertyChanged -= OnControlChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DesignControl control in e.NewItems)
            {
                control.PropertyChanged += OnControlChanged;
            }
        }

        _selection.RemoveAll(control => !_document.Controls.Contains(control));
        if (_selected is not null && !_document.Controls.Contains(_selected))
        {
            _selected = null;
            SelectWindow();
        }

        MarkDirty("控件列表");
        RefreshLayerView();
    }

    private void OnControlChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignControl.IsSelected))
        {
            return;
        }

        MarkDirty("属性修改");
        if (sender is DesignControl changed && ReferenceEquals(changed, _selected))
        {
            if (e.PropertyName == nameof(DesignControl.Name) && ReferenceEquals(_scriptControl, changed))
            {
                ScriptTargetText.Text = $"{changed.Name} · {_scriptEvent}";
            }

            UpdateStatusSelection();
        }

        if (e.PropertyName is nameof(DesignControl.Name) or
            nameof(DesignControl.IsVisible) or
            nameof(DesignControl.IsLocked))
        {
            _layerView?.Refresh();
            RefreshLayerSelection();
        }
    }

    private void MarkDirty(string? historyLabel = null)
    {
        if (_loadingDocument || _restoringHistory)
        {
            return;
        }

        if (!_dirty)
        {
            _dirty = true;
            UpdateDirtyUi();
        }

        ScheduleHistory(historyLabel);
    }

    private void UpdateDirtyUi()
    {
        DirtyDot.Visibility = _dirty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearDirty()
    {
        _dirty = false;
        UpdateDirtyUi();
    }

    // ============================================================
    // Component snippets
    // ============================================================

    private void ComponentsButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshComponents();
        ComponentsPopup.IsOpen = true;
    }

    private void RefreshComponents()
    {
        var search = ComponentSearchBox?.Text?.Trim() ?? string.Empty;
        _components.Clear();
        foreach (var component in ComponentLibraryService.List())
        {
            if (search.Length == 0 ||
                component.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                _components.Add(component);
            }
        }

        ComponentList.SelectedIndex = _components.Count > 0 ? 0 : -1;
        if (ComponentCountText is not null)
        {
            ComponentCountText.Text = $"{_components.Count} 个 · 双击插入";
        }
    }

    private void RefreshComponents_OnClick(object sender, RoutedEventArgs e) => RefreshComponents();

    private void DuplicateComponent_OnClick(object sender, RoutedEventArgs e)
    {
        if (ComponentList.SelectedItem is not ComponentEntry entry)
        {
            return;
        }

        try
        {
            ComponentLibraryService.Duplicate(entry);
            RefreshComponents();
            AppendLog($"已复制组件：{entry.Name}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"复制组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void ImportComponent_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入组件",
            Filter = "FrostUI 组件 (*.frostui.json)|*.frostui.json|JSON (*.json)|*.json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ComponentLibraryService.Import(dialog.FileName);
            RefreshComponents();
            AppendLog($"已导入组件：{dialog.FileName}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"导入组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void ExportComponent_OnClick(object sender, RoutedEventArgs e)
    {
        if (ComponentList.SelectedItem is not ComponentEntry entry)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出组件",
            Filter = "FrostUI 组件 (*.frostui.json)|*.frostui.json",
            FileName = entry.Name + ".frostui.json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ComponentLibraryService.Export(entry, dialog.FileName);
            AppendLog($"已导出组件：{dialog.FileName}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"导出组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void OpenComponentsFolder_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{ComponentLibraryService.LibraryDirectory}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppendLog($"打开组件目录失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void ComponentSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        RefreshComponents();

    private void RenameComponent_OnClick(object sender, RoutedEventArgs e)
    {
        if (ComponentList.SelectedItem is not ComponentEntry entry)
        {
            StatusText.Text = "请选择要重命名的组件";
            return;
        }

        var name = ComponentNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "请在名称框输入新名称";
            return;
        }

        try
        {
            ComponentLibraryService.Rename(entry, name);
            ComponentNameBox.Text = string.Empty;
            RefreshComponents();
            AppendLog($"组件已重命名：{entry.Name} → {name}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"重命名组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void DeleteComponent_OnClick(object sender, RoutedEventArgs e)
    {
        if (ComponentList.SelectedItem is not ComponentEntry entry)
        {
            StatusText.Text = "请选择要删除的组件";
            return;
        }

        var result = FrostMessageBox.Show(
            this,
            $"确定删除组件“{entry.Name}”？此操作只删除本机组件库文件。",
            "删除组件",
            FrostMessageBoxButtons.YesNo,
            danger: true);
        if (result != FrostMessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            ComponentLibraryService.Delete(entry);
            RefreshComponents();
            AppendLog($"已删除组件：{entry.Name}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            AppendLog($"删除组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void SaveComponent_OnClick(object sender, RoutedEventArgs e)
    {
        var targets = _selection.Count > 0
            ? _selection.ToList()
            : _selected is null ? [] : new List<DesignControl> { _selected };
        if (targets.Count == 0)
        {
            StatusText.Text = "请先选择要保存为组件的控件";
            return;
        }

        var name = ComponentNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"组件 {DateTime.Now:HHmmss}";
        }

        try
        {
            ComponentLibraryService.Save(name, targets);
            ComponentNameBox.Text = string.Empty;
            RefreshComponents();
            AppendLog($"已保存组件：{name}（{targets.Count} 个控件）", ScriptLogLevel.Success);
            StatusText.Text = $"已保存组件 {name}";
        }
        catch (Exception ex)
        {
            AppendLog($"保存组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void InsertComponent_OnClick(object sender, RoutedEventArgs e) => InsertSelectedComponent();

    private void InsertComponentInstance_OnClick(object sender, RoutedEventArgs e) =>
        InsertSelectedComponent(asInstance: true);

    private void ComponentList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertSelectedComponent();
        e.Handled = true;
    }

    private void InsertSelectedComponent(bool asInstance = false)
    {
        if (ComponentList.SelectedItem is not ComponentEntry entry)
        {
            StatusText.Text = "请选择组件";
            return;
        }

        try
        {
            var variantInput = ComponentVariantBox.Text.Trim();
            var source = entry.Name;
            var variant = variantInput;
            var at = source.IndexOf('@');
            if (at >= 0)
            {
                variant = variantInput.Length > 0 ? variantInput : source[(at + 1)..];
                source = source[..at];
            }

            var resolved = asInstance
                ? ComponentLibraryService.Resolve(source, variant)
                : null;
            var controls = ComponentLibraryService.Load(resolved ?? entry);
            var instanceId = asInstance ? Guid.NewGuid().ToString("N") : string.Empty;
            var clones = new List<DesignControl>();
            foreach (var control in controls)
            {
                var clone = control.Clone(ControlCatalog.NextName(control.Kind, _document.Controls));
                clone.X += 20;
                clone.Y += 20;
                if (asInstance)
                {
                    clone.ComponentSource = source;
                    clone.ComponentVariant = variant;
                    clone.ComponentInstanceId = instanceId;
                    clone.ComponentSourceControl = control.Name;
                }

                _document.Controls.Add(clone);
                clones.Add(clone);
            }

            SelectControl(clones[0]);
            foreach (var clone in clones.Skip(1))
            {
                SelectControl(clone, additive: true);
            }

            CommitHistory("插入组件");
            ComponentsPopup.IsOpen = false;
            AppendLog($"插入组件：{entry.Name}（{clones.Count} 个控件）", ScriptLogLevel.Success);
            StatusText.Text = $"已插入组件 {entry.Name}";
        }
        catch (Exception ex)
        {
            AppendLog($"插入组件失败：{ex.Message}", ScriptLogLevel.Error);
        }
    }

    private void RefreshInstancesForChangedComponent(string changedName)
    {
        var at = changedName.IndexOf('@');
        var source = at >= 0 ? changedName[..at] : changedName;
        var variant = at >= 0 ? changedName[(at + 1)..] : string.Empty;
        var groups = _document.Controls
            .Where(control => !string.IsNullOrWhiteSpace(control.ComponentInstanceId) &&
                              (string.Equals(control.ComponentSource, source, StringComparison.Ordinal) ||
                               string.Equals(control.ComponentSource, changedName, StringComparison.Ordinal)) &&
                              (variant.Length == 0 ||
                               string.Equals(control.ComponentVariant, variant, StringComparison.Ordinal)))
            .GroupBy(control => control.ComponentInstanceId)
            .ToList();
        if (groups.Count == 0)
        {
            return;
        }

        foreach (var group in groups)
        {
            AutoRefreshInstanceGroup(group.Key);
        }

        CommitHistory("组件更新，自动刷新实例");
        SelectWindow();
        StatusText.Text = $"组件 {changedName} 已更新，自动刷新 {groups.Count} 个实例";
    }

    private void AutoRefreshInstanceGroup(string instanceId)
    {
        var group = _document.Controls
            .Where(control => string.Equals(control.ComponentInstanceId, instanceId, StringComparison.Ordinal))
            .ToList();
        if (group.Count == 0)
        {
            return;
        }

        var first = group[0];
        var entry = ComponentLibraryService.Resolve(first.ComponentSource, first.ComponentVariant);
        if (entry is null)
        {
            return;
        }

        var anchorX = group.Min(control => control.X);
        var anchorY = group.Min(control => control.Y);
        var overrides = group
            .Where(control => !string.IsNullOrWhiteSpace(control.ComponentSourceControl))
            .GroupBy(control => control.ComponentSourceControl)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First().InstanceOverrides);
        foreach (var control in group)
        {
            _document.Controls.Remove(control);
        }

        foreach (var control in ComponentLibraryService.Load(entry))
        {
            var clone = control.Clone(ControlCatalog.NextName(control.Kind, _document.Controls));
            clone.X = anchorX + control.X;
            clone.Y = anchorY + control.Y;
            clone.ComponentSource = first.ComponentSource;
            clone.ComponentVariant = first.ComponentVariant;
            clone.ComponentInstanceId = instanceId;
            clone.ComponentSourceControl = control.Name;
            if (overrides.TryGetValue(control.Name, out var instanceOverrides) &&
                !string.IsNullOrWhiteSpace(instanceOverrides))
            {
                clone.InstanceOverrides = instanceOverrides;
                ApplyInstanceOverrides(clone, instanceOverrides);
            }

            _document.Controls.Add(clone);
        }
    }

    private void RefreshComponentInstance_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null ||
            string.IsNullOrWhiteSpace(_selected.ComponentSource) ||
            string.IsNullOrWhiteSpace(_selected.ComponentInstanceId))
        {
            StatusText.Text = "请选择一个组件实例";
            return;
        }

        var group = _document.Controls
            .Where(control => string.Equals(
                control.ComponentInstanceId,
                _selected.ComponentInstanceId,
                StringComparison.Ordinal))
            .ToList();
        if (group.Count == 0)
        {
            return;
        }

        var entry = ComponentLibraryService.Resolve(
            _selected.ComponentSource,
            _selected.ComponentVariant);
        if (entry is null)
        {
            AppendLog($"找不到组件来源：{_selected.ComponentSource}", ScriptLogLevel.Error);
            return;
        }

        var anchorX = group.Min(control => control.X);
        var anchorY = group.Min(control => control.Y);
        var instanceId = _selected.ComponentInstanceId;
        var source = _selected.ComponentSource;
        var variant = _selected.ComponentVariant;
        var overrides = group
            .Where(control => !string.IsNullOrWhiteSpace(control.ComponentSourceControl))
            .GroupBy(control => control.ComponentSourceControl)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping.First().InstanceOverrides,
                StringComparer.Ordinal);
        foreach (var control in group)
        {
            _document.Controls.Remove(control);
        }

        var controls = ComponentLibraryService.Load(entry);
        var clones = new List<DesignControl>();
        foreach (var control in controls)
        {
            var clone = control.Clone(ControlCatalog.NextName(control.Kind, _document.Controls));
            clone.X = anchorX + control.X;
            clone.Y = anchorY + control.Y;
            clone.ComponentSource = source;
            clone.ComponentVariant = variant;
            clone.ComponentInstanceId = instanceId;
            clone.ComponentSourceControl = control.Name;
            if (overrides.TryGetValue(control.Name, out var instanceOverrides) &&
                !string.IsNullOrWhiteSpace(instanceOverrides))
            {
                clone.InstanceOverrides = instanceOverrides;
                ApplyInstanceOverrides(clone, instanceOverrides);
            }

            _document.Controls.Add(clone);
            clones.Add(clone);
        }

        if (clones.Count > 0)
        {
            SelectControl(clones[0]);
            foreach (var clone in clones.Skip(1))
            {
                SelectControl(clone, additive: true);
            }
        }

        CommitHistory("刷新组件实例");
        AppendLog($"已刷新组件实例：{source}{(variant.Length > 0 ? "@" + variant : string.Empty)}", ScriptLogLevel.Success);
    }

    private static void ApplyInstanceOverrides(DesignControl control, string json)
    {
        try
        {
            var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (values is null)
            {
                return;
            }

            if (values.TryGetValue("text", out var text)) control.Text = text;
            if (values.TryGetValue("width", out var widthText) && double.TryParse(widthText, out var width))
                control.Width = Math.Clamp(width, 20, 10000);
            if (values.TryGetValue("height", out var heightText) && double.TryParse(heightText, out var height))
                control.Height = Math.Clamp(height, 20, 10000);
            if (values.TryGetValue("fontSize", out var fontText) && double.TryParse(fontText, out var font))
                control.FontSize = Math.Clamp(font, 1, 300);
            if (values.TryGetValue("isBold", out var boldText)) control.IsBold = boldText == "1";
            if (values.TryGetValue("cornerRadius", out var cornerText) && double.TryParse(cornerText, out var corner))
                control.CornerRadius = Math.Clamp(corner, 0, 200);
            if (values.TryGetValue("opacity", out var opacityText) && double.TryParse(opacityText, out var opacity))
                control.Opacity = Math.Clamp(opacity, 0, 1);
        }
        catch
        {
            // Invalid override JSON should not break instance refresh.
        }
    }

    private void CaptureInstanceOverride_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || string.IsNullOrWhiteSpace(_selected.ComponentInstanceId))
        {
            StatusText.Text = "请选择一个组件实例控件";
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["text"] = _selected.Text,
            ["width"] = _selected.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["height"] = _selected.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["fontSize"] = _selected.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["isBold"] = _selected.IsBold ? "1" : "0",
            ["cornerRadius"] = _selected.CornerRadius.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["opacity"] = _selected.Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        _selected.InstanceOverrides = System.Text.Json.JsonSerializer.Serialize(values);
        CommitHistory("记录实例覆盖");
        StatusText.Text = "已记录实例覆盖";
    }

    private void ClearInstanceOverride_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || string.IsNullOrWhiteSpace(_selected.ComponentInstanceId))
        {
            StatusText.Text = "请选择一个组件实例控件";
            return;
        }

        _selected.InstanceOverrides = string.Empty;
        CommitHistory("清除实例覆盖");
        StatusText.Text = "已清除实例覆盖";
    }

    private void DetachComponentInstance_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || string.IsNullOrWhiteSpace(_selected.ComponentInstanceId))
        {
            StatusText.Text = "请选择一个组件实例";
            return;
        }

        var instanceId = _selected.ComponentInstanceId;
        var group = _document.Controls
            .Where(control => string.Equals(
                control.ComponentInstanceId,
                instanceId,
                StringComparison.Ordinal))
            .ToList();
        foreach (var control in group)
        {
            control.ComponentSource = string.Empty;
            control.ComponentVariant = string.Empty;
            control.ComponentInstanceId = string.Empty;
        }

        CommitHistory("分离组件实例");
        StatusText.Text = $"已分离 {group.Count} 个控件";
    }

    // ============================================================
    // Command palette
    // ============================================================

    private void ShowCommandPalette()
    {
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            CloseCommandPalette();
            return;
        }

        BuildCommands();
        CommandSearchBox.Text = string.Empty;
        FilterCommands(string.Empty);
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        CommandSearchBox.Focus();
        CommandSearchBox.SelectAll();
        CommandList.SelectedIndex = _commandItems.Count > 0 ? 0 : -1;
    }

    private void CloseCommandPalette()
    {
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        ScriptEditor.Focus();
    }

    private void BuildCommands()
    {
        _allCommands =
        [
            new CommandItem("新建设计", "Ctrl+N", () => NewProject_OnClick(this, new RoutedEventArgs())),
            new CommandItem("打开设计", "Ctrl+O", () => OpenProject_OnClick(this, new RoutedEventArgs())),
            new CommandItem("保存设计", "Ctrl+S", () => SaveProject()),
            new CommandItem("运行预览", "F5", () => OpenPreview()),
            new CommandItem("导出 EXE", "release", () => ExportExe_OnClick(this, new RoutedEventArgs())),
            new CommandItem("切换主题", "light/dark", () => ThemeButton_OnClick(this, new RoutedEventArgs())),
            new CommandItem("窗口属性", "", () => SelectWindow()),
            new CommandItem("打开图层", "", () => LayersButton_OnClick(this, new RoutedEventArgs())),
            new CommandItem("打开对齐", "2+ controls", () => AlignButton_OnClick(this, new RoutedEventArgs())),
            new CommandItem("查找 / 替换", "Ctrl+F", () => ShowFindReplace(false)),
            new CommandItem("格式化脚本", "Ctrl+Shift+F", FormatScript),
            new CommandItem("校验脚本", "Ctrl+Enter", () => ValidateScript_OnClick(this, new RoutedEventArgs())),
            new CommandItem("脚本参考", "函数/控件", () => ScriptReference_OnClick(this, new RoutedEventArgs())),
            new CommandItem("撤销", "Ctrl+Z", UndoHistory),
            new CommandItem("重做", "Ctrl+Y", RedoHistory),
            new CommandItem("复制选中控件", "Ctrl+D", DuplicateSelected),
            new CommandItem("删除选中控件", "Delete", DeleteSelected),
            new CommandItem("清空运行输出", "", () => ClearOutput_OnClick(this, new RoutedEventArgs())),
            new CommandItem("全部选择控件", "Ctrl+A", SelectAllControls),
            new CommandItem("适应画布", "", FitCanvasToViewport),
            new CommandItem("关闭命令面板", "Esc", CloseCommandPalette),
        ];

        foreach (var definition in ControlCatalog.Basic.Concat(ControlCatalog.Interactive))
        {
            var kind = definition.Kind;
            _allCommands.Add(new CommandItem(
                $"添加 {definition.Title}",
                "控件",
                () => AddControlAtCenter(kind)));
        }
    }

    private List<CommandItem> _allCommands = [];

    private void FilterCommands(string search)
    {
        var query = search.Trim();
        var filtered = _allCommands
            .Where(item => query.Length == 0 ||
                           item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           item.Hint.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _commandItems.Clear();
        foreach (var item in filtered)
        {
            _commandItems.Add(item);
        }

        CommandList.SelectedIndex = _commandItems.Count > 0 ? 0 : -1;
    }

    private void CommandSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        FilterCommands(CommandSearchBox.Text);

    private void CommandSearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            MoveCommandSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelectedCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void CommandList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelectedCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseCommandPalette();
            e.Handled = true;
        }
    }

    private void CommandList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelectedCommand();
        e.Handled = true;
    }

    private void MoveCommandSelection(int delta)
    {
        if (_commandItems.Count == 0)
        {
            return;
        }

        CommandList.SelectedIndex = Math.Clamp(
            CommandList.SelectedIndex + delta,
            0,
            _commandItems.Count - 1);
        CommandList.ScrollIntoView(CommandList.SelectedItem);
    }

    private void ExecuteSelectedCommand()
    {
        if (CommandList.SelectedItem is not CommandItem item)
        {
            return;
        }

        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
        item.Execute();
    }

    // ============================================================
    // History, autosave and recovery
    // ============================================================

    private void ScheduleHistory(string? label)
    {
        if (_loadingDocument || _restoringHistory)
        {
            return;
        }

        _pendingHistoryLabel = string.IsNullOrWhiteSpace(label) ? "编辑" : label;
        _historyPending = true;
        _historyTimer.Stop();
        _historyTimer.Start();
    }

    private void CommitHistory(string label)
    {
        if (_loadingDocument || _restoringHistory)
        {
            return;
        }

        _pendingHistoryLabel = label;
        _historyPending = true;
        FlushHistory();
    }

    private void FlushHistory()
    {
        _historyTimer.Stop();
        if (!_historyPending || _document is null)
        {
            return;
        }

        _historyPending = false;
        if (_history.Capture(_document, _selected?.Name, _pendingHistoryLabel))
        {
            UpdateHistoryUi();
        }
    }

    private void UpdateHistoryUi()
    {
        if (UndoButton is null || RedoButton is null)
        {
            return;
        }

        UndoButton.IsEnabled = _history.CanUndo;
        RedoButton.IsEnabled = _history.CanRedo;
        HistoryCountText.Text = $"{Math.Max(0, _history.CurrentIndex + 1)}/{_history.Entries.Count}";
        if (HistoryPopup.IsOpen)
        {
            RefreshHistoryItems();
        }
    }

    private void RefreshHistoryItems()
    {
        _syncingHistoryList = true;
        _historyItems.Clear();
        foreach (var entry in _history.Entries)
        {
            _historyItems.Add(entry);
        }

        HistoryList.SelectedIndex = _history.CurrentIndex;
        _syncingHistoryList = false;
    }

    private void HistoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshHistoryItems();
        HistoryPopup.IsOpen = true;
    }

    private void HistoryList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingHistoryList || HistoryList.SelectedIndex < 0)
        {
            return;
        }

        var entry = _history.JumpTo(HistoryList.SelectedIndex);
        if (entry is not null)
        {
            RestoreHistory(entry);
        }
    }

    private void Undo_OnClick(object sender, RoutedEventArgs e) => UndoHistory();

    private void Redo_OnClick(object sender, RoutedEventArgs e) => RedoHistory();

    private void UndoHistory()
    {
        FlushHistory();
        var entry = _history.Undo();
        if (entry is not null)
        {
            RestoreHistory(entry);
            AppendLog("撤销上一步编辑", ScriptLogLevel.Info);
        }
    }

    private void RedoHistory()
    {
        var entry = _history.Redo();
        if (entry is not null)
        {
            RestoreHistory(entry);
            AppendLog("重做编辑", ScriptLogLevel.Info);
        }
    }

    private void RestoreHistory(HistoryEntry entry)
    {
        _restoringHistory = true;
        _loadingDocument = true;
        try
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                Keyboard.ClearFocus();
            }

            ClearInspectorBinding();
            DetachDocument();
            _selected = null;
            _selection.Clear();
            _scriptControl = null;
            _scriptEvent = "启动";
            _document = DesignSerializer.Deserialize(entry.Json);
            DataContext = _document;
            AttachDocument();
            UpdateWindowMaterialChecks();
            SelectionBox.Visibility = Visibility.Collapsed;
            ClearGuides();
            RefreshUserGuides();
            _layerView = null;
            RefreshLayerView();
            _loadingDocument = false;
            if (!string.IsNullOrWhiteSpace(entry.SelectedName))
            {
                SelectControlByName(entry.SelectedName);
            }

            if (_selected is null)
            {
                SelectWindow();
            }

            _dirty = !_history.IsAtSavedState;
            UpdateDirtyUi();
            UpdateStatusSelection();
            UpdateHistoryUi();
            if (_history.IsAtSavedState)
            {
                AutosaveService.Delete();
            }
        }
        finally
        {
            _loadingDocument = false;
            _restoringHistory = false;
        }
    }

    private bool _autosaveRecoveryScheduled;

    private void TryRecoverAutosave()
    {
        if (!IsVisible && !_autosaveRecoveryScheduled)
        {
            _autosaveRecoveryScheduled = true;
            Loaded += (_, _) => TryRecoverAutosave();
            return;
        }

        var recovered = AutosaveService.TryLoad(out var timestamp);
        if (recovered is null)
        {
            return;
        }

        var result = FrostMessageBox.Show(
            this,
            $"发现 {timestamp:yyyy-MM-dd HH:mm:ss} 的自动保存，是否恢复？\n\n" +
            "选择“否”会删除这份自动保存。",
            "FrostUI Designer · 恢复编辑",
            FrostMessageBoxButtons.YesNo);
        if (result == FrostMessageBoxResult.Yes)
        {
            LoadDocument(recovered, null, keepAutosave: true);
            _dirty = true;
            UpdateDirtyUi();
            AppendLog("已恢复自动保存", ScriptLogLevel.Warning);
            StatusText.Text = "已恢复自动保存";
        }
        else
        {
            AutosaveService.Delete();
        }
    }

    private void AutosaveNow()
    {
        if (!_dirty || _document is null || _loadingDocument || _restoringHistory)
        {
            return;
        }

        AutosaveService.Save(_document);
        if (AutosaveStatusText is not null)
        {
            AutosaveStatusText.Text = $"自动保存 {DateTime.Now:HH:mm:ss}";
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_dirty)
        {
            return true;
        }

        var result = FrostMessageBox.Show(
            this,
            "当前设计还有未保存的修改，继续将丢失这些修改。是否继续？",
            "FrostUI Designer",
            FrostMessageBoxButtons.YesNo,
            danger: true);
        return result == FrostMessageBoxResult.Yes;
    }

    // ============================================================
    // File
    // ============================================================

    private void NewProject_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var document = new DesignDocument
        {
            Name = "未命名设计",
            WindowTitle = "FrostUI 设计",
            WindowWidth = 980,
            WindowHeight = 620,
            Description = "从左侧拖入控件开始。",
        };
        LoadDocument(document, null);
        AppendLog("已新建设计", ScriptLogLevel.Info);
        StatusText.Text = "就绪 · 新建设计";
    }

    private void OpenProject_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "FrostUI Designer (*.frostui.json)|*.frostui.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "打开 FrostUI 设计",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var document = DesignSerializer.Load(dialog.FileName);
            LoadDocument(document, dialog.FileName);
            AppendLog($"已打开 {Path.GetFileName(dialog.FileName)}", ScriptLogLevel.Success);
            StatusText.Text = $"已打开 {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            AppendLog($"打开失败：{ex.Message}", ScriptLogLevel.Error);
            StatusText.Text = "打开设计失败";
        }
    }

    private void SaveProject_OnClick(object sender, RoutedEventArgs e) => SaveProject();

    private bool SaveProject()
    {
        SaveCurrentScript();
        FlushHistory();
        var path = _currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "FrostUI Designer (*.frostui.json)|*.frostui.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "保存 FrostUI 设计",
                FileName = $"{SafeFileName(_document.Name)}.frostui.json",
            };
            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            path = dialog.FileName;
        }

        try
        {
            DesignSerializer.Save(_document, path);
            _currentPath = path;
            _history.MarkSaved();
            ClearDirty();
            AutosaveService.Delete();
            AutosaveStatusText.Text = string.Empty;
            UpdateHistoryUi();
            AppendLog($"已保存 {Path.GetFileName(path)}", ScriptLogLevel.Success);
            StatusText.Text = $"已保存 {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"保存失败：{ex.Message}", ScriptLogLevel.Error);
            StatusText.Text = "保存失败";
            return false;
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    // ============================================================
    // Controls
    // ============================================================

    private void SelectControl(DesignControl control, bool additive = false)
    {
        if (_loadingDocument)
        {
            additive = false;
        }

        SaveCurrentScript();
        ClearInspectorBinding();
        if (additive)
        {
            if (_selection.Contains(control))
            {
                _selection.Remove(control);
                control.IsSelected = false;
                if (ReferenceEquals(_selected, control))
                {
                    _selected = _selection.LastOrDefault();
                }

                if (_selected is null)
                {
                    SelectWindow();
                    return;
                }

                ControlInspector.DataContext = _selected;
                RefreshParentChoices(_selected);
                RefreshTokenUi(_selected);
                RefreshNavigationChoices(_selected);
                LoadScriptFor(_selected, _selected.PrimaryEvent);
                UpdateStatusSelection();
                return;
            }

            _selection.Add(control);
            control.IsSelected = true;
            _selected = control;
        }
        else
        {
            foreach (var item in _selection)
            {
                item.IsSelected = false;
            }

            _selection.Clear();
            _selection.Add(control);
            _selected = control;
            control.IsSelected = true;
        }

        ControlInspector.DataContext = control;
        RefreshParentChoices(control);
        RefreshTokenUi(control);
        RefreshNavigationChoices(control);
        ControlInspector.Visibility = Visibility.Visible;
        WindowInspector.Visibility = Visibility.Collapsed;
        LoadScriptFor(control, control.PrimaryEvent);
        UpdateStatusSelection();
        RefreshLayerSelection();
    }

    private void RefreshTokenUi(DesignControl? control)
    {
        if (TokenComboBox is null || TokenList is null)
        {
            return;
        }

        _applyingToken = true;
        TokenComboBox.ItemsSource = _document.Tokens;
        TokenList.ItemsSource = _document.Tokens;
        if (control is not null)
        {
            TokenComboBox.SelectedValue = control.StyleToken;
        }

        _applyingToken = false;
    }

    private static void ApplyToken(DesignControl control, DesignToken token)
    {
        control.StyleToken = token.Name;
        control.FontFamily = token.FontFamily;
        control.FontSize = token.FontSize;
        control.IsBold = token.IsBold;
        control.CornerRadius = token.CornerRadius;
        control.Opacity = token.Opacity;
    }

    private void TokenComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingToken || _selected is null ||
            TokenComboBox.SelectedItem is not DesignToken token)
        {
            return;
        }

        var targets = _selection.Count > 0 ? _selection.ToList() : new List<DesignControl> { _selected };
        foreach (var control in targets)
        {
            ApplyToken(control, token);
        }

        CommitHistory($"应用令牌：{token.Name}");
    }

    private void SaveToken_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            StatusText.Text = "请先选择控件";
            return;
        }

        var name = TokenNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = string.IsNullOrWhiteSpace(_selected.StyleToken)
                ? $"令牌 {_document.Tokens.Count + 1}"
                : _selected.StyleToken;
        }

        var token = _document.Tokens.FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (token is null)
        {
            token = new DesignToken { Name = name };
            _document.Tokens.Add(token);
        }

        token.FontFamily = _selected.FontFamily;
        token.FontSize = _selected.FontSize;
        token.IsBold = _selected.IsBold;
        token.CornerRadius = _selected.CornerRadius;
        token.Opacity = _selected.Opacity;
        foreach (var control in _document.Controls.Where(
                     item => string.Equals(item.StyleToken, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            ApplyToken(control, token);
        }

        TokenNameBox.Text = string.Empty;
        RefreshTokenUi(_selected);
        CommitHistory($"保存设计令牌：{name}");
    }

    private void ApplyToken_OnClick(object sender, RoutedEventArgs e)
    {
        if (TokenList.SelectedItem is not DesignToken token || _selection.Count == 0)
        {
            StatusText.Text = "请选择令牌和控件";
            return;
        }

        foreach (var control in _selection)
        {
            ApplyToken(control, token);
        }

        CommitHistory($"应用令牌：{token.Name}");
    }

    private void RefreshNavigationChoices(DesignControl control)
    {
        if (NavigationTargetBox is null)
        {
            return;
        }

        NavigationTargetBox.ItemsSource = _document.Pages;
        NavigationTargetBox.SelectedValue = control.TargetPageId;
        if (NavigationTargetBox.SelectedItem is null && _document.Pages.Count > 0)
        {
            NavigationTargetBox.SelectedIndex = 0;
        }
    }

    private void RefreshParentChoices(DesignControl control)
    {
        if (ParentContainerBox is null)
        {
            return;
        }

        var names = new List<string> { string.Empty };
        names.AddRange(_document.Controls
            .Where(item => item.IsContainer && !ReferenceEquals(item, control))
            .Select(item => item.Name));
        ParentContainerBox.ItemsSource = names;
        ParentContainerBox.SelectedItem = names.Contains(control.ParentName)
            ? control.ParentName
            : string.Empty;
    }

    private void SelectWindow()
    {
        RefreshTokenUi(null);
        SaveCurrentScript();
        ClearInspectorBinding();
        foreach (var item in _selection)
        {
            item.IsSelected = false;
        }

        _selection.Clear();
        _selected = null;
        ControlInspector.Visibility = Visibility.Collapsed;
        WindowInspector.Visibility = Visibility.Visible;
        LoadScriptFor(null, "启动");
        UpdateStatusSelection();
        RefreshLayerSelection();
    }

    private void ClearInspectorBinding()
    {
        if (ControlInspector is null)
        {
            return;
        }

        if (ControlInspector.IsKeyboardFocusWithin)
        {
            Keyboard.ClearFocus();
        }

        ControlInspector.DataContext = null;
    }

    private void SelectAllControls()
    {
        if (_document.Controls.Count == 0)
        {
            return;
        }

        SaveCurrentScript();
        ClearInspectorBinding();
        foreach (var item in _selection)
        {
            item.IsSelected = false;
        }

        _selection.Clear();
        foreach (var control in _document.Controls)
        {
            control.IsSelected = true;
            _selection.Add(control);
        }

        _selected = _selection.LastOrDefault();
        if (_selected is not null)
        {
            ControlInspector.DataContext = _selected;
            ControlInspector.Visibility = Visibility.Visible;
            WindowInspector.Visibility = Visibility.Collapsed;
            LoadScriptFor(_selected, _selected.PrimaryEvent);
        }

        UpdateStatusSelection();
        RefreshLayerSelection();
        StatusText.Text = $"已选择 {_selection.Count} 个控件";
    }

    private void SelectWindow_OnClick(object sender, RoutedEventArgs e) => SelectWindow();

    private void BrowseImage_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true && _selected is not null)
        {
            _selected.ImagePath = dialog.FileName;
        }
    }

    private void BrowseIcon_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择应用图标",
            Filter = "图标 / 图片 (*.ico;*.png)|*.ico;*.png|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            _document.IconPath = dialog.FileName;
        }
    }

    private void AddControlAtCenter(ControlKind kind)
    {
        var definition = ControlCatalog.Get(kind);
        var cascade = (_document.Controls.Count % 6) * 10;
        var x = (_document.WindowWidth - definition.DefaultWidth) / 2 + cascade;
        var y = (_document.ContentHeight - definition.DefaultHeight) / 2 + cascade;
        AddControl(kind, x, y);
    }

    private DesignControl AddControl(ControlKind kind, double x, double y)
    {
        var definition = ControlCatalog.Get(kind);
        var control = ControlCatalog.Create(
            kind,
            ControlCatalog.NextName(kind, _document.Controls),
            Snap(x),
            Snap(y));
        control.X = Math.Clamp(control.X, 0, Math.Max(0, _document.WindowWidth - control.Width));
        control.Y = Math.Clamp(control.Y, 0, Math.Max(0, _document.ContentHeight - control.Height));
        _document.Controls.Add(control);
        SelectControl(control);
        CommitHistory("添加控件");
        AppendLog($"添加 {control.KindTitle} · {control.Name}", ScriptLogLevel.Info);
        StatusText.Text = $"已添加 {control.Name}";
        return control;
    }

    private void DuplicateControl_OnClick(object sender, RoutedEventArgs e) => DuplicateSelected();

    private void DuplicateSelected()
    {
        var sources = _selection.Count > 0
            ? _selection.ToList()
            : _selected is null ? [] : new List<DesignControl> { _selected };
        if (sources.Count == 0)
        {
            return;
        }

        var clones = new List<DesignControl>();
        foreach (var source in sources)
        {
            var clone = source.Clone(ControlCatalog.NextName(source.Kind, _document.Controls));
            clone.X = Math.Clamp(clone.X, 0, Math.Max(0, _document.WindowWidth - clone.Width));
            clone.Y = Math.Clamp(clone.Y, 0, Math.Max(0, _document.ContentHeight - clone.Height));
            _document.Controls.Add(clone);
            clones.Add(clone);
        }

        SelectControl(clones[0]);
        foreach (var clone in clones.Skip(1))
        {
            SelectControl(clone, additive: true);
        }

        CommitHistory("复制控件");
        AppendLog($"复制 {sources.Count} 个控件", ScriptLogLevel.Info);
    }

    private void DeleteControl_OnClick(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        var targets = _selection.Count > 0
            ? _selection.ToList()
            : _selected is null ? [] : new List<DesignControl> { _selected };
        if (targets.Count == 0)
        {
            return;
        }

        var names = string.Join("、", targets.Select(x => x.Name));
        foreach (var target in targets)
        {
            _document.Controls.Remove(target);
        }

        SelectWindow();
        CommitHistory("删除控件");
        AppendLog($"删除 {targets.Count} 个控件 · {names}", ScriptLogLevel.Warning);
        StatusText.Text = $"已删除 {targets.Count} 个控件";
    }

    // ============================================================
    // Toolbox drag & drop
    // ============================================================

    private void Toolbox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<ScrollBar>(source) is null)
        {
            var definition = FindDataContext<ControlDefinition>(source);
            _toolDragDefinition = definition;
            _toolDragStart = e.GetPosition(this);
        }
    }

    private void Toolbox_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_toolDragDefinition is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _toolDragStart.X) < 5 &&
            Math.Abs(current.Y - _toolDragStart.Y) < 5)
        {
            return;
        }

        var definition = _toolDragDefinition;
        _toolDragDefinition = null;
        DragDrop.DoDragDrop(
            ToolboxPanel,
            new DataObject(ControlDragFormat, definition.Kind.ToString()),
            DragDropEffects.Copy);
    }

    private void Toolbox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var definition = FindDataContext<ControlDefinition>(source);
        if (definition is not null)
        {
            AddControlAtCenter(definition.Kind);
            e.Handled = true;
        }
    }

    private void ControlItems_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ControlDragFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ControlItems_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ControlDragFormat) ||
            e.Data.GetData(ControlDragFormat) is not string text ||
            !Enum.TryParse<ControlKind>(text, out var kind))
        {
            return;
        }

        var definition = ControlCatalog.Get(kind);
        var point = e.GetPosition(ControlItems);
        AddControl(kind, point.X - definition.DefaultWidth / 2, point.Y - definition.DefaultHeight / 2);
        e.Handled = true;
    }

    // ============================================================
    // Design surface interaction
    // ============================================================

    private void ControlItems_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var control = FindDataContext<DesignControl>(source);
        if (control is null)
        {
            _boxSelecting = true;
            _boxAdditive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                           Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _boxStart = e.GetPosition(ControlItems);
            _boxStart = new Point(
                Math.Clamp(_boxStart.X, 0, _document.WindowWidth),
                Math.Clamp(_boxStart.Y, 0, _document.ContentHeight));
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            SelectionBox.Margin = new Thickness(_boxStart.X, _boxStart.Y, 0, 0);
            SelectionBox.Visibility = Visibility.Visible;
            ControlItems.CaptureMouse();
            e.Handled = true;
            return;
        }

        var additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                       Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        SelectControl(control, additive);

        if (!_selection.Contains(control) || control.IsLocked)
        {
            e.Handled = true;
            return;
        }

        if (FindAncestorWithTag(source, "Resize") is not null)
        {
            _resizeControl = control;
            _pointerStart = e.GetPosition(ControlItems);
            _resizeStartWidth = control.Width;
            _resizeStartHeight = control.Height;
            ControlItems.CaptureMouse();
            e.Handled = true;
            return;
        }

        _dragControl = control;
        _dragOrigins.Clear();
        foreach (var item in _selection.Where(x => !x.IsLocked))
        {
            _dragOrigins[item] = new Point(item.X, item.Y);
        }

        _pointerStart = e.GetPosition(ControlItems);
        _originStart = new Point(control.X, control.Y);
        _surfaceChanged = false;
        ControlItems.CaptureMouse();
        e.Handled = true;
    }

    private void ControlItems_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndSurfaceInteraction();
            return;
        }

        var position = e.GetPosition(ControlItems);
        if (_boxSelecting)
        {
            var left = Math.Clamp(Math.Min(_boxStart.X, position.X), 0, _document.WindowWidth);
            var top = Math.Clamp(Math.Min(_boxStart.Y, position.Y), 0, _document.ContentHeight);
            var right = Math.Clamp(Math.Max(_boxStart.X, position.X), 0, _document.WindowWidth);
            var bottom = Math.Clamp(Math.Max(_boxStart.Y, position.Y), 0, _document.ContentHeight);
            SelectionBox.Margin = new Thickness(left, top, 0, 0);
            SelectionBox.Width = right - left;
            SelectionBox.Height = bottom - top;
            e.Handled = true;
            return;
        }

        if (_dragControl is not null)
        {
            var rawX = _originStart.X + position.X - _pointerStart.X;
            var rawY = _originStart.Y + position.Y - _pointerStart.Y;
            var snapped = SnapWithGuides(_dragControl, rawX, rawY);
            var deltaX = snapped.X - _originStart.X;
            var deltaY = snapped.Y - _originStart.Y;
            foreach (var (item, origin) in _dragOrigins)
            {
                item.X = Math.Clamp(origin.X + deltaX, 0, Math.Max(0, _document.WindowWidth - item.Width));
                item.Y = Math.Clamp(origin.Y + deltaY, 0, Math.Max(0, _document.ContentHeight - item.Height));
            }

            _surfaceChanged = true;
            UpdateStatusSelection();
            e.Handled = true;
        }
        else if (_resizeControl is not null)
        {
            var width = _resizeStartWidth + position.X - _pointerStart.X;
            var height = _resizeStartHeight + position.Y - _pointerStart.Y;
            _resizeControl.Width = Math.Clamp(
                Snap(width),
                8,
                Math.Max(8, _document.WindowWidth - _resizeControl.X));
            _resizeControl.Height = Math.Clamp(
                Snap(height),
                8,
                Math.Max(8, _document.ContentHeight - _resizeControl.Y));
            _surfaceChanged = true;
            UpdateStatusSelection();
            e.Handled = true;
        }
    }

    private void ControlItems_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_boxSelecting)
        {
            FinishBoxSelection(e.GetPosition(ControlItems));
            e.Handled = true;
            return;
        }

        EndSurfaceInteraction();
        e.Handled = true;
    }

    private void FinishBoxSelection(Point position)
    {
        _boxSelecting = false;
        var left = Math.Min(_boxStart.X, position.X);
        var top = Math.Min(_boxStart.Y, position.Y);
        var right = Math.Max(_boxStart.X, position.X);
        var bottom = Math.Max(_boxStart.Y, position.Y);
        SelectionBox.Visibility = Visibility.Collapsed;

        if (right - left < 4 && bottom - top < 4)
        {
            if (!_boxAdditive)
            {
                SelectWindow();
            }

            ReleaseSurfaceCapture();
            return;
        }

        var matches = _document.Controls
            .Where(control => control.X < right &&
                              control.X + control.Width > left &&
                              control.Y < bottom &&
                              control.Y + control.Height > top)
            .ToList();
        SetSelection(matches, _boxAdditive);
        ReleaseSurfaceCapture();
    }

    private void SetSelection(IEnumerable<DesignControl> controls, bool additive)
    {
        SaveCurrentScript();
        ClearInspectorBinding();
        var list = controls.Distinct().ToList();
        if (!additive)
        {
            foreach (var item in _selection)
            {
                item.IsSelected = false;
            }

            _selection.Clear();
        }

        foreach (var control in list)
        {
            if (!_selection.Contains(control))
            {
                _selection.Add(control);
            }

            control.IsSelected = true;
        }

        _selected = list.LastOrDefault() ?? _selection.LastOrDefault();
        if (_selected is null)
        {
            SelectWindow();
            return;
        }

        ControlInspector.DataContext = _selected;
        ControlInspector.Visibility = Visibility.Visible;
        WindowInspector.Visibility = Visibility.Collapsed;
        LoadScriptFor(_selected, _selected.PrimaryEvent);
        UpdateStatusSelection();
        RefreshLayerSelection();
        StatusText.Text = $"已选择 {_selection.Count} 个控件";
    }

    private void ReleaseSurfaceCapture()
    {
        if (ControlItems.IsMouseCaptured)
        {
            ControlItems.ReleaseMouseCapture();
        }
    }

    private void EndSurfaceInteraction()
    {
        var changed = _surfaceChanged;
        var wasDragging = _dragControl is not null;
        _dragControl = null;
        _resizeControl = null;
        _dragOrigins.Clear();
        _boxSelecting = false;
        SelectionBox.Visibility = Visibility.Collapsed;
        ClearGuides();
        ReleaseSurfaceCapture();

        if (changed)
        {
            CommitHistory(wasDragging ? "移动控件" : "调整尺寸");
        }

        StatusText.Text = _selected is null ? "就绪" : $"已选择 {_selection.Count} 个控件";
    }

    private void ArtboardTitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SelectWindow();
        e.Handled = true;
    }

    private double Snap(double value) =>
        _snapToGrid ? Math.Round(value / _document.GridSize) * _document.GridSize : Math.Round(value);

    private (double X, double Y) SnapWithGuides(DesignControl primary, double x, double y)
    {
        ClearGuides();
        if (_snapToGrid)
        {
            x = Snap(x);
            y = Snap(y);
        }

        var others = _document.Controls
            .Where(control => !_selection.Contains(control))
            .ToList();
        var xTargets = others
            .SelectMany(control => new[] { control.X, control.X + control.Width / 2, control.X + control.Width })
            .Concat(_document.Guides
                .Where(guide => guide.Orientation == GuideOrientation.Vertical)
                .Select(guide => guide.Position))
            .Concat([0, _document.WindowWidth / 2, _document.WindowWidth])
            .ToArray();
        var yTargets = others
            .SelectMany(control => new[] { control.Y, control.Y + control.Height / 2, control.Y + control.Height })
            .Concat(_document.Guides
                .Where(guide => guide.Orientation == GuideOrientation.Horizontal)
                .Select(guide => guide.Position))
            .Concat([0, _document.ContentHeight / 2, _document.ContentHeight])
            .ToArray();

        var xSnap = FindAxisSnap(x, primary.Width, xTargets, out var xLine);
        var ySnap = FindAxisSnap(y, primary.Height, yTargets, out var yLine);
        if (xLine is not null)
        {
            AddGuideLine(xLine.Value, vertical: true);
        }

        if (yLine is not null)
        {
            AddGuideLine(yLine.Value, vertical: false);
        }

        return (xSnap, ySnap);
    }

    private static double FindAxisSnap(
        double position,
        double size,
        IReadOnlyList<double> targets,
        out double? guide)
    {
        guide = null;
        var bestDistance = 6.0;
        var bestDelta = 0.0;
        foreach (var target in targets)
        {
            foreach (var edge in new[] { position, position + size / 2, position + size })
            {
                var distance = Math.Abs(target - edge);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDelta = target - edge;
                    guide = target;
                }
            }
        }

        return position + bestDelta;
    }

    private void AddGuideLine(double position, bool vertical)
    {
        if (GuideLayer is null)
        {
            return;
        }

        var brush = TryFindResource("AccentBrush") as Brush ?? Brushes.DeepSkyBlue;
        var line = new Line
        {
            X1 = position,
            X2 = position,
            Y1 = 0,
            Y2 = vertical ? _document.ContentHeight : 0,
            Stroke = brush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Opacity = 0.9,
        };
        if (!vertical)
        {
            line.X1 = 0;
            line.X2 = _document.WindowWidth;
            line.Y1 = position;
            line.Y2 = position;
        }

        GuideLayer.Children.Add(line);
    }

    private void ClearGuides()
    {
        GuideLayer?.Children.Clear();
    }

    private void ApplyGridBrush()
    {
        if (GridOverlay is null || _document is null)
        {
            return;
        }

        var size = Math.Max(2, _document.GridSize);
        var brush = new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, size, size),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        var group = new DrawingGroup();
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 0.5);
        group.Children.Add(new GeometryDrawing(
            null, pen, new LineGeometry(new Point(0, 0), new Point(size, 0))));
        group.Children.Add(new GeometryDrawing(
            null, pen, new LineGeometry(new Point(0, 0), new Point(0, size))));
        brush.Drawing = group;
        GridOverlay.Fill = brush;
    }

    private void GridToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (GridOverlay is null)
        {
            return;
        }

        _showGrid = GridToggle.IsChecked == true;
        _snapToGrid = _showGrid;
        GridOverlay.Visibility = _showGrid ? Visibility.Visible : Visibility.Collapsed;
    }

    // ============================================================
    // Layers, alignment and history panels
    // ============================================================

    private void LayersButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshLayerView();
        LayersPopup.IsOpen = true;
    }

    private void RefreshLayerView()
    {
        if (_document is null || LayerList is null)
        {
            return;
        }

        _layerView ??= CollectionViewSource.GetDefaultView(_document.Controls);
        _layerView.Filter = FilterLayer;
        if (!ReferenceEquals(LayerList.ItemsSource, _layerView))
        {
            LayerList.ItemsSource = _layerView;
        }

        _layerView.Refresh();
        LayerCountText.Text = $"{_document.Controls.Count} 个";
        RefreshLayerSelection();
    }

    private bool FilterLayer(object item)
    {
        if (item is not DesignControl control)
        {
            return false;
        }

        var search = LayerSearchBox?.Text?.Trim();
        return string.IsNullOrWhiteSpace(search) ||
               control.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               control.KindTitle.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshLayerSelection()
    {
        if (LayerList is null)
        {
            return;
        }

        _syncingLayerSelection = true;
        try
        {
            LayerList.SelectedItem = _selected;
            if (_selected is not null)
            {
                LayerList.ScrollIntoView(_selected);
            }
        }
        finally
        {
            _syncingLayerSelection = false;
        }
    }

    private void LayerSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _layerView?.Refresh();
        RefreshLayerSelection();
    }

    private void LayerList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingLayerSelection || LayerList.SelectedItem is not DesignControl control)
        {
            return;
        }

        SelectControl(
            control,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private void LayerVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesignControl control })
        {
            control.IsVisible = !control.IsVisible;
            CommitHistory("显示/隐藏控件");
            e.Handled = true;
        }
    }

    private void LayerLock_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesignControl control })
        {
            control.IsLocked = !control.IsLocked;
            CommitHistory("锁定/解锁控件");
            e.Handled = true;
        }
    }

    private void LayerOrder_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || sender is not Button { Tag: string direction })
        {
            return;
        }

        var index = _document.Controls.IndexOf(_selected);
        if (index < 0)
        {
            return;
        }

        var next = direction == "up" ? index + 1 : index - 1;
        if (next < 0 || next >= _document.Controls.Count)
        {
            return;
        }

        _document.Controls.Move(index, next);
        CommitHistory(direction == "up" ? "上移图层" : "下移图层");
        RefreshLayerSelection();
        AppendLog($"{_selected.Name} 图层 {(direction == "up" ? "上移" : "下移")}", ScriptLogLevel.Info);
    }

    private void AlignButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selection.Count(x => !x.IsLocked) < 2)
        {
            StatusText.Text = "对齐至少需要选中 2 个未锁定控件";
            return;
        }

        AlignPopup.IsOpen = true;
    }

    private void AlignAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action })
        {
            return;
        }

        var items = _selection.Where(x => !x.IsLocked).ToList();
        if (items.Count < 2)
        {
            return;
        }
        var left = items.Min(x => x.X);
        var top = items.Min(x => x.Y);
        var right = items.Max(x => x.X + x.Width);
        var bottom = items.Max(x => x.Y + x.Height);
        var centerX = (left + right) / 2;
        var centerY = (top + bottom) / 2;

        switch (action)
        {
            case "left":
                foreach (var item in items) item.X = left;
                break;
            case "right":
                foreach (var item in items) item.X = right - item.Width;
                break;
            case "centerX":
                foreach (var item in items) item.X = centerX - item.Width / 2;
                break;
            case "top":
                foreach (var item in items) item.Y = top;
                break;
            case "bottom":
                foreach (var item in items) item.Y = bottom - item.Height;
                break;
            case "centerY":
                foreach (var item in items) item.Y = centerY - item.Height / 2;
                break;
            case "distributeX":
                Distribute(items, horizontal: true);
                break;
            case "distributeY":
                Distribute(items, horizontal: false);
                break;
            default:
                return;
        }

        CommitHistory("对齐控件");
        AlignPopup.IsOpen = false;
        UpdateStatusSelection();
        StatusText.Text = $"已对齐 {items.Count} 个控件";
    }

    private static void Distribute(List<DesignControl> items, bool horizontal)
    {
        if (items.Count < 3)
        {
            return;
        }

        var ordered = horizontal
            ? items.OrderBy(x => x.X).ToList()
            : items.OrderBy(x => x.Y).ToList();
        var start = horizontal ? ordered[0].X : ordered[0].Y;
        var end = horizontal
            ? ordered[^1].X + ordered[^1].Width
            : ordered[^1].Y + ordered[^1].Height;
        var totalSize = ordered.Sum(x => horizontal ? x.Width : x.Height);
        var gap = (end - start - totalSize) / (ordered.Count - 1);
        var cursor = start;
        foreach (var item in ordered)
        {
            if (horizontal)
            {
                item.X = cursor;
                cursor += item.Width + gap;
            }
            else
            {
                item.Y = cursor;
                cursor += item.Height + gap;
            }
        }
    }

    // ============================================================
    // Inspector
    // ============================================================

    private void InspectorNameBox_OnGotFocus(object sender, RoutedEventArgs e) =>
        _nameBeforeEdit = _selected?.Name ?? string.Empty;

    private void InspectorNameBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }

        var newName = InspectorNameBox.Text.Trim();
        if (newName.Length == 0 ||
            _document.Controls.Any(x => !ReferenceEquals(x, _selected) &&
                                       string.Equals(x.Name, newName, StringComparison.Ordinal)))
        {
            _selected.Name = _nameBeforeEdit;
            InspectorNameBox.Text = _nameBeforeEdit;
            AppendLog("控件名称不能为空，也不能重复", ScriptLogLevel.Error);
            return;
        }

        _selected.Name = newName;
        ScriptTargetText.Text = $"{newName} · {_scriptEvent}";
        UpdateStatusSelection();
    }

    private void WindowMaterial_OnChecked(object sender, RoutedEventArgs e)
    {
        if (_document is null || _loadingDocument || _syncingWindowMaterial ||
            sender is not RadioButton { Tag: string tag } ||
            !Enum.TryParse<WindowBackdrop>(tag, ignoreCase: true, out var backdrop))
        {
            return;
        }

        _document.Backdrop = backdrop;
        AppendLog($"窗口材质切换为 {backdrop}", ScriptLogLevel.Info);
    }

    private void UpdateWindowMaterialChecks()
    {
        if (_document is null || WindowMaterialAcrylic is null)
        {
            return;
        }

        _syncingWindowMaterial = true;
        WindowMaterialAcrylic.IsChecked = _document.Backdrop == WindowBackdrop.Acrylic;
        WindowMaterialMica.IsChecked = _document.Backdrop == WindowBackdrop.Mica;
        WindowMaterialMicaAlt.IsChecked = _document.Backdrop == WindowBackdrop.MicaAlt;
        WindowMaterialBlur.IsChecked = _document.Backdrop == WindowBackdrop.Blur;
        WindowMaterialNone.IsChecked = _document.Backdrop == WindowBackdrop.None;
        _syncingWindowMaterial = false;
    }

    private void UpdateStatusSelection()
    {
        if (_document is null)
        {
            return;
        }

        if (_selected is null)
        {
            StatusSelectionText.Text = $"窗口 · {_document.WindowSizeText}";
        }
        else
        {
            StatusSelectionText.Text = _selection.Count > 1
                ? $"已选 {_selection.Count} 个 · 主选 {_selected.Name} · X {_selected.X:0} Y {_selected.Y:0}"
                : $"{_selected.KindTitle} {_selected.Name} · X {_selected.X:0} Y {_selected.Y:0} · W {_selected.Width:0} H {_selected.Height:0}";
        }
    }

    // ============================================================
    // Scripts
    // ============================================================

    private void LoadScriptFor(DesignControl? control, string eventName)
    {
        if (_document is null)
        {
            return;
        }

        _scriptControl = control;
        _scriptEvent = eventName;

        var code = control is null
            ? GetWindowScript(eventName)
            : control.GetScript(eventName);

        if (string.IsNullOrWhiteSpace(code))
        {
            code = control is null
                ? "// 窗口 · 启动\n输出(\"窗口启动\")\n"
                : ControlCatalog.DefaultScript(ControlCatalog.Get(control.Kind), control.Name, eventName);
        }

        _loadingScript = true;
        ScriptEditor.Text = code;
        _loadingScript = false;
        ScriptTargetText.Text = control is null ? $"窗口 · {eventName}" : $"{control.Name} · {eventName}";
        ScriptEventText.Text = eventName;
        UpdateScriptLineInfo();
        UpdateScriptDiagnostics();
        UpdateSignatureHint();
        UpdateBracketHint();
    }

    private string GetWindowScript(string eventName) =>
        _document.WindowScripts.TryGetValue(eventName, out var code) ? code : string.Empty;

    private void SaveCurrentScript()
    {
        if (_loadingDocument || _loadingScript || _document is null)
        {
            return;
        }

        var code = ScriptEditor?.Text ?? string.Empty;
        if (_scriptControl is not null)
        {
            var old = _scriptControl.GetScript(_scriptEvent);
            if (!string.Equals(old, code, StringComparison.Ordinal))
            {
                _scriptControl.SetScript(_scriptEvent, code);
                MarkDirty("脚本修改");
            }
        }
        else if (!string.IsNullOrEmpty(_scriptEvent))
        {
            var old = GetWindowScript(_scriptEvent);
            if (!string.Equals(old, code, StringComparison.Ordinal))
            {
                _document.WindowScripts[_scriptEvent] = code;
                MarkDirty("脚本修改");
            }
        }
    }

    private void ScriptEditor_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateScriptLineInfo();
        UpdateSignatureHint();
        UpdateBracketHint();
        if (_loadingScript || _document is null)
        {
            return;
        }

        SaveCurrentScript();
        ScheduleCompletion();
        ScheduleScriptDiagnostics();
    }

    private void ScheduleScriptDiagnostics()
    {
        if (_loadingScript || ScriptDiagnosticsText is null)
        {
            return;
        }

        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Start();
    }

    private void UpdateScriptDiagnostics()
    {
        if (ScriptDiagnosticsText is null || ScriptEditor is null)
        {
            return;
        }

        var diagnostics = ScriptAnalyzer.Analyze(_document, ScriptEditor.Text);
        _scriptDiagnostics.Clear();
        foreach (var diagnostic in diagnostics)
        {
            _scriptDiagnostics.Add(diagnostic);
        }

        var errors = diagnostics.Count(x => x.Severity == ScriptDiagnosticSeverity.Error);
        var warnings = diagnostics.Count - errors;
        if (errors > 0)
        {
            ScriptDiagnosticsText.Text = errors == 1
                ? diagnostics[0].Message
                : $"{errors} 个错误";
            ScriptDiagnosticsText.Foreground = (Brush)FindResource("DangerBrush");
        }
        else if (warnings > 0)
        {
            ScriptDiagnosticsText.Text = $"{warnings} 个提示";
            ScriptDiagnosticsText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else
        {
            ScriptDiagnosticsText.Text = "✓ 语法正确";
            ScriptDiagnosticsText.Foreground = (Brush)FindResource("SuccessBrush");
        }

        ScriptDiagnosticsText.ToolTip = diagnostics.Count == 0
            ? "实时语法检查；Ctrl+Enter 可手动校验"
            : string.Join(Environment.NewLine, diagnostics.Select(x => x.Display));
        RedrawScriptOverlay();
    }

    private void ScriptDiagnostics_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_scriptDiagnostics.Count == 0)
        {
            return;
        }

        ScriptDiagnosticsPopup.IsOpen = true;
        e.Handled = true;
    }

    private void ScriptDiagnosticsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ScriptDiagnosticsList.SelectedItem is not ScriptDiagnostic diagnostic)
        {
            return;
        }

        var text = ScriptEditor.Text ?? string.Empty;
        var lines = text.Split('\n');
        var index = 0;
        for (var i = 0; i < Math.Min(lines.Length, diagnostic.Line - 1); i++)
        {
            index += lines[i].Length + 1;
        }

        index = Math.Min(index + Math.Max(0, diagnostic.Column - 1), text.Length);
        ScriptEditor.CaretIndex = index;
        ScriptEditor.Focus();
        ScriptDiagnosticsPopup.IsOpen = false;
        e.Handled = true;
    }

    private void ScheduleCompletion()
    {
        if (_loadingScript || _insertingCompletion || !ScriptEditor.IsKeyboardFocused)
        {
            return;
        }

        _completionTimer.Stop();
        _completionTimer.Start();
    }

    private void ShowCompletions(bool force)
    {
        if (_document is null || _loadingScript || _insertingCompletion || !ScriptEditor.IsKeyboardFocused)
        {
            return;
        }

        var result = ScriptCompletionService.GetCompletions(
            _document,
            ScriptEditor.Text,
            ScriptEditor.CaretIndex);
        _completionResult = result;

        if (result.IsInStringOrComment || result.Items.Count == 0)
        {
            CloseCompletions();
            return;
        }

        if (!force && !CompletionPopup.IsOpen && result.Prefix.Length == 0 && result.BaseName is null)
        {
            return;
        }

        _completionItems.Clear();
        foreach (var item in result.Items)
        {
            _completionItems.Add(item);
        }

        CompletionList.SelectedIndex = 0;
        CompletionList.ScrollIntoView(CompletionList.SelectedItem);

        var rectangle = ScriptEditor.GetRectFromCharacterIndex(ScriptEditor.CaretIndex, false);
        if (rectangle.IsEmpty)
        {
            rectangle = new Rect(0, Math.Max(0, ScriptEditor.ActualHeight - 8), 0, 0);
        }

        CompletionPopup.PlacementTarget = ScriptEditor;
        CompletionPopup.Placement = PlacementMode.Relative;
        CompletionPopup.HorizontalOffset = Math.Max(0, rectangle.Left);
        CompletionPopup.VerticalOffset = Math.Max(0, rectangle.Bottom + 4);
        CompletionPopup.IsOpen = true;
    }

    private void CloseCompletions()
    {
        if (CompletionPopup is not null)
        {
            CompletionPopup.IsOpen = false;
        }

        _completionItems.Clear();
        _completionResult = CompletionResult.Empty(ScriptEditor?.CaretIndex ?? 0);
    }

    private void InsertCompletion()
    {
        if (CompletionList.SelectedItem is not CompletionItem item)
        {
            return;
        }

        var start = _completionResult.ReplaceStart;
        var length = _completionResult.ReplaceLength;
        var text = ScriptEditor.Text;
        if (start < 0 || start + length > text.Length)
        {
            CloseCompletions();
            return;
        }

        _insertingCompletion = true;
        CompletionPopup.IsOpen = false;
        var inserted = item.InsertText;
        ScriptEditor.Text = text[..start] + inserted + text[(start + length)..];
        ScriptEditor.CaretIndex = start + inserted.Length - item.CaretOffsetFromEnd;
        ScriptEditor.SelectionLength = 0;
        _insertingCompletion = false;
        ScriptEditor.Focus();
    }

    private void CompletionList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        InsertCompletion();
        e.Handled = true;
    }

    private void CompletionList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Tab)
        {
            InsertCompletion();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseCompletions();
            ScriptEditor.Focus();
            e.Handled = true;
        }
    }

    private void ScriptEditor_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        CloseCompletions();

    private void ScriptEditor_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateSignatureHint();
        UpdateBracketHint();
    }

    private void ScriptEditor_OnMouseMove(object sender, MouseEventArgs e)
    {
        _hoverPoint = e.GetPosition(ScriptEditor);
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void ScriptEditor_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTimer.Stop();
        ScriptDocPopup.IsOpen = false;
    }

    private static readonly Dictionary<string, string> ScriptDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["输出"] = "输出(值)\n把值写入设计器底部运行输出。",
        ["提示"] = "提示(文本)\n在预览窗口显示 Toast，同时写入运行输出。",
        ["长度"] = "长度(值)\n返回文本、列表或 JSON 对象的长度。",
        ["类型"] = "类型(值)\n返回值的类型名，如“整数”“文本”“列表”。",
        ["文本"] = "文本(值)\n转换为文本。",
        ["整数"] = "整数(值)\n转换为整数。",
        ["小数"] = "小数(值)\n转换为小数。",
        ["逻辑"] = "逻辑(值)\n转换为逻辑值。",
        ["随机"] = "随机(最小值, 最大值)\n返回闭区间随机整数。",
        ["等待"] = "等待(毫秒)\n异步等待，最多 30 秒。",
        ["JSON.解析"] = "JSON.解析(文本)\n把 JSON 文本解析成对象/列表。",
        ["JSON.取值"] = "JSON.取值(JSON, 路径)\n支持 user.name、items[0].id 这样的路径。",
        ["JSON.文本"] = "JSON.文本(JSON, 是否缩进)\n把 JSON 序列化为文本。",
        ["HTTP.获取"] = "HTTP.获取(URL)\nGET 请求并返回文本。",
        ["HTTP.获取JSON"] = "HTTP.获取JSON(URL)\nGET 请求并解析为 JSON。",
        ["HTTP.提交JSON"] = "HTTP.提交JSON(URL, JSON文本)\nPOST JSON 请求。",
    };

    private void ShowScriptDocumentation(Point point)
    {
        var text = ScriptEditor.Text;
        var index = ScriptEditor.GetCharacterIndexFromPoint(point, snapToText: true);
        if (index < 0 || index >= text.Length)
        {
            ScriptDocPopup.IsOpen = false;
            return;
        }

        var start = index;
        while (start > 0 &&
               (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '.'))
        {
            start--;
        }

        var end = index;
        while (end < text.Length &&
               (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '.'))
        {
            end++;
        }

        var name = text[start..end].Trim('.');
        if (string.IsNullOrWhiteSpace(name))
        {
            ScriptDocPopup.IsOpen = false;
            return;
        }

        string? documentation = null;
        if (!ScriptDocs.TryGetValue(name, out documentation))
        {
            var signature = FindSignature(name, text);
            documentation = signature is null
                ? null
                : $"{signature}\n用户函数";
        }

        if (documentation is null)
        {
            ScriptDocPopup.IsOpen = false;
            return;
        }

        ScriptDocText.Text = documentation;
        ScriptDocPopup.IsOpen = true;
    }

    private void UpdateBracketHint()
    {
        if (ScriptBracketText is null)
        {
            return;
        }

        var text = ScriptEditor.Text;
        var caret = ScriptEditor.CaretIndex;
        var index = caret < text.Length && "()[]{}".Contains(text[caret])
            ? caret
            : caret > 0 && "()[]{}".Contains(text[caret - 1])
                ? caret - 1
                : -1;
        if (index < 0)
        {
            ScriptBracketText.Text = string.Empty;
            return;
        }

        var bracket = text[index];
        var match = bracket switch
        {
            '(' => FindForward(text, index, '(', ')'),
            '[' => FindForward(text, index, '[', ']'),
            '{' => FindForward(text, index, '{', '}'),
            ')' => FindBackward(text, index, '(', ')'),
            ']' => FindBackward(text, index, '[', ']'),
            '}' => FindBackward(text, index, '{', '}'),
            _ => -1,
        };
        if (match < 0)
        {
            ScriptBracketText.Text = "括号不匹配";
            RedrawScriptOverlay();
            return;
        }

        ScriptBracketText.Text = $"{bracket} ↔ {text[match]} · 第 {GetLineNumber(match)} 行";
        RedrawScriptOverlay();
    }

    private void ToggleBreakpointAtCaret()
    {
        var text = ScriptEditor.Text ?? string.Empty;
        var caret = Math.Clamp(ScriptEditor.CaretIndex, 0, text.Length);
        var line = 1;
        for (var i = 0; i < caret; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        if (!_breakpoints.Add(line))
        {
            _breakpoints.Remove(line);
        }

        BreakpointText.Text = _breakpoints.Count == 0 ? string.Empty : $"断点 {_breakpoints.Count}";
        RedrawScriptOverlay();
        StatusText.Text = _breakpoints.Contains(line)
            ? $"已设置断点：第 {line} 行"
            : $"已移除断点：第 {line} 行";
    }

    private void RedrawScriptOverlay()
    {
        if (ScriptOverlay is null || ScriptEditor is null)
        {
            return;
        }

        ScriptOverlay.Children.Clear();
        var text = ScriptEditor.Text ?? string.Empty;
        foreach (var breakpointLine in _breakpoints)
        {
            var start = GetLineStart(text, Math.Clamp(breakpointLine - 1, 0, text.Count(c => c == '\n')));
            var rect = GetCharacterRect(start);
            if (rect.IsEmpty)
            {
                continue;
            }

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = (Brush)FindResource("DangerBrush"),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(dot, 2);
            Canvas.SetTop(dot, rect.Top + 3);
            ScriptOverlay.Children.Add(dot);
        }
        foreach (var diagnostic in _scriptDiagnostics)
        {
            var line = Math.Clamp(diagnostic.Line - 1, 0, Math.Max(0, text.Count(c => c == '\n')));
            var lineStart = GetLineStart(text, line);
            var lineEnd = GetLineEnd(text, lineStart);
            var startRect = GetCharacterRect(lineStart);
            var endRect = GetCharacterRect(Math.Max(lineStart, lineEnd - 1));
            if (startRect.IsEmpty)
            {
                continue;
            }

            var left = startRect.Left;
            var right = endRect.IsEmpty ? startRect.Right : Math.Max(endRect.Right, left + 24);
            var y = startRect.Bottom + 1;
            var brush = (Brush)FindResource(
                diagnostic.Severity == ScriptDiagnosticSeverity.Error ? "DangerBrush" : "WarningBrush");
            var figure = new PathFigure { StartPoint = new Point(left, y) };
            var points = new PolyLineSegment();
            var up = true;
            for (var x = left; x <= right; x += 3)
            {
                points.Points.Add(new Point(x, up ? y - 1.5 : y + 1.5));
                up = !up;
            }

            figure.Segments.Add(points);
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            ScriptOverlay.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = brush,
                StrokeThickness = 1.2,
            });
        }

        var bracketMatch = GetBracketMatch();
        if (bracketMatch is not null)
        {
            var (first, second) = bracketMatch.Value;
            foreach (var index in new[] { first, second })
            {
                var rect = GetCharacterRect(index);
                if (rect.IsEmpty)
                {
                    continue;
                }

                var rectangle = new Rectangle
                {
                    Width = Math.Max(3, rect.Width),
                    Height = Math.Max(10, rect.Height),
                    Stroke = (Brush)FindResource("AccentBrush"),
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(Color.FromArgb(28, 130, 170, 255)),
                };
                Canvas.SetLeft(rectangle, rect.Left);
                Canvas.SetTop(rectangle, rect.Top);
                ScriptOverlay.Children.Add(rectangle);
            }
        }
    }

    private Rect GetCharacterRect(int index)
    {
        if (index < 0 || index >= ScriptEditor.Text.Length)
        {
            return Rect.Empty;
        }

        try
        {
            return ScriptEditor.GetRectFromCharacterIndex(index, trailingEdge: false);
        }
        catch
        {
            return Rect.Empty;
        }
    }

    private static int GetLineStart(string text, int line)
    {
        var current = 0;
        for (var i = 0; i < line; i++)
        {
            var newline = text.IndexOf('\n', current);
            if (newline < 0)
            {
                return text.Length;
            }

            current = newline + 1;
        }

        return current;
    }

    private static int GetLineEnd(string text, int lineStart)
    {
        var newline = text.IndexOf('\n', lineStart);
        return newline < 0 ? text.Length : newline;
    }

    private (int First, int Second)? GetBracketMatch()
    {
        var text = ScriptEditor?.Text ?? string.Empty;
        var caret = ScriptEditor?.CaretIndex ?? 0;
        var index = caret < text.Length && "()[]{}".Contains(text[caret])
            ? caret
            : caret > 0 && "()[]{}".Contains(text[caret - 1])
                ? caret - 1
                : -1;
        if (index < 0)
        {
            return null;
        }

        var match = text[index] switch
        {
            '(' => FindForward(text, index, '(', ')'),
            '[' => FindForward(text, index, '[', ']'),
            '{' => FindForward(text, index, '{', '}'),
            ')' => FindBackward(text, index, '(', ')'),
            ']' => FindBackward(text, index, '[', ']'),
            '}' => FindBackward(text, index, '{', '}'),
            _ => -1,
        };
        return match < 0 ? null : (index, match);
    }

    private static int FindForward(string text, int index, char open, char close)
    {
        var depth = 0;
        for (var i = index; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindBackward(string text, int index, char open, char close)
    {
        var depth = 0;
        for (var i = index; i >= 0; i--)
        {
            if (text[i] == close)
            {
                depth++;
            }
            else if (text[i] == open && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private void FormatScript_OnClick(object sender, RoutedEventArgs e) => FormatScript();

    private void FormatScript()
    {
        var text = ScriptEditor.Text.Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var indent = 0;
        var formatted = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                formatted.Add(string.Empty);
                continue;
            }

            var lineIndent = indent;
            if (trimmed.StartsWith('}') ||
                trimmed.StartsWith("否则", StringComparison.Ordinal) ||
                trimmed.StartsWith("else", StringComparison.OrdinalIgnoreCase))
            {
                lineIndent = Math.Max(0, lineIndent - 1);
            }

            formatted.Add(new string(' ', lineIndent * 4) + trimmed);
            var opens = trimmed.Count(c => c == '{');
            var closes = trimmed.Count(c => c == '}');
            indent = Math.Max(0, indent + opens - closes);
        }

        var caret = ScriptEditor.CaretIndex;
        ScriptEditor.Text = string.Join(Environment.NewLine, formatted);
        ScriptEditor.CaretIndex = Math.Min(caret, ScriptEditor.Text.Length);
        StatusText.Text = "脚本已格式化";
    }

    private void UpdateSignatureHint()
    {
        if (ScriptEditor is null || SignatureHint is null)
        {
            return;
        }

        var context = FindCallContext(ScriptEditor.Text, ScriptEditor.CaretIndex);
        if (context is null)
        {
            SignatureHint.Visibility = Visibility.Collapsed;
            return;
        }

        var (name, activeArgument) = context.Value;
        var signature = FindSignature(name, ScriptEditor.Text);
        if (signature is null)
        {
            SignatureHint.Visibility = Visibility.Collapsed;
            return;
        }

        var parameterCount = CountParameters(signature);
        ScriptSignatureText.Text = parameterCount > 1
            ? $"{signature}   ·   参数 {Math.Clamp(activeArgument, 1, parameterCount)}/{parameterCount}"
            : signature;
        SignatureHint.Visibility = Visibility.Visible;
    }

    private static (string Name, int ActiveArgument)? FindCallContext(string text, int caret)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        caret = Math.Clamp(caret, 0, text.Length);
        var depth = 0;
        var open = -1;
        for (var i = caret - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c == '"' || c == '\'')
            {
                var quote = c;
                i--;
                while (i >= 0 && text[i] != quote)
                {
                    i--;
                }

                continue;
            }

            if (c == ')')
            {
                depth++;
            }
            else if (c == '(')
            {
                if (depth == 0)
                {
                    open = i;
                    break;
                }

                depth--;
            }
        }

        if (open < 0)
        {
            return null;
        }

        var nameEnd = open;
        var nameStart = nameEnd;
        while (nameStart > 0 &&
               (char.IsLetterOrDigit(text[nameStart - 1]) ||
                text[nameStart - 1] is '_' or '.'))
        {
            nameStart--;
        }

        if (nameStart == nameEnd)
        {
            return null;
        }

        var name = text[nameStart..nameEnd];
        var argument = 1;
        depth = 0;
        for (var i = open + 1; i < caret; i++)
        {
            var c = text[i];
            if (c == '"' || c == '\'')
            {
                var quote = c;
                i++;
                while (i < caret && text[i] != quote)
                {
                    i++;
                }

                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                argument++;
            }
        }

        return (name, argument);
    }

    private static string? FindSignature(string name, string text)
    {
        if (BuiltinSignatures.TryGetValue(name, out var builtin))
        {
            return builtin;
        }

        var match = Regex.Match(
            text,
            $@"函数\s+{Regex.Escape(name)}\s*\(([^)]*)\)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var parameters = match.Groups[1].Value.Trim();
        return $"函数 {name}({parameters})";
    }

    private static int CountParameters(string signature)
    {
        var open = signature.IndexOf('(');
        var close = signature.LastIndexOf(')');
        if (open < 0 || close <= open + 1)
        {
            return 0;
        }

        return signature[(open + 1)..close].Split(',').Length;
    }

    private void UpdateScriptLineInfo()
    {
        if (ScriptEditor is null || ScriptLineInfo is null)
        {
            return;
        }

        var text = ScriptEditor.Text ?? string.Empty;
        var lines = text.Length == 0 ? 1 : text.Count(c => c == '\n') + 1;
        ScriptLineInfo.Text = $"{lines} 行 · {text.Length} 字符";
    }

    private void FindReplace_OnClick(object sender, RoutedEventArgs e) => ShowFindReplace(false);

    private void ShowFindReplace(bool focusReplace)
    {
        var selected = ScriptEditor.SelectedText;
        if (!string.IsNullOrEmpty(selected) && !selected.Contains('\n'))
        {
            FindBox.Text = selected;
        }

        FindReplacePopup.IsOpen = true;
        if (focusReplace)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }
        else
        {
            FindBox.Focus();
            FindBox.SelectAll();
        }
    }

    private void FindBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindReplace_OnClick(sender, e);
            e.Handled = true;
        }
    }

    private void FindAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string action })
        {
            return;
        }

        switch (action)
        {
            case "next":
                FindNext();
                break;
            case "replace":
                ReplaceCurrent();
                break;
            case "replaceAll":
                ReplaceAll();
                break;
        }
    }

    private void CloseFindReplace_OnClick(object sender, RoutedEventArgs e)
    {
        FindReplacePopup.IsOpen = false;
        ScriptEditor.Focus();
    }

    private void FindNext()
    {
        var needle = FindBox.Text;
        if (string.IsNullOrEmpty(needle))
        {
            FindStatusText.Text = "请输入查找内容";
            return;
        }

        var text = ScriptEditor.Text;
        var start = Math.Min(text.Length, ScriptEditor.SelectionStart + ScriptEditor.SelectionLength);
        var index = text.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && start > 0)
        {
            index = text.IndexOf(needle, 0, StringComparison.OrdinalIgnoreCase);
        }

        if (index < 0)
        {
            FindStatusText.Text = "未找到";
            return;
        }

        ScriptEditor.Select(index, needle.Length);
        FindStatusText.Text = $"已定位第 {GetLineNumber(index)} 行";
    }

    private void ReplaceCurrent()
    {
        var needle = FindBox.Text;
        if (string.IsNullOrEmpty(needle))
        {
            FindStatusText.Text = "请输入查找内容";
            return;
        }

        if (ScriptEditor.SelectionLength == needle.Length &&
            string.Equals(ScriptEditor.SelectedText, needle, StringComparison.OrdinalIgnoreCase))
        {
            ScriptEditor.SelectedText = ReplaceBox.Text;
            FindStatusText.Text = "已替换 1 处";
        }

        FindNext();
    }

    private void ReplaceAll()
    {
        var needle = FindBox.Text;
        if (string.IsNullOrEmpty(needle))
        {
            FindStatusText.Text = "请输入查找内容";
            return;
        }

        var text = ScriptEditor.Text;
        var count = 0;
        var index = 0;
        var builder = new System.Text.StringBuilder();
        while (index < text.Length)
        {
            var found = text.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, found - index);
            builder.Append(ReplaceBox.Text);
            index = found + needle.Length;
            count++;
        }

        if (count == 0)
        {
            FindStatusText.Text = "未找到";
            return;
        }

        ScriptEditor.Text = builder.ToString();
        FindStatusText.Text = $"已替换 {count} 处";
    }

    private int GetLineNumber(int index)
    {
        var line = 1;
        for (var i = 0; i < Math.Min(index, ScriptEditor.Text.Length); i++)
        {
            if (ScriptEditor.Text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private void ToggleComment()
    {
        var text = ScriptEditor.Text;
        var start = ScriptEditor.SelectionStart;
        var length = ScriptEditor.SelectionLength;
        if (length == 0)
        {
            var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            var lineEnd = text.IndexOf('\n', start);
            lineEnd = lineEnd < 0 ? text.Length : lineEnd;
            start = lineStart;
            length = lineEnd - lineStart;
        }

        var selected = text.Substring(start, length);
        var lines = selected.Split('\n');
        var allCommented = lines.All(line =>
            line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (allCommented)
            {
                var comment = line.IndexOf("//", StringComparison.Ordinal);
                lines[i] = comment >= 0 ? line.Remove(comment, 2).TrimStart() : line;
            }
            else
            {
                lines[i] = "// " + line;
            }
        }

        var replacement = string.Join("\n", lines);
        ScriptEditor.Text = text[..start] + replacement + text[(start + length)..];
        ScriptEditor.Select(start, replacement.Length);
    }

    private void ScriptReference_OnClick(object sender, RoutedEventArgs e)
    {
        if (_referenceWindow is not null)
        {
            _referenceWindow.Activate();
            return;
        }

        _referenceWindow = new ScriptReferenceWindow { Owner = this };
        _referenceWindow.InsertRequested += signature =>
        {
            var start = ScriptEditor.SelectionStart;
            ScriptEditor.SelectedText = signature;
            ScriptEditor.CaretIndex = start + signature.Length;
            ScriptEditor.Focus();
        };
        _referenceWindow.Closed += (_, _) => _referenceWindow = null;
        _referenceWindow.Show();
    }

    private void Timeline_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            StatusText.Text = "请先选择控件";
            return;
        }

        if (_timelineWindow is not null)
        {
            _timelineWindow.Activate();
            return;
        }

        _timelineWindow = new TimelineWindow(_selected) { Owner = this };
        _timelineWindow.Changed += () => CommitHistory("编辑动画时间线");
        _timelineWindow.PreviewRequested += () => OpenPreview();
        _timelineWindow.Closed += (_, _) => _timelineWindow = null;
        _timelineWindow.Show();
    }

    private void DebugScript_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentScript();
        var clone = DesignSerializer.Clone(_document);
        var dialog = new DebugWindow(
            clone,
            ScriptEditor.Text,
            _scriptControl?.Name,
            _scriptEvent,
            _breakpoints)
        {
            Owner = this,
        };
        dialog.ShowDialog();
    }

    private void ValidateScript_OnClick(object sender, RoutedEventArgs e)
    {
        var result = ScriptRuntime.Validate(ScriptEditor.Text);
        UpdateScriptDiagnostics();
        if (result.Success)
        {
            AppendLog("脚本语法正确", ScriptLogLevel.Success);
            StatusText.Text = "脚本语法正确";
        }
        else
        {
            AppendLog($"脚本语法错误：{result.Message}", ScriptLogLevel.Error);
            StatusText.Text = "脚本存在语法错误";
        }
    }

    private void ScriptEditor_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9)
        {
            ToggleBreakpointAtCaret();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.F)
        {
            ShowFindReplace(false);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.H)
        {
            ShowFindReplace(true);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
            e.Key == Key.F)
        {
            FormatScript();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            e.Key is Key.Oem2 or Key.OemQuestion or Key.Divide)
        {
            ToggleComment();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ShowCompletions(true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CloseCompletions();
            ValidateScript_OnClick(sender, e);
            e.Handled = true;
            return;
        }

        if (CompletionPopup.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveCompletionSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    MoveCompletionSelection(-1);
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    if (CompletionList.SelectedItem is not null)
                    {
                        InsertCompletion();
                        e.Handled = true;
                        return;
                    }

                    CloseCompletions();
                    break;
                case Key.Escape:
                    CloseCompletions();
                    e.Handled = true;
                    return;
                case Key.Left:
                case Key.Right:
                case Key.Home:
                case Key.End:
                    CloseCompletions();
                    break;
            }
        }

        if (e.Key == Key.Tab)
        {
            var start = ScriptEditor.SelectionStart;
            ScriptEditor.Text = ScriptEditor.Text.Insert(start, "    ");
            ScriptEditor.SelectionStart = start + 4;
            e.Handled = true;
        }
    }

    private void MoveCompletionSelection(int delta)
    {
        var count = CompletionList.Items.Count;
        if (count == 0)
        {
            return;
        }

        CompletionList.SelectedIndex = Math.Clamp(
            CompletionList.SelectedIndex + delta,
            0,
            count - 1);
        CompletionList.ScrollIntoView(CompletionList.SelectedItem);
    }

    private void InsertSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag })
        {
            return;
        }

        var thing = _selected?.Name ?? "标签_结果";
        var snippet = tag switch
        {
            "condition" =>
                """
                
                如果 (1 == 1) {
                    输出("条件成立")
                } 否则 {
                    输出("条件不成立")
                }
                """,
            "json" =>
                """
                
                设 原始: 文本 = "{\"name\":\"FrostUI\",\"items\":[1,2,3]}"
                设 数据: JSON = JSON.解析(原始)
                输出(文本(JSON.取值(数据, "name")))
                """,
            "http" =>
                """
                
                提示("正在请求…")
                设 数据: JSON = HTTP.获取JSON("https://api.github.com/repos/dotnet/wpf")
                输出(文本(JSON.取值(数据, "full_name")))
                """,
            "counter" =>
                """
                
                全局 次数: 整数 = 0
                全局 次数 = 次数 + 1
                输出("第 " + 文本(次数) + " 次点击")
                """,
            "property" => $"\n\n{thing}.文本 = \"新的文本\"\n",
            _ => string.Empty,
        };

        InsertIntoScript(snippet);
    }

    private void InsertFullExample_OnClick(object sender, RoutedEventArgs e)
    {
        var snippet = _scriptControl is null
            ? """
              // 窗口 · 启动
              输出("窗口启动")
              设 标题: 文本 = "FrostUI Designer"
              输出("当前设计：" + 标题)
              """
            : _scriptEvent switch
            {
                "点击" =>
                    $"""
                     // {_scriptControl.Name} · 点击
                     全局 点击次数: 整数 = 0
                     全局 点击次数 = 点击次数 + 1
                     标签_结果.文本 = "{_scriptControl.Name} 被点击 " + 文本(点击次数) + " 次"
                     输出("点击次数：" + 文本(点击次数))
                     """,
                "切换" =>
                    $$"""
                     // {{_scriptControl.Name}} · 切换
                     如果 ({{_scriptControl.Name}}.开启) {
                         输出("已开启")
                     } 否则 {
                         输出("已关闭")
                     }
                     """,
                "文本改变" =>
                    $"""
                     // {_scriptControl.Name} · 文本改变
                     输出("输入内容：" + {_scriptControl.Name}.文本)
                     """,
                "值改变" =>
                    $"""
                     // {_scriptControl.Name} · 值改变
                     输出("当前值：" + 文本({_scriptControl.Name}.值))
                     """,
                _ => "输出(\"事件触发\")",
            };

        InsertIntoScript(snippet, replaceIfOnlyWhitespace: true);
    }

    private void InsertIntoScript(string snippet, bool replaceIfOnlyWhitespace = false)
    {
        if (string.IsNullOrEmpty(snippet))
        {
            return;
        }

        if (replaceIfOnlyWhitespace && string.IsNullOrWhiteSpace(ScriptEditor.Text))
        {
            ScriptEditor.Text = snippet.TrimStart('\n', '\r');
            ScriptEditor.SelectionStart = ScriptEditor.Text.Length;
            ScriptEditor.Focus();
            return;
        }

        var start = ScriptEditor.SelectionStart;
        var length = ScriptEditor.SelectionLength;
        var text = ScriptEditor.Text;
        var insert = snippet;
        if (start > 0 && text[start - 1] != '\n')
        {
            insert = "\n" + insert;
        }

        ScriptEditor.Text = text[..start] + insert + text[(start + length)..];
        ScriptEditor.SelectionStart = start + insert.Length;
        ScriptEditor.Focus();
    }

    // ============================================================
    // Preview
    // ============================================================

    private void RunPreview_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentScript();
        OpenPreview();
    }

    private void ExportExe_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentScript();
        var dialog = new ExportDialog(_document)
        {
            Owner = this,
        };
        dialog.ShowDialog();
    }

    public PreviewWindow? OpenPreview()
    {
        SaveCurrentScript();
        if (_previewWindow is { IsVisible: true })
        {
            _previewWindow.Activate();
            return _previewWindow;
        }

        try
        {
            var clone = DesignSerializer.Clone(_document);
            _previewWindow = new PreviewWindow(
                clone,
                (message, level) => Dispatcher.Invoke(() => AppendLog(message, level)));
            _previewWindow.Owner = this;
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.Show();
            AppendLog($"预览启动 · {_document.Name}", ScriptLogLevel.Success);
            StatusText.Text = "预览窗口已打开";
            return _previewWindow;
        }
        catch (Exception ex)
        {
            AppendLog($"无法启动预览：{ex.Message}", ScriptLogLevel.Error);
            StatusText.Text = "预览启动失败";
            return null;
        }
    }

    public void SelectControlByName(string name)
    {
        var control = _document.FindControl(name);
        if (control is not null)
        {
            SelectControl(control);
        }
    }

    public void OpenLayersForScreenshot()
    {
        RefreshLayerView();
        LayersPopup.IsOpen = true;
    }

    // ============================================================
    // Theme & output
    // ============================================================

    private void ThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeGlyph();
        AppendLog(ThemeManager.IsDark ? "切换到深色主题" : "切换到浅色主题", ScriptLogLevel.Info);
    }

    private void ThemeColorButton_OnClick(object sender, RoutedEventArgs e) =>
        OpenThemeColorWindow();

    public void OpenThemeColorWindow()
    {
        var original = ThemeManager.Accent;
        var dialog = new ThemeColorWindow(original, livePreview: true, _document.ThemePalette)
        {
            Owner = this,
        };
        dialog.PresetSaved += AddThemePreset;
        if (dialog.ShowDialog() == true)
        {
            var color = dialog.SelectedColor;
            _document.AccentColor = ThemeColorHelper.ToHex(color);
            ThemeManager.SetAccent(color);
            MarkDirty("主题色");
            AppendLog($"主题色已改为 {_document.AccentColor}", ScriptLogLevel.Success);
        }
        else
        {
            ThemeManager.SetAccent(original);
        }
    }

    private void AddThemePreset(string hex)
    {
        if (!_document.ThemePalette.Any(value =>
                string.Equals(value, hex, StringComparison.OrdinalIgnoreCase)))
        {
            _document.ThemePalette.Add(hex);
        }

        MarkDirty("主题色预设");
    }

    private void ControlAccent_OnClick(object sender, RoutedEventArgs e)
    {
        var targets = _selection.Count > 0
            ? _selection.ToList()
            : _selected is null
                ? []
                : [_selected];
        if (targets.Count == 0)
        {
            AppendLog("请先选择控件，再修改控件主题色。", ScriptLogLevel.Warning);
            return;
        }

        var initial = ThemeManager.Accent;
        if (ThemeColorHelper.TryParse(targets[0].AccentColor, out var existing))
        {
            initial = existing;
        }

        var dialog = new ThemeColorWindow(initial, livePreview: false, _document.ThemePalette)
        {
            Owner = this,
        };
        dialog.PresetSaved += AddThemePreset;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var hex = ThemeColorHelper.ToHex(dialog.SelectedColor);
        foreach (var target in targets)
        {
            target.AccentColor = hex;
        }

        MarkDirty("控件主题色");
        AppendLog($"控件主题色已改为 {hex}", ScriptLogLevel.Success);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateThemeGlyph();

    private void UpdateThemeGlyph()
    {
        if (ThemeGlyph is null)
        {
            return;
        }

        ThemeGlyph.Text = ThemeManager.IsDark ? "\uE706" : "\uE708";
        ThemeButton.ToolTip = ThemeManager.IsDark ? "切换到浅色主题" : "切换到深色主题";
    }

    private void AppendLog(string message, ScriptLogLevel level = ScriptLogLevel.Info)
    {
        if (OutputTextBox is null)
        {
            return;
        }

        var tag = level switch
        {
            ScriptLogLevel.Success => "成功",
            ScriptLogLevel.Warning => "警告",
            ScriptLogLevel.Error => "错误",
            _ => "信息",
        };

        OutputTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [{tag}] {message}{Environment.NewLine}");
        if (OutputTextBox.Text.Length > 120_000)
        {
            OutputTextBox.Text = OutputTextBox.Text[^60_000..];
        }

        OutputTextBox.ScrollToEnd();
        OutputStatusText.Text = level switch
        {
            ScriptLogLevel.Error => "有错误",
            ScriptLogLevel.Warning => "注意",
            _ => "就绪",
        };
    }

    private void ClearOutput_OnClick(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Clear();
        OutputStatusText.Text = "就绪";
    }

    // ============================================================
    // Keyboard shortcuts
    // ============================================================

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.K)
        {
            ShowCommandPalette();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.S)
            {
                SaveProject();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenProject_OnClick(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                NewProject_OnClick(sender, e);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D && Keyboard.FocusedElement is not TextBox)
            {
                DuplicateSelected();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && Keyboard.FocusedElement is not TextBox)
            {
                SelectAllControls();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F5)
        {
            RunPreview_OnClick(sender, e);
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
            e.Key is Key.Z &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            RedoHistory();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.Z)
        {
            UndoHistory();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.Y)
        {
            RedoHistory();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            SelectWindow();
            e.Handled = true;
            return;
        }

        if (_selected is null)
        {
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? _document.GridSize : 1;
        var moved = false;
        foreach (var item in _selection.Where(x => !x.IsLocked))
        {
            switch (e.Key)
            {
                case Key.Left:
                    item.X = Math.Max(0, item.X - step);
                    moved = true;
                    break;
                case Key.Right:
                    item.X = Math.Min(
                        Math.Max(0, _document.WindowWidth - item.Width),
                        item.X + step);
                    moved = true;
                    break;
                case Key.Up:
                    item.Y = Math.Max(0, item.Y - step);
                    moved = true;
                    break;
                case Key.Down:
                    item.Y = Math.Min(
                        Math.Max(0, _document.ContentHeight - item.Height),
                        item.Y + step);
                    moved = true;
                    break;
            }
        }

        if (moved)
        {
            UpdateStatusSelection();
            e.Handled = true;
        }
    }

    // ============================================================
    // Visual helpers
    // ============================================================

    private static T? FindDataContext<T>(DependencyObject? source) where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T value })
            {
                return value;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private static FrameworkElement? FindAncestorWithTag(DependencyObject? source, string tag)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element &&
                string.Equals(element.Tag as string, tag, StringComparison.Ordinal))
            {
                return element;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return null;
    }
}
