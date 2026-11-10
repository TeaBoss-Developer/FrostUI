using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FrostUI.Demo.Models;
using FrostUI.Demo.Services;
using FrostUI.Demo.Views;
using FrostUI.Theme;

namespace FrostUI.Demo;

public partial class MainWindow
{
    private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _toastTimer;
    private string? _currentPageKey;

    public MainWindow()
    {
        InitializeComponent();

        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 32);
        Height = Math.Min(Height, workArea.Height - 32);

        DataContext = RuntimeMetrics.Current;
        NavList.ItemsSource = new[]
        {
            new NavItem("概览", "\uE80F", "overview"),
            new NavItem("组件", "\uE8A9", "components", "12"),
            new NavItem("外观", "\uE790", "appearance"),
            new NavItem("设置", "\uE713", "settings"),
        };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _toastTimer.Tick += OnToastTimerTick;

        Loaded += OnLoaded;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
        ThemeManager.ThemeChanged += OnThemeChanged;
        UpdateThemeGlyph();
    }

    public void NavigateTo(string pageKey)
    {
        var item = NavList.Items.OfType<NavItem>()
            .FirstOrDefault(x => string.Equals(x.PageKey, pageKey, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return;
        }

        if (ReferenceEquals(NavList.SelectedItem, item))
        {
            ShowPage(item.PageKey);
            return;
        }

        NavList.SelectedItem = item;
    }

    public void ShowToast(string message, string glyph = "\uE73E")
    {
        _toastTimer.Stop();
        ToastGlyph.Text = glyph;
        ToastText.Text = message;

        ToastHost.BeginAnimation(OpacityProperty, null);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ToastHost.Opacity = 0;
        ToastTransform.Y = 12;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        var rise = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ToastHost.BeginAnimation(OpacityProperty, fadeIn);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, rise);
        _toastTimer.Start();
    }

    public void ApplyBackdropForCapture()
    {
        WindowState = WindowState.Normal;
        RefreshBackdrop();
        Activate();
        UpdateLayout();
    }

    public void SetAmbientEnabled(bool enabled)
    {
        AmbientLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(
            enabled ? 1 : 0,
            TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RuntimeMetrics.Current.Start(this);
        if (NavList.SelectedIndex < 0)
        {
            NavList.SelectedIndex = 0;
        }

        if (PageHost.Content is null)
        {
            ShowPage("overview");
        }

        UpdateThemeGlyph();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        RuntimeMetrics.Current.Stop();
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is NavItem item)
        {
            ShowPage(item.PageKey);
        }
    }

    private void ShowPage(string pageKey)
    {
        if (string.Equals(_currentPageKey, pageKey, StringComparison.OrdinalIgnoreCase) &&
            PageHost.Content is not null)
        {
            return;
        }

        if (!_pages.TryGetValue(pageKey, out var page))
        {
            page = pageKey switch
            {
                "components" => new ComponentsView(),
                "appearance" => new AppearanceView(),
                "settings" => new SettingsView(),
                _ => new OverviewView(),
            };
            _pages[pageKey] = page;
        }

        _currentPageKey = pageKey;
        page.Opacity = 0;
        var transform = new TranslateTransform(0, 9);
        page.RenderTransform = transform;
        PageHost.Content = page;

        page.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        transform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeGlyph();
        Dispatcher.BeginInvoke(RefreshBackdrop);
    }

    private void UpdateThemeGlyph()
    {
        ThemeGlyph.Text = ThemeManager.IsDark ? "\uE706" : "\uE708";
        ThemeButton.ToolTip = ThemeManager.IsDark ? "切换到浅色主题" : "切换到深色主题";
    }

    private void ThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        ShowToast(ThemeManager.IsDark ? "已切换到深色主题" : "已切换到浅色主题", "\uE790");
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SearchBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchShell.BorderBrush = (Brush)FindResource("AccentBorderBrush");
        SearchShell.Background = (Brush)FindResource("InputFocusBrush");
    }

    private void SearchBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchShell.BorderBrush = (Brush)FindResource("InputBorderBrush");
        SearchShell.Background = (Brush)FindResource("InputBrush");
    }

    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            ShowToast($"没有找到「{SearchBox.Text.Trim()}」的本地结果", "\uE721");
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            Keyboard.ClearFocus();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                 Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            ThemeManager.ToggleTheme();
            e.Handled = true;
        }
    }

    private void OnToastTimerTick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        ToastHost.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void DocsButton_OnClick(object sender, RoutedEventArgs e) =>
        ShowToast("文档位于 frost-ui/README.md", "\uE8A5");

    private void ChangelogButton_OnClick(object sender, RoutedEventArgs e) =>
        ShowToast("FrostUI 1.0 · 初版包含 12 个控件与双主题", "\uE81C");

    private void GithubButton_OnClick(object sender, RoutedEventArgs e) =>
        ShowToast("示例源码已放在当前工作区 frost-ui", "\uE774");

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e) => NavigateTo("settings");
}
