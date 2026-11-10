using System.Windows;

namespace FrostUI.Demo;

/// <summary>
/// A full-screen, colorful wallpaper used only while capturing promotional screenshots.
/// It gives the DWM acrylic material something interesting to sample.
/// </summary>
public partial class ShowcaseBackdropWindow : Window
{
    public ShowcaseBackdropWindow()
    {
        InitializeComponent();
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        IsHitTestVisible = false;
        Focusable = false;
        Topmost = true;
    }
}
