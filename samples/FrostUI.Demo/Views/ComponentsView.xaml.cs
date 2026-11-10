using System.Windows;
using System.Windows.Controls;

namespace FrostUI.Demo.Views;

public partial class ComponentsView : UserControl
{
    private const string SampleXaml =
        "<controls:GlassCard CornerRadius=\"18\" Padding=\"20,16\">\n" +
        "    <TextBlock Text=\"Frosted glass\" />\n" +
        "</controls:GlassCard>";

    public ComponentsView()
    {
        InitializeComponent();
    }

    private void DemoAction_OnClick(object sender, RoutedEventArgs e)
    {
        var label = (sender as Button)?.Tag as string ?? "Control";
        GetMainWindow()?.ShowToast($"{label} 示例已响应点击", "\uE73E");
    }

    private void CopySample_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(SampleXaml);
            GetMainWindow()?.ShowToast("XAML 示例已复制到剪贴板", "\uE8C8");
        }
        catch
        {
            GetMainWindow()?.ShowToast("剪贴板当前不可用，请稍后再试", "\uE783");
        }
    }

    private void OpenAppearance_OnClick(object sender, RoutedEventArgs e) =>
        GetMainWindow()?.NavigateTo("appearance");

    private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
}
