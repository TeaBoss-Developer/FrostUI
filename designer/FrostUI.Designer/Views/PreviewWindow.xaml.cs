using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FrostUI.Designer.Models;
using FrostUI.Designer.Scripting;
using FrostUI.Designer.Services;
using FrostUI.Theme;

namespace FrostUI.Designer.Views;

public partial class PreviewWindow
{
    private readonly DesignDocument _document;
    private readonly ScriptRuntime _runtime;
    private readonly Action<string, ScriptLogLevel> _logSink;
    private readonly DispatcherTimer _toastTimer;
    private readonly Dictionary<DesignControl, FrameworkElement> _visuals = [];
    private readonly Dictionary<string, string> _pageParameters;
    private DesignDocument? _overlayDocument;
    private ScriptRuntime? _overlayRuntime;
    private bool _effectiveAnchorLayout;
    private bool _pendingPageStartup;
    private bool _suppressUiEvents = true;

    public PreviewWindow(
        DesignDocument document,
        Action<string, ScriptLogLevel>? logSink = null,
        bool standalone = false,
        IReadOnlyDictionary<string, string>? pageParameters = null)
    {
        InitializeComponent();

        _document = document;
        if (ThemeColorHelper.TryParse(document.AccentColor, out var accent))
        {
            ThemeManager.SetAccent(accent);
        }

        _pageParameters = pageParameters is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(pageParameters, StringComparer.Ordinal);
        _logSink = logSink ?? ((_, _) => { });
        ApplyPageWindowSettings();
        PreviewTitleHint.Text = standalone
            ? $"FrostUI 桌面应用 · {PageWidth:0} × {PageHeight:0}"
            : $"交互预览 · {PageWidth:0} × {PageHeight:0}";
        if (standalone)
        {
            PreviewBadge.Visibility = Visibility.Collapsed;
            ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _effectiveAnchorLayout = PageAnchorLayout;
        ApplyResponsiveBreakpoint(PageWidth);
        PreviewCanvas.Width = PageWidth;
        PreviewCanvas.Height = PageContentHeight;

        _runtime = new ScriptRuntime(document)
        {
            Logger = (message, level) =>
            {
                _logSink(message, level);
                if (level == ScriptLogLevel.Error)
                {
                    ShowToast(message);
                }
            },
            Toast = ShowToast,
            Navigate = (action, pageId) =>
            {
                Navigate(action, pageId ?? string.Empty);
                return true;
            },
            // Overlay runtime callbacks are replaced per dialog.
            GetPageParameter = key =>
                _pageParameters.TryGetValue(key, out var value) ? value : null,
            SetPageParameter = (key, value) => _pageParameters[key] = value,
            SetPageResult = value =>
            {
                Result = value;
                _pageParameters["结果"] = value;
            },
            GetProjectValue = key => ProjectDataStore.Get(_document.Name, key),
            SetProjectValue = (key, value) => ProjectDataStore.Set(_document.Name, key, value),
        };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _toastTimer.Tick += OnToastTimerTick;

        BuildPreview();
        Loaded += OnLoaded;
    }

    private void BuildPreview()
    {
        _suppressUiEvents = true;
        foreach (var control in _document.Controls)
        {
            _visuals[control] = PreviewControlFactory.Create(control, TriggerEvent);
        }

        var containers = _document.Controls
            .Where(control => control.IsContainer && !string.IsNullOrWhiteSpace(control.Name))
            .ToDictionary(control => control.Name, StringComparer.Ordinal);
        foreach (var control in _document.Controls)
        {
            if (!containers.ContainsKey(control.ParentName))
            {
                PreviewCanvas.Children.Add(_visuals[control]);
            }
        }

        foreach (var container in containers.Values)
        {
            PopulateContainer(container);
        }
    }

    private void PopulateContainer(DesignControl container)
    {
        if (_visuals[container].Tag is not ContainerHost host)
        {
            return;
        }

        var children = _document.Controls
            .Where(control => string.Equals(
                control.ParentName,
                container.Name,
                StringComparison.Ordinal))
            .ToList();
        var index = 0;
        foreach (var child in children)
        {
            var childVisual = _visuals[child];
            switch (container.Kind)
            {
                case ControlKind.Stack:
                    var spacing = container.ContainerSpacing;
                    childVisual.Margin = container.StackOrientation == "Horizontal"
                        ? new Thickness(0, 0, index == children.Count - 1 ? 0 : spacing, 0)
                        : new Thickness(0, 0, 0, index == children.Count - 1 ? 0 : spacing);
                    host.Panel.Children.Add(childVisual);
                    break;
                case ControlKind.Dock:
                    DockPanel.SetDock(childVisual, child.DockMode switch
                    {
                        "Bottom" => Dock.Bottom,
                        "Left" => Dock.Left,
                        "Right" => Dock.Right,
                        _ => Dock.Top,
                    });
                    host.Panel.Children.Add(childVisual);
                    break;
                case ControlKind.Grid:
                    Grid.SetRow(childVisual, Math.Min(child.GridRow, Math.Max(0, container.GridRows - 1)));
                    Grid.SetColumn(childVisual, Math.Min(child.GridColumn, Math.Max(0, container.GridColumns - 1)));
                    Grid.SetRowSpan(childVisual, Math.Min(child.GridRowSpan, container.GridRows));
                    Grid.SetColumnSpan(childVisual, Math.Min(child.GridColumnSpan, container.GridColumns));
                    host.Panel.Children.Add(childVisual);
                    break;
                case ControlKind.Scroll:
                    Canvas.SetLeft(childVisual, child.X);
                    Canvas.SetTop(childVisual, child.Y);
                    host.Panel.Children.Add(childVisual);
                    break;
                case ControlKind.Tabs:
                    Canvas.SetLeft(childVisual, child.X);
                    Canvas.SetTop(childVisual, child.Y);
                    var tabPanel = host.Panels is { Count: > 0 }
                        ? host.Panels[Math.Clamp(child.TabIndex - 1, 0, host.Panels.Count - 1)]
                        : host.Panel;
                    tabPanel.Children.Add(childVisual);
                    break;
                case ControlKind.SplitView:
                    Canvas.SetLeft(childVisual, child.X);
                    Canvas.SetTop(childVisual, child.Y);
                    var panePanel = host.Panels is { Count: > 0 }
                        ? host.Panels[Math.Clamp(child.PaneIndex, 0, host.Panels.Count - 1)]
                        : host.Panel;
                    panePanel.Children.Add(childVisual);
                    break;
                case ControlKind.Expander:
                    Canvas.SetLeft(childVisual, child.X);
                    Canvas.SetTop(childVisual, child.Y);
                    host.Panel.Children.Add(childVisual);
                    break;
            }

            if (child.IsContainer)
            {
                PopulateContainer(child);
            }

            index++;
        }

        if (host.ScrollCanvas is not null)
        {
            host.ScrollCanvas.Width = Math.Max(
                container.Width - container.ContainerPadding * 2,
                children.Count == 0 ? 0 : children.Max(child => child.X + child.Width));
            host.ScrollCanvas.Height = Math.Max(
                container.Height - container.ContainerPadding * 2,
                children.Count == 0 ? 0 : children.Max(child => child.Y + child.Height));
        }
    }

    private void PreviewScaler_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveBreakpoint(PreviewScaler.ActualWidth);
        ApplyAnchorLayout();
    }

