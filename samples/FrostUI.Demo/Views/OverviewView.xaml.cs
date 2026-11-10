using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrostUI.Controls;
using FrostUI.Interop;
using FrostUI.Theme;

namespace FrostUI.Demo.Views;

public partial class OverviewView : UserControl
{
    private FrostWindow? _window;
    private DependencyPropertyDescriptor? _backdropDescriptor;
    private bool _ready;
    private bool _syncing;

    public OverviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ready = false;
        _window = Window.GetWindow(this) as FrostWindow;

        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            < 6 => "夜深了",
            < 12 => "早上好",
            < 18 => "下午好",
            _ => "晚上好",
        };
        GreetingText.Text = $"{greeting}，茶总";
        DateText.Text = $"O V E R V I E W   ·   {DateTime.Now:yyyy.MM.dd}";

        if (_window is not null)
        {
            TintSlider.Value = Math.Round(_window.TintOpacity * 100);
            TintValueText.Text = $"{TintSlider.Value:0}%";
            BlurToggle.IsChecked = _window.IsBackdropActive;
            SelectMaterial(_window.Backdrop);
            _window.RefreshBackdrop();
        }

        ThemeToggle.IsChecked = ThemeManager.IsDark;
        SyncAccent();

        _backdropDescriptor = DependencyPropertyDescriptor.FromProperty(
            FrostWindow.IsBackdropActiveProperty,
            typeof(FrostWindow));
        if (_window is not null)
        {
            _backdropDescriptor?.AddValueChanged(_window, OnBackdropStateChanged);
        }

        ThemeManager.ThemeChanged += OnThemeChanged;
        ThemeManager.AccentChanged += OnAccentChanged;
        _ready = true;

        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                if (_window is null)
                {
                    return;
                }

                _syncing = true;
                BlurToggle.IsChecked = _window.IsBackdropActive;
                SelectMaterial(_window.Backdrop);
                _syncing = false;
            }));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _ready = false;
        if (_window is not null)
        {
            _backdropDescriptor?.RemoveValueChanged(_window, OnBackdropStateChanged);
        }

        _backdropDescriptor = null;
        _window = null;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.AccentChanged -= OnAccentChanged;
    }

    private void OnBackdropStateChanged(object? sender, EventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        _syncing = true;
        BlurToggle.IsChecked = _window?.IsBackdropActive == true;
        if (_window is not null)
        {
            SelectMaterial(_window.Backdrop);
        }
        _syncing = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _syncing = true;
        ThemeToggle.IsChecked = ThemeManager.IsDark;
        _syncing = false;
    }

    private void OnAccentChanged(object? sender, EventArgs e) => SyncAccent();

    private void SyncAccent()
    {
        _syncing = true;
        var current = ThemeManager.Accent;
        foreach (var swatch in new[] { AccentBlue, AccentMint, AccentViolet, AccentPink, AccentAmber })
        {
            if (swatch.Tag is string tag &&
                ColorConverter.ConvertFromString(tag) is Color color &&
                color.R == current.R && color.G == current.G && color.B == current.B)
            {
                swatch.IsChecked = true;
                break;
            }
        }
        _syncing = false;
    }

    private void SelectMaterial(WindowBackdrop backdrop)
    {
        _syncing = true;
        MaterialAcrylic.IsChecked = backdrop == WindowBackdrop.Acrylic;
        MaterialMica.IsChecked = backdrop is WindowBackdrop.Mica or WindowBackdrop.MicaAlt;
        MaterialNone.IsChecked = backdrop is WindowBackdrop.None or WindowBackdrop.Blur;
        _syncing = false;
    }

    private void TintSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TintValueText is null)
        {
            return;
        }

        TintValueText.Text = $"{e.NewValue:0}%";
        if (_ready && _window is not null)
        {
            _window.TintOpacity = e.NewValue / 100d;
        }
    }

    private void BlurToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing || _window is null)
        {
            return;
        }

        var on = BlurToggle.IsChecked == true;
        _window.Backdrop = on ? WindowBackdrop.Acrylic : WindowBackdrop.None;
        GetMainWindow()?.ShowToast(on ? "已启用 DWM 实时亚克力" : "已关闭系统材质，使用内置回退表面", "\uE7F4");
    }

    private void AmbientToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing)
        {
            return;
        }

        GetMainWindow()?.SetAmbientEnabled(AmbientToggle.IsChecked == true);
    }

    private void AccentSwatch_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        if (ColorConverter.ConvertFromString(tag) is Color color)
        {
            ThemeManager.SetAccent(color);
        }
    }

    private void ThemeToggle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing)
        {
            return;
        }

        var mode = ThemeToggle.IsChecked == true
            ? FrostThemeMode.Dark
            : FrostThemeMode.Light;
        if (ThemeManager.CurrentTheme != mode)
        {
            ThemeManager.SetTheme(mode);
        }
    }

    private void MaterialRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing || _window is null || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        _window.Backdrop = tag switch
        {
            "Mica" => WindowBackdrop.Mica,
            "None" => WindowBackdrop.None,
            _ => WindowBackdrop.Acrylic,
        };
        _window.RefreshBackdrop();

        var label = _window.EffectiveBackdrop switch
        {
            WindowBackdrop.Mica => "Mica 云母材质已应用",
            WindowBackdrop.MicaAlt => "Mica Alt 材质已应用",
            WindowBackdrop.Acrylic => "Acrylic 亚克力材质已应用",
            WindowBackdrop.Blur => "Blur 模糊材质已应用",
            _ => "系统材质已关闭",
        };
        GetMainWindow()?.ShowToast(label, "\uE790");
    }

    private void RefreshBackdrop_OnClick(object sender, RoutedEventArgs e)
    {
        _window?.RefreshBackdrop();
        GetMainWindow()?.ShowToast("已重新应用窗口材质", "\uE72C");
    }

    private void ExploreButton_OnClick(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.NavigateTo("components");

    private void MaterialButton_OnClick(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.ShowToast("真实亚克力由 DWM 在桌面合成，不是一张高斯模糊图片", "\uE946");

    private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
}
