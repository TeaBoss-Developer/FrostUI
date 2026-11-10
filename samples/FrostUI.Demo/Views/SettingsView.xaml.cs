using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrostUI.Controls;
using FrostUI.Demo.Services;
using FrostUI.Theme;

namespace FrostUI.Demo.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshEnvironment();

    private void RefreshEnvironment()
    {
        var version = Environment.OSVersion.Version;
        var osName = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        OsText.Text = $"{osName} · {version.Major}.{version.Minor}.{version.Build}";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;

        var wpfVersion = typeof(FrameworkElement).Assembly.GetName().Version;
        var tier = RenderCapability.Tier >> 16;
        WpfText.Text = $"{wpfVersion?.ToString(3) ?? "9.0"} · Tier {tier}";

        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = (int)Math.Round(dpi.DpiScaleX * 100);
        var width = (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY);
        DisplayText.Text = $"{scale}% · {width} × {height}";

        var composed = DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
        DwmText.Text = composed ? "正常 · 实时材质" : "不可用 · 已回退";
        DwmText.Foreground = (Brush)FindResource(composed ? "SuccessBrush" : "TextTertiaryBrush");
    }

    private void RefreshMaterial_OnClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is FrostWindow window)
        {
            window.RefreshBackdrop();
        }

        RefreshEnvironment();
        GetMainWindow()?.ShowToast("已重新检测 DWM 与材质能力", "\uE72C");
    }

    private void CopyDiagnostics_OnClick(object sender, RoutedEventArgs e)
    {
        var version = Environment.OSVersion.Version;
        var osName = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        var dpi = VisualTreeHelper.GetDpi(this);
        var window = Window.GetWindow(this) as FrostWindow;
        var builder = new StringBuilder();
        builder.AppendLine("FrostUI diagnostics");
        builder.AppendLine($"OS            : {osName} {version.Major}.{version.Minor}.{version.Build}");
        builder.AppendLine($"Runtime       : {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"WPF           : {typeof(FrameworkElement).Assembly.GetName().Version}");
        builder.AppendLine($"DPI           : {dpi.DpiScaleX * 100:0}%");
        builder.AppendLine($"Screen        : {SystemParameters.PrimaryScreenWidth:0} x {SystemParameters.PrimaryScreenHeight:0} DIPs");
        builder.AppendLine($"Backdrop      : {window?.EffectiveBackdrop.ToString() ?? "n/a"}");
        builder.AppendLine($"BackdropActive: {window?.IsBackdropActive.ToString() ?? "n/a"}");
        builder.AppendLine($"Tint          : {window?.TintOpacity:P0}");
        builder.AppendLine($"Theme         : {ThemeManager.CurrentTheme}");
        builder.AppendLine($"Accent        : #{ThemeManager.Accent.R:X2}{ThemeManager.Accent.G:X2}{ThemeManager.Accent.B:X2}");
        builder.AppendLine($"FPS           : {RuntimeMetrics.Current.FpsText}");
        builder.AppendLine($"Memory        : {RuntimeMetrics.Current.MemoryText} MB");

        try
        {
            Clipboard.SetText(builder.ToString());
            GetMainWindow()?.ShowToast("诊断信息已复制到剪贴板", "\uE8C8");
        }
        catch
        {
            GetMainWindow()?.ShowToast("剪贴板当前不可用", "\uE783");
        }
    }

    private void BrowseComponents_OnClick(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.NavigateTo("components");

    private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;

    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled(out bool enabled);
}
