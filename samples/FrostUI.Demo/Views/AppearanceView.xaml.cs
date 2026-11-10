using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrostUI.Controls;
using FrostUI.Interop;
using FrostUI.Theme;

namespace FrostUI.Demo.Views;

public partial class AppearanceView : UserControl
{
    private FrostWindow? _window;
    private bool _ready;
    private bool _syncing;

    public AppearanceView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ready = false;
        _window = Window.GetWindow(this) as FrostWindow;

        if (_window is not null)
        {
            TintSlider.Value = Math.Round(_window.TintOpacity * 100);
            TintValueText.Text = $"{TintSlider.Value:0}%";
            SelectMaterial(_window.Backdrop);
        }

        ThemeDarkBtn.IsChecked = ThemeManager.IsDark;
        ThemeLightBtn.IsChecked = !ThemeManager.IsDark;
        SyncAccent();
        UpdatePreview();

        ThemeManager.ThemeChanged += OnThemeChanged;
        ThemeManager.AccentChanged += OnAccentChanged;
        _ready = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _ready = false;
        _window = null;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.AccentChanged -= OnAccentChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _syncing = true;
        ThemeDarkBtn.IsChecked = ThemeManager.IsDark;
        ThemeLightBtn.IsChecked = !ThemeManager.IsDark;
        _syncing = false;
        UpdatePreview();
    }

    private void OnAccentChanged(object? sender, EventArgs e)
    {
        SyncAccent();
        UpdatePreview();
    }

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
        MaterialMica.IsChecked = backdrop == WindowBackdrop.Mica;
        MaterialMicaAlt.IsChecked = backdrop == WindowBackdrop.MicaAlt;
        MaterialBlur.IsChecked = backdrop == WindowBackdrop.Blur;
        MaterialNone.IsChecked = backdrop == WindowBackdrop.None;
        _syncing = false;
    }

    private void UpdatePreview()
    {
        var accent = ThemeManager.Accent;
        PreviewThemeText.Text = $"{(ThemeManager.IsDark ? "DARK" : "LIGHT")} · #{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        PreviewAccentText.Text = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        PreviewTintText.Text = $"{TintSlider.Value:0}%";
        PreviewTintLayer.Opacity = 0.04 + (TintSlider.Value / 60d * 0.22);

        if (_window is null)
        {
            return;
        }

        var effective = _window.EffectiveBackdrop;
        var label = effective switch
        {
            WindowBackdrop.Acrylic => "Acrylic",
            WindowBackdrop.Mica => "Mica",
            WindowBackdrop.MicaAlt => "Mica Alt",
            WindowBackdrop.Blur => "Blur",
            _ => "Fallback",
        };
        EffectiveText.Text = label;
        PreviewBackdropText.Text = label;
        EffectiveText.Foreground = (Brush)FindResource(
            _window.IsBackdropActive ? "SuccessBrush" : "TextTertiaryBrush");
        PreviewBackdropText.Foreground = EffectiveText.Foreground;
        MaterialHintText.Text = effective switch
        {
            WindowBackdrop.Acrylic => "DWM 实时合成，支持失焦保持",
            WindowBackdrop.Mica => "Windows 11 壁纸采样，低对比",
            WindowBackdrop.MicaAlt => "Windows 11 壁纸采样，高对比",
            WindowBackdrop.Blur => "兼容性模糊，无颜色混合",
            _ => "系统材质已关闭，使用内置回退表面",
        };
    }

    private void ThemeTile_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing)
        {
            return;
        }

        ThemeManager.SetTheme(ThemeDarkBtn.IsChecked == true
            ? FrostThemeMode.Dark
            : FrostThemeMode.Light);
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

    private void MaterialRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncing || _window is null || sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        _window.Backdrop = tag switch
        {
            "Mica" => WindowBackdrop.Mica,
            "MicaAlt" => WindowBackdrop.MicaAlt,
            "Blur" => WindowBackdrop.Blur,
            "None" => WindowBackdrop.None,
            _ => WindowBackdrop.Acrylic,
        };
        _window.RefreshBackdrop();
        Dispatcher.BeginInvoke(UpdatePreview);
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
            UpdatePreview();
        }
    }

    private void RefreshMaterial_OnClick(object sender, RoutedEventArgs e)
    {
        _window?.RefreshBackdrop();
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePreview();
            GetMainWindow()?.ShowToast("材质与强度已重新应用", "\uE72C");
        });
    }

    private void ResetAppearance_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetTheme(FrostThemeMode.Dark);
        ThemeManager.SetAccent(ThemeManager.Presets[0].Color);
        if (_window is not null)
        {
            _window.Backdrop = WindowBackdrop.Acrylic;
            _window.TintOpacity = 0.28;
            _window.RefreshBackdrop();
            TintSlider.Value = 28;
        }

        UpdatePreview();
        GetMainWindow()?.ShowToast("外观已恢复默认设置", "\uE777");
    }

    private void GoComponents_OnClick(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.NavigateTo("components");

    private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
}
