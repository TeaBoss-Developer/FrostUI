using System.Runtime.InteropServices;
using System.Windows;
using FrostUI.Demo.Services;
using FrostUI.Theme;

namespace FrostUI.Demo;

public partial class App : Application
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.Initialize();
        ThemeManager.SetTheme(e.Args.Contains("--light", StringComparer.OrdinalIgnoreCase)
            ? FrostThemeMode.Light
            : FrostThemeMode.Dark);
        ThemeManager.SetAccent(ThemeManager.Presets[0].Color);

        var accentArgument = GetArgument(e.Args, "--accent");
        if (accentArgument is not null &&
            System.Windows.Media.ColorConverter.ConvertFromString(accentArgument) is System.Windows.Media.Color accent)
        {
            ThemeManager.SetAccent(accent);
        }

        var screenshot = GetArgument(e.Args, "--screenshot");
        var initialPage = GetArgument(e.Args, "--page") ?? "overview";
        ShowcaseBackdropWindow? showcase = null;

        if (screenshot is not null && !e.Args.Contains("--no-showcase", StringComparer.OrdinalIgnoreCase))
        {
            showcase = new ShowcaseBackdropWindow();
            showcase.Show();
            showcase.Activate();
            await Task.Delay(180);
        }

        var window = new MainWindow();
        MainWindow = window;

        var backdropArgument = GetArgument(e.Args, "--backdrop");
        if (backdropArgument is not null &&
            Enum.TryParse<FrostUI.Interop.WindowBackdrop>(backdropArgument, true, out var backdrop))
        {
            window.Backdrop = backdrop;
        }

        if (screenshot is not null)
        {
            window.Topmost = true;
        }

        window.Show();
        window.Activate();

        var hasOriginalCursor = false;
        POINT originalCursor = default;
        if (screenshot is not null)
        {
            hasOriginalCursor = GetCursorPos(out originalCursor);
            SetCursorPos(GetSystemMetrics(SmCxScreen) - 8, GetSystemMetrics(SmCyScreen) - 8);
        }

        if (showcase is not null)
        {
            // Put the wallpaper directly below the glass window, even though both
            // are topmost while the screenshot is being taken.
            var mainHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var showcaseHandle = new System.Windows.Interop.WindowInteropHelper(showcase).Handle;
            SetWindowPos(
                showcaseHandle,
                mainHandle,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
            window.RefreshBackdrop();
        }

        if (!string.IsNullOrWhiteSpace(initialPage))
        {
            _ = window.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => window.NavigateTo(initialPage)));
        }

        if (screenshot is null)
        {
            return;
        }

        try
        {
            await Task.Delay(1900);
            window.Dispatcher.Invoke(() => window.ApplyBackdropForCapture());
            await Task.Delay(250);
            window.Dispatcher.Invoke(() => ScreenshotService.Capture(window, screenshot));
        }
        finally
        {
            if (hasOriginalCursor)
            {
                SetCursorPos(originalCursor.X, originalCursor.Y);
            }

            window.Close();
            showcase?.Close();
            Shutdown(0);
            Environment.Exit(0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private static string? GetArgument(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