    private DesignPage ActivePage => _document.ActivePage;

    private double PageWidth => ActivePage.Width ?? _document.WindowWidth;

    private double PageHeight => ActivePage.Height ?? _document.WindowHeight;

    private double PageContentHeight => Math.Max(120, PageHeight - 54);

    private bool PageScaleContent => ActivePage.ScaleContent ?? _document.ScaleContent;

    private bool PageAnchorLayout => ActivePage.AnchorLayout ?? _document.AnchorLayout;

    private string? PageBreakpoint => ActivePage.BreakpointSpec ?? _document.BreakpointSpec;

    private string? PageTitleBinding => ActivePage.TitleBinding ?? _document.TitleBinding;

    private void ApplyPageWindowSettings()
    {
        Title = string.IsNullOrWhiteSpace(ActivePage.Title)
            ? _document.WindowTitle
            : ActivePage.Title;
        Width = PageWidth;
        Height = PageHeight;
        Backdrop = ActivePage.Backdrop ?? _document.Backdrop;
        TintOpacity = ActivePage.TintOpacity ?? _document.TintOpacity;
    }

    private void ApplyResponsiveBreakpoint(double width)
    {
        if (string.IsNullOrWhiteSpace(PageBreakpoint))
        {
            _effectiveAnchorLayout = PageAnchorLayout;
            PreviewScaler.Stretch = PageScaleContent ? Stretch.Uniform : Stretch.None;
            return;
        }

        var selected = PageBreakpoint
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var pieces = part.Split(':', 2, StringSplitOptions.TrimEntries);
                return double.TryParse(pieces[0], out var minimum)
                    ? (Minimum: minimum, Mode: pieces.Length > 1 ? pieces[1].ToLowerInvariant() : "fixed")
                    : (Minimum: -1d, Mode: "fixed");
            })
            .Where(item => item.Minimum >= 0 && item.Minimum <= width)
            .OrderBy(item => item.Minimum)
            .LastOrDefault();
        switch (selected.Mode)
        {
            case "scale":
                PreviewScaler.Stretch = Stretch.Uniform;
                _effectiveAnchorLayout = false;
                break;
            case "anchor":
                PreviewScaler.Stretch = Stretch.None;
                _effectiveAnchorLayout = true;
                break;
            default:
                PreviewScaler.Stretch = Stretch.None;
                _effectiveAnchorLayout = false;
                break;
        }
    }

    private void ApplyAnchorLayout()
    {
        if (!_effectiveAnchorLayout || PreviewScaler.Stretch == Stretch.Uniform)
        {
            return;
        }

        var contentWidth = PreviewScaler.ActualWidth;
        var contentHeight = PreviewScaler.ActualHeight;
        if (contentWidth < 1 || contentHeight < 1)
        {
            return;
        }

        PreviewCanvas.Width = contentWidth;
        PreviewCanvas.Height = contentHeight;
        var dx = contentWidth - PageWidth;
        var dy = contentHeight - PageContentHeight;
        foreach (var (control, visual) in _visuals)
        {
            if (!string.IsNullOrWhiteSpace(control.ParentName))
            {
                continue;
            }

            var x = control.X;
            var y = control.Y;
            var width = control.Width;
            var height = control.Height;
            switch (control.Anchor)
            {
                case "Left":
                    x = control.MarginLeft;
                    break;
                case "Top":
                    y = control.MarginTop;
                    break;
                case "Right":
                    x = contentWidth - control.Width - control.MarginRight;
                    break;
                case "Bottom":
                    y = contentHeight - control.Height - control.MarginBottom;
                    break;
                case "RightBottom":
                    x = contentWidth - control.Width - control.MarginRight;
                    y = contentHeight - control.Height - control.MarginBottom;
                    break;
                case "CenterX":
                    x += dx / 2;
                    break;
                case "CenterY":
                    y += dy / 2;
                    break;
                case "StretchHorizontal":
                    width = contentWidth - control.X - control.MarginRight;
                    break;
                case "StretchVertical":
                    height = contentHeight - control.Y - control.MarginBottom;
                    break;
                case "StretchAll":
                    width = contentWidth - control.X - control.MarginRight;
                    height = contentHeight - control.Y - control.MarginBottom;
                    break;
                case "StretchLeftRight":
                    x = control.MarginLeft;
                    width = contentWidth - control.MarginLeft - control.MarginRight;
                    break;
                case "StretchTopBottom":
                    y = control.MarginTop;
                    height = contentHeight - control.MarginTop - control.MarginBottom;
                    break;
                case "Fill":
                    x = control.MarginLeft;
                    y = control.MarginTop;
                    width = contentWidth - control.MarginLeft - control.MarginRight;
                    height = contentHeight - control.MarginTop - control.MarginBottom;
                    break;
            }

            Canvas.SetLeft(visual, x);
            Canvas.SetTop(visual, y);
            if (control.Anchor is "StretchHorizontal" or "StretchVertical" or "StretchAll"
                or "StretchLeftRight" or "StretchTopBottom" or "Fill")
            {
                var minimumWidth = Math.Max(20, control.MinWidth);
                var minimumHeight = Math.Max(20, control.MinHeight);
                if (control.MaxWidth > 1)
                {
                    width = Math.Min(width, control.MaxWidth);
                }

                if (control.MaxHeight > 1)
                {
                    height = Math.Min(height, control.MaxHeight);
                }

                visual.SetCurrentValue(
                    FrameworkElement.WidthProperty,
                    Math.Max(minimumWidth, width));
                visual.SetCurrentValue(
                    FrameworkElement.HeightProperty,
                    Math.Max(minimumHeight, height));
            }
        }
    }

    private void PlayPageTransition()
    {
        var page = ActivePage;
        var mode = page.Transition;
        if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
        {
            PreviewCanvas.Opacity = 1;
            PreviewCanvas.RenderTransform = Transform.Identity;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(page.TransitionDurationMs);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        PreviewCanvas.BeginAnimation(UIElement.OpacityProperty, null);
        PreviewCanvas.Opacity = 0;
        TranslateTransform? translate = null;
        ScaleTransform? scale = null;
        if (mode == "SlideLeft" || mode == "SlideRight")
        {
            translate = new TranslateTransform(mode == "SlideLeft" ? 32 : -32, 0);
            PreviewCanvas.RenderTransform = translate;
        }
        else if (mode == "Scale")
        {
            PreviewCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
            scale = new ScaleTransform(0.97, 0.97);
            PreviewCanvas.RenderTransform = scale;
        }
        else
        {
            PreviewCanvas.RenderTransform = Transform.Identity;
        }

        PreviewCanvas.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd,
            });
        if (translate is not null)
        {
            translate.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(translate.X, 0, duration)
                {
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.HoldEnd,
                });
        }

        if (scale is not null)
        {
            var animation = new DoubleAnimation(0.97, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd,
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }

    private void PlayOverlayTransition(DesignPage page)
    {
        var mode = page.Transition;
        if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase))
        {
            OverlayDialogBorder.Opacity = 1;
            OverlayDialogBorder.RenderTransform = Transform.Identity;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(page.TransitionDurationMs);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        OverlayDialogBorder.BeginAnimation(UIElement.OpacityProperty, null);
        OverlayDialogBorder.Opacity = 0;
        OverlayDialogBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.97, 0.97);
        OverlayDialogBorder.RenderTransform = scale;
        OverlayDialogBorder.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd,
            });
        var scaleAnimation = new DoubleAnimation(0.97, 1, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd,
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _suppressUiEvents = false;
        ApplyAnchorLayout();
        PlayPageTransition();

        await RunPageStartupAsync();
    }

    private void ApplyBindings()
    {
        foreach (var control in _document.Controls)
        {
            if (!string.IsNullOrWhiteSpace(control.TextBinding))
            {
                if (_runtime.TryGetGlobalValue(control.TextBinding, out var value))
                {
                    if (!string.IsNullOrWhiteSpace(control.TextBindingFormat))
                    {
                        try
                        {
                            value = string.Format(
                                System.Globalization.CultureInfo.CurrentCulture,
                                control.TextBindingFormat,
                                value);
                        }
                        catch (FormatException)
                        {
                            // Invalid user format: keep the raw value.
                        }
                    }

                    control.Text = value;
                }
                else if (!string.IsNullOrWhiteSpace(control.TextBindingFallback))
                {
                    control.Text = control.TextBindingFallback;
                }
            }

            if (!string.IsNullOrWhiteSpace(control.VisibilityBinding) &&
                _runtime.TryGetGlobalValue(control.VisibilityBinding, out var visibleValue))
            {
                var normalized = visibleValue.Trim().ToLowerInvariant();
                if (normalized is "true" or "1" or "是" or "真")
                {
                    control.IsVisible = true;
                }
                else if (normalized is "false" or "0" or "否" or "假")
                {
                    control.IsVisible = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(control.EnabledBinding) &&
                _runtime.TryGetGlobalValue(control.EnabledBinding, out var enabledValue))
            {
                var normalized = enabledValue.Trim().ToLowerInvariant();
                if (normalized is "true" or "1" or "是" or "真")
                {
                    control.IsEnabled = true;
                }
                else if (normalized is "false" or "0" or "否" or "假")
                {
                    control.IsEnabled = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(control.XBinding) &&
                _runtime.TryGetGlobalValue(control.XBinding, out var xValue) &&
                double.TryParse(xValue, out var x))
            {
                control.X = x;
            }

            if (!string.IsNullOrWhiteSpace(control.YBinding) &&
                _runtime.TryGetGlobalValue(control.YBinding, out var yValue) &&
                double.TryParse(yValue, out var y))
            {
                control.Y = y;
            }

            if (!string.IsNullOrWhiteSpace(control.WidthBinding) &&
                _runtime.TryGetGlobalValue(control.WidthBinding, out var widthValue) &&
                double.TryParse(widthValue, out var width))
            {
                control.Width = Math.Clamp(width, 20, 100000);
            }

            if (!string.IsNullOrWhiteSpace(control.HeightBinding) &&
                _runtime.TryGetGlobalValue(control.HeightBinding, out var heightValue) &&
                double.TryParse(heightValue, out var height))
            {
                control.Height = Math.Clamp(height, 20, 100000);
            }

            if (!string.IsNullOrWhiteSpace(control.OpacityBinding) &&
                _runtime.TryGetGlobalValue(control.OpacityBinding, out var opacityValue) &&
                double.TryParse(opacityValue, out var opacity))
            {
                control.Opacity = Math.Clamp(opacity, 0, 1);
            }

            if (!string.IsNullOrWhiteSpace(control.FontSizeBinding) &&
                _runtime.TryGetGlobalValue(control.FontSizeBinding, out var fontSizeValue) &&
                double.TryParse(fontSizeValue, out var fontSize))
            {
                control.FontSize = Math.Clamp(fontSize, 1, 300);
            }

            if (!string.IsNullOrWhiteSpace(control.CornerRadiusBinding) &&
                _runtime.TryGetGlobalValue(control.CornerRadiusBinding, out var cornerValue) &&
                double.TryParse(cornerValue, out var corner))
            {
                control.CornerRadius = Math.Clamp(corner, 0, 200);
            }

            if (!string.IsNullOrWhiteSpace(control.ValueBinding) &&
                _runtime.TryGetGlobalValue(control.ValueBinding, out var numericValue) &&
                double.TryParse(numericValue, out var numeric))
            {
                control.Value = Math.Clamp(numeric, 0, 100);
            }

            if (!string.IsNullOrWhiteSpace(control.IsOnBinding) &&
                _runtime.TryGetGlobalValue(control.IsOnBinding, out var onValue))
            {
                var normalized = onValue.Trim().ToLowerInvariant();
                if (normalized is "true" or "1" or "是" or "真")
                {
                    control.IsOn = true;
                }
                else if (normalized is "false" or "0" or "否" or "假")
                {
                    control.IsOn = false;
                }
            }

            if (control.Kind == ControlKind.Image &&
                !string.IsNullOrWhiteSpace(control.ImageBinding) &&
                _runtime.TryGetGlobalValue(control.ImageBinding, out var imagePath) &&
                !string.IsNullOrWhiteSpace(imagePath) &&
                File.Exists(imagePath))
            {
                control.ImagePath = imagePath;
            }
        }

        if (!string.IsNullOrWhiteSpace(PageTitleBinding) &&
            _runtime.TryGetGlobalValue(PageTitleBinding, out var title))
        {
            Title = title;
        }
    }

    public string? Result { get; private set; }

    private void TriggerEvent(DesignControl control, string eventName)
    {
        if (_suppressUiEvents)
        {
            return;
        }

        if (eventName == "点击" && control.PageAction != "None")
        {
            ApplyNavigationArguments(control.NavigationArgs);
            Navigate(
                control.PageAction,
                control.TargetPageId,
                control.DialogDisplay == "Overlay");
        }

        if (_runtime.IsRunning ||
            !control.Scripts.TryGetValue(eventName, out var code) ||
            string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        _ = RunEventAsync(control, eventName, code);
    }

    private void CloseOverlay_OnClick(object sender, RoutedEventArgs e) => CloseOverlay();

    private void CloseOverlay()
    {
        _overlayRuntime = null;
        _overlayDocument = null;
        OverlayCanvas.Children.Clear();
        DialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowOverlayPage(int pageIndex)
    {
        CloseOverlay();
        _overlayDocument = DesignSerializer.Clone(_document);
        _overlayDocument.ActivatePage(pageIndex);
        var page = _overlayDocument.ActivePage;
        var width = page.Width ?? _document.WindowWidth;
        var height = page.Height ?? _document.WindowHeight;
        OverlayCanvas.Width = width;
        OverlayCanvas.Height = Math.Max(120, height - 54);
        OverlayCanvas.Children.Clear();
        OverlayTitle.Text = string.IsNullOrWhiteSpace(page.Title) ? page.Name : page.Title;
        var runtime = new ScriptRuntime(_overlayDocument)
        {
            Logger = _logSink,
            Toast = ShowToast,
            Navigate = (action, target) =>
            {
                CloseOverlay();
                if (action != "Close")
                {
                    Navigate(action, target ?? string.Empty);
                }

                return true;
            },
            GetPageParameter = key =>
                _pageParameters.TryGetValue(key, out var value) ? value : null,
            SetPageParameter = (key, value) => _pageParameters[key] = value,
            SetPageResult = value =>
            {
                Result = value;
                _pageParameters["结果"] = value;
                CloseOverlay();
            },
            GetProjectValue = key => ProjectDataStore.Get(_document.Name, key),
            SetProjectValue = (key, value) => ProjectDataStore.Set(_document.Name, key, value),
        };
        _overlayRuntime = runtime;
        foreach (var control in _overlayDocument.Controls)
        {
            OverlayCanvas.Children.Add(PreviewControlFactory.Create(control, TriggerOverlayEvent));
        }

        DialogOverlay.Visibility = Visibility.Visible;
        PlayOverlayTransition(page);
        _ = RunOverlayStartupAsync();
    }

    private async Task RunOverlayStartupAsync()
    {
        var document = _overlayDocument;
        var runtime = _overlayRuntime;
        if (document is null || runtime is null)
        {
            return;
        }

        if (document.WindowScripts.TryGetValue("启动", out var startup) &&
            !string.IsNullOrWhiteSpace(startup))
        {
            await runtime.RunAsync(null, "启动", startup);
        }
    }

    private void TriggerOverlayEvent(DesignControl control, string eventName)
    {
        if (_overlayDocument is null || _overlayRuntime is null)
        {
            return;
        }

        if (eventName == "点击" && control.PageAction != "None")
        {
            ApplyNavigationArguments(control.NavigationArgs);
            if (control.PageAction == "Close")
            {
                CloseOverlay();
                return;
            }
        }

        if (control.Scripts.TryGetValue(eventName, out var code) &&
            !string.IsNullOrWhiteSpace(code))
        {
            _ = _overlayRuntime.RunAsync(control, eventName, code);
        }
    }

    private void ApplyNavigationArguments(string arguments)
    {
        foreach (var pair in (arguments ?? string.Empty)
                     .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            _pageParameters[pair[..separator].Trim()] = pair[(separator + 1)..].Trim();
        }
    }

    private void Navigate(string action, string targetPageId, bool forceOverlay = false)
    {
        if (action == "Close")
        {
            Close();
            return;
        }

        var pageIndex = _document.Pages
            .Select((page, index) => (page, index))
            .FirstOrDefault(item => string.Equals(item.page.Id, targetPageId, StringComparison.Ordinal)
                                    || string.Equals(item.page.Name, targetPageId, StringComparison.Ordinal))
            .index;
        if (pageIndex < 0 || pageIndex >= _document.Pages.Count ||
            !_document.Pages.Any(page => page.Id == targetPageId || page.Name == targetPageId))
        {
            return;
        }

        var targetPage = _document.Pages[pageIndex];
        if (action == "Open")
        {
            action = targetPage.Kind switch
            {
                DesignPageKind.Window => "Window",
                DesignPageKind.Dialog => "Dialog",
                _ => "Switch",
            };
        }

        if (action == "Switch")
        {
            _document.ActivatePage(pageIndex);
            RebuildPage();
            return;
        }

        if (action == "Dialog" &&
            (forceOverlay || targetPage.DialogDisplay == "Overlay"))
        {
            ShowOverlayPage(pageIndex);
            return;
        }

        var clone = DesignSerializer.Clone(_document);
        clone.ActivatePage(pageIndex);
        var window = new PreviewWindow(
            clone,
            _logSink,
            standalone: true,
            pageParameters: _pageParameters)
        {
            Owner = this,
            ShowInTaskbar = action != "Dialog",
        };
        if (action == "Dialog")
        {
            window.ShowDialog();
            if (window.Result is not null)
            {
                _pageParameters["结果"] = window.Result;
            }
        }
        else
        {
            window.Show();
        }
    }

    private void RebuildPage()
    {
        _visuals.Clear();
        PreviewCanvas.Children.Clear();
        ApplyPageWindowSettings();
        PreviewCanvas.Width = PageWidth;
        PreviewCanvas.Height = PageContentHeight;
        ApplyResponsiveBreakpoint(PageWidth);
        BuildPreview();
        ApplyBindings();
        PlayPageTransition();
        ApplyAnchorLayout();
        PreviewTitleHint.Text = $"交互预览 · {PageWidth:0} × {PageHeight:0}";
        _ = RunPageStartupAsync();
    }

    private async Task RunPageStartupAsync()
    {
        if (_runtime.IsRunning)
        {
            _pendingPageStartup = true;
            return;
        }

        if (_document.WindowScripts.TryGetValue("启动", out var startup) &&
            !string.IsNullOrWhiteSpace(startup))
        {
            await _runtime.RunAsync(null, "启动", startup);
        }

        ApplyBindings();
    }

    private async Task RunEventAsync(DesignControl control, string eventName, string code)
    {
        await _runtime.RunAsync(control, eventName, code);
        ApplyBindings();
        if (_pendingPageStartup)
        {
            _pendingPageStartup = false;
            await RunPageStartupAsync();
        }
    }

    private void ShowToast(string message)
    {
        _toastTimer.Stop();
        ToastText.Text = message;
        ToastHost.BeginAnimation(OpacityProperty, null);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ToastHost.Opacity = 0;
        ToastTransform.Y = 8;

        ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        _toastTimer.Start();
    }

    private void OnToastTimerTick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)));
    }
}
