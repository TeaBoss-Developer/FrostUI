using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using FrostUI.Interop;
using FrostUI.Theme;

namespace FrostUI.Controls;

/// <summary>
/// A borderless WPF window with a native Acrylic / Mica backdrop, rounded DWM corners,
/// a custom title bar and an optional content slot for the title bar.
/// </summary>
public class FrostWindow : Window
{
    private const int WmDwmCompositionChanged = 0x031E;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 2;

    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop),
        typeof(WindowBackdrop),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(WindowBackdrop.Acrylic, OnBackdropPropertyChanged));

    public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register(
        nameof(TintColor),
        typeof(Color),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(Color.FromRgb(0x0A, 0x0F, 0x1A), OnBackdropPropertyChanged));

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity),
        typeof(double),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(0.30, OnBackdropPropertyChanged, CoerceTintOpacity));

    public static readonly DependencyProperty FollowThemeTintProperty = DependencyProperty.Register(
        nameof(FollowThemeTint),
        typeof(bool),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(true, OnBackdropPropertyChanged));

    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
        nameof(TitleBarHeight),
        typeof(double),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(54d, OnTitleBarHeightChanged, CoerceTitleBarHeight));

    public static readonly DependencyProperty TitleBarContentProperty = DependencyProperty.Register(
        nameof(TitleBarContent),
        typeof(object),
        typeof(FrostWindow),
        new FrameworkPropertyMetadata(null));

    private static readonly DependencyPropertyKey IsBackdropActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsBackdropActive),
            typeof(bool),
            typeof(FrostWindow),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsBackdropActiveProperty =
        IsBackdropActivePropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey EffectiveBackdropPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(EffectiveBackdrop),
            typeof(WindowBackdrop),
            typeof(FrostWindow),
            new FrameworkPropertyMetadata(WindowBackdrop.None));

    public static readonly DependencyProperty EffectiveBackdropProperty =
        EffectiveBackdropPropertyKey.DependencyProperty;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _applied;

    static FrostWindow()
    {
        // Let the template come from the merged FrostUI resource dictionary.
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FrostWindow),
            new FrameworkPropertyMetadata(typeof(FrostWindow)));
    }

    public FrostWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResize;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

        var chrome = new WindowChrome
        {
            CaptionHeight = TitleBarHeight,
            ResizeBorderThickness = new Thickness(7),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        };
        WindowChrome.SetWindowChrome(this, chrome);

        MinimizeCommand = new RelayCommand(() => WindowState = WindowState.Minimized);
        MaximizeCommand = new RelayCommand(
            () => WindowState = WindowState.Maximized,
            () => ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip);
        RestoreCommand = new RelayCommand(() => WindowState = WindowState.Normal);
        CloseCommand = new RelayCommand(Close);

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closed += OnClosed;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    /// <summary>Minimizes the window. Bound by the built-in caption button.</summary>
    public ICommand MinimizeCommand { get; }

    /// <summary>Maximizes the window. Bound by the built-in caption button.</summary>
    public ICommand MaximizeCommand { get; }

    /// <summary>Restores the window. Bound by the built-in caption button.</summary>
    public ICommand RestoreCommand { get; }

    /// <summary>Closes the window. Bound by the built-in caption button.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>The requested material. Unsupported materials fall back gracefully.</summary>
    public WindowBackdrop Backdrop
    {
        get => (WindowBackdrop)GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>The tint used by the acrylic compositor. Ignored when <see cref="FollowThemeTint"/> is true.</summary>
    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    /// <summary>Opacity of the acrylic tint, from 0 to 1.</summary>
    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    /// <summary>When true, the tint follows the dark/light theme automatically.</summary>
    public bool FollowThemeTint
    {
        get => (bool)GetValue(FollowThemeTintProperty);
        set => SetValue(FollowThemeTintProperty, value);
    }

    /// <summary>Height of the custom title bar in device-independent pixels.</summary>
    public double TitleBarHeight
    {
        get => (double)GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    /// <summary>Content displayed in the middle of the custom title bar.</summary>
    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    /// <summary>True when a native backdrop was successfully attached.</summary>
    public bool IsBackdropActive => (bool)GetValue(IsBackdropActiveProperty);

    /// <summary>The material that is actually active after capability checks.</summary>
    public WindowBackdrop EffectiveBackdrop => (WindowBackdrop)GetValue(EffectiveBackdropProperty);

    /// <summary>Re-applies the current material, for example after moving to a new monitor.</summary>
    public void RefreshBackdrop()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ApplyBackdrop();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        if (_source?.CompositionTarget is not null)
        {
            // The crucial WPF switch: without it the transparent client area is painted black.
            _source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        _source?.AddHook(WndProc);
        ApplyBackdrop();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_applied && sizeInfo.WidthChanged && WindowState == WindowState.Normal)
        {
            // Some drivers drop the accent policy when a layered accent window resizes.
            Dispatcher.BeginInvoke(ApplyBackdrop);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBackdrop();

        // The DWM may create the final composition target a beat after Loaded.
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(ApplyBackdrop));
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(ApplyBackdrop);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        Loaded -= OnLoaded;
        StateChanged -= OnStateChanged;
        Closed -= OnClosed;
        _source?.RemoveHook(WndProc);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyBackdrop();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDwmCompositionChanged)
        {
            ApplyBackdrop();
        }

        // 无边框 + WindowChrome 的窗口最大化时会盖住任务栏：把最大化范围限制到工作区。
        if (msg == WmGetMinMaxInfo)
        {
            if (TryConstrainToWorkArea(hwnd, lParam))
            {
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryConstrainToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        try
        {
            var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
            var monitor = NativeMethods.MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var monitorInfo = new NativeMethods.MonitorInfo
            {
                Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(),
            };
            if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
            info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
            info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
            info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
            Marshal.StructureToPtr(info, lParam, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyBackdrop()
    {
        if (_hwnd == IntPtr.Zero || _source?.CompositionTarget is null)
        {
            return;
        }

        _applied = true;
        var dark = ThemeManager.IsDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DwmUseImmersiveDarkMode,
            ref dark,
            sizeof(int));

        var corner = NativeMethods.DwmWindowCornerRound;
        NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DwmWindowCornerPreference,
            ref corner,
            sizeof(int));

        var compositionResult = NativeMethods.DwmIsCompositionEnabled(out var compositionEnabled);
        if (compositionResult != 0 || !compositionEnabled)
        {
            DisableBackdrop();
            return;
        }

        switch (Backdrop)
        {
            case WindowBackdrop.Acrylic:
                if (!TryAcrylic())
                {
                    if (!TryLegacyBlur())
                    {
                        DisableBackdrop();
                    }
                }

                break;

            case WindowBackdrop.Blur:
                if (!TryLegacyBlur())
                {
                    DisableBackdrop();
                }

                break;

            case WindowBackdrop.Mica:
            case WindowBackdrop.MicaAlt:
                if (!TryMica(Backdrop == WindowBackdrop.MicaAlt))
                {
                    // Mica needs Windows 11 22H2+. Acrylic is the closest usable fallback.
                    if (!TryAcrylic())
                    {
                        DisableBackdrop();
                    }
                }

                break;

            default:
                DisableBackdrop();
                break;
        }
    }

    private bool TryAcrylic()
    {
        if (!ClearMicaFrame())
        {
            return false;
        }

        var tint = GetEffectiveTint();
        var alpha = (byte)Math.Round(Math.Clamp(TintOpacity, 0d, 1d) * tint.A);
        var color =
            ((uint)alpha << 24) |
            ((uint)tint.B << 16) |
            ((uint)tint.G << 8) |
            tint.R;

        var policy = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentEnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = unchecked((int)color),
            AnimationId = 0,
        };

        if (!SetAccentPolicy(policy))
        {
            return false;
        }

        SetBackdropActive(WindowBackdrop.Acrylic, true);
        return true;
    }

    private bool TryLegacyBlur()
    {
        if (!ClearMicaFrame())
        {
            return false;
        }

        var policy = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentEnableBlurBehind,
            AccentFlags = 2,
            GradientColor = 0,
            AnimationId = 0,
        };

        if (!SetAccentPolicy(policy))
        {
            return false;
        }

        SetBackdropActive(WindowBackdrop.Blur, true);
        return true;
    }

    private bool TryMica(bool alt)
    {
        if (Environment.OSVersion.Version.Build < 22621)
        {
            return false;
        }

        // Switch off the legacy accent before asking DWM for a system backdrop.
        var disabled = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentDisabled,
            AccentFlags = 0,
            GradientColor = 0,
            AnimationId = 0,
        };
        SetAccentPolicy(disabled);

        var margins = new NativeMethods.Margins
        {
            Left = -1,
            Right = -1,
            Top = -1,
            Bottom = -1,
        };
        if (NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref margins) != 0)
        {
            return false;
        }

        var type = alt ? NativeMethods.DwmsbtTabbedWindow : NativeMethods.DwmsbtMainWindow;
        if (NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DwmSystemBackdropType,
                ref type,
                sizeof(int)) != 0)
        {
            return false;
        }

        // A transparent DWM border keeps the custom 1px edge crisp on Windows 11.
        var border = NativeMethods.DwmColorNone;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DwmBorderColor, ref border, sizeof(int));

        SetBackdropActive(alt ? WindowBackdrop.MicaAlt : WindowBackdrop.Mica, true);
        return true;
    }

    private bool ClearMicaFrame()
    {
        // Clear a previous Mica backdrop and reset the extended frame.
        var none = NativeMethods.DwmsbtNone;
        NativeMethods.DwmSetWindowAttribute(
            _hwnd,
            NativeMethods.DwmSystemBackdropType,
            ref none,
            sizeof(int));

        var margins = new NativeMethods.Margins();
        NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        return true;
    }

    private bool SetAccentPolicy(NativeMethods.AccentPolicy policy)
    {
        var size = Marshal.SizeOf<NativeMethods.AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, pointer, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WcaAccentPolicy,
                Data = pointer,
                SizeOfData = size,
            };
            return NativeMethods.SetWindowCompositionAttribute(_hwnd, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private void DisableBackdrop()
    {
        var policy = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentDisabled,
            AccentFlags = 0,
            GradientColor = 0,
            AnimationId = 0,
        };
        SetAccentPolicy(policy);
        ClearMicaFrame();
        SetBackdropActive(WindowBackdrop.None, false);
    }

    private Color GetEffectiveTint()
    {
        if (!FollowThemeTint)
        {
            return TintColor;
        }

        return ThemeManager.IsDark
            ? Color.FromRgb(0x0A, 0x0F, 0x1A)
            : Color.FromRgb(0xF3, 0xF6, 0xFD);
    }

    private void SetBackdropActive(WindowBackdrop effective, bool active)
    {
        SetValue(EffectiveBackdropPropertyKey, effective);
        SetValue(IsBackdropActivePropertyKey, active);
    }

    private static void OnBackdropPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrostWindow window && window._hwnd != IntPtr.Zero)
        {
            window.ApplyBackdrop();
        }
    }

    private static object CoerceTintOpacity(DependencyObject d, object value) =>
        Math.Clamp((double)value, 0d, 1d);

    private static void OnTitleBarHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrostWindow window)
        {
            return;
        }

        if (WindowChrome.GetWindowChrome(window) is { } chrome)
        {
            chrome.CaptionHeight = window.TitleBarHeight;
        }
    }

    private static object CoerceTitleBarHeight(DependencyObject d, object value) =>
        Math.Max(0d, (double)value);
}

