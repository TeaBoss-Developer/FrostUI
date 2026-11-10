using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using FrostUI.Designer.Services;
using FrostUI.Theme;

namespace FrostUI.Designer;

public partial class App : Application
{
    public static bool IsAutomationMode { get; set; }

    private static readonly string ErrorLog =
        Path.Combine(AppContext.BaseDirectory, "designer-error.log");

    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.ExceptionObject as Exception ?? new Exception("Unknown startup error"));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize();
        ThemeManager.SetTheme(e.Args.Contains("--light", StringComparer.OrdinalIgnoreCase)
            ? FrostThemeMode.Light
            : FrostThemeMode.Dark);

        var selfTestPath = GetArgument(e.Args, "--self-test");
        if (selfTestPath is not null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = await ScriptSelfTest.RunAsync(selfTestPath);
            Shutdown(exitCode);
            return;
        }

        var exportSample = GetArgument(e.Args, "--export-sample");
        if (exportSample is not null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DesignSerializer.Save(Models.DesignDocument.CreateSample(), exportSample);
            Shutdown(0);
            return;
        }

        var exportRelease = GetArgument(e.Args, "--export-release");
        if (exportRelease is not null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var designPath = GetArgument(e.Args, "--design");
                var document = designPath is null
                    ? Models.DesignDocument.CreateSample()
                    : DesignSerializer.Load(designPath);
                var options = new ExportOptions
                {
                    DestinationDirectory = exportRelease,
                    SelfContained = e.Args.Contains("--self-contained", StringComparer.OrdinalIgnoreCase),
                    SingleFile = e.Args.Contains("--single-file", StringComparer.OrdinalIgnoreCase),
                    ReadyToRun = e.Args.Contains("--ready-to-run", StringComparer.OrdinalIgnoreCase),
                    PublishMode = e.Args.Contains("--template-single", StringComparer.OrdinalIgnoreCase)
                        ? "TemplateSingle"
                        : e.Args.Contains("--template-folder", StringComparer.OrdinalIgnoreCase)
                            ? "TemplateFolder"
                            : "Dev",
                    OpenFolderAfterExport = false,
                };
                await ProjectExporter.ExportAsync(document, options);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Log(ex);
                Shutdown(1);
            }

            return;
        }

        try
        {
            var screenshot = GetArgument(e.Args, "--screenshot");
            IsAutomationMode = screenshot is not null;
            var window = new MainWindow();
            MainWindow = window;
            if (screenshot is not null)
            {
                window.Topmost = true;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            window.Show();
            window.Activate();

            var startupDesign = GetArgument(e.Args, "--design");
            if (!string.IsNullOrWhiteSpace(startupDesign))
            {
                window.Dispatcher.Invoke(() => window.LoadDesignFromPath(startupDesign));
            }

            if (e.Args.Contains("--theme-color", StringComparer.OrdinalIgnoreCase))
            {
                window.Dispatcher.Invoke(() => window.OpenThemeColorWindow());
            }

            var selection = GetArgument(e.Args, "--select");
            if (!string.IsNullOrWhiteSpace(selection))
            {
                window.Dispatcher.Invoke(() => window.SelectControlByName(selection));
            }

            if (screenshot is not null)
            {
                if (e.Args.Contains("--layers", StringComparer.OrdinalIgnoreCase))
                {
                    window.Dispatcher.Invoke(() => window.OpenLayersForScreenshot());
                }

                var hasCursor = GetCursorPos(out var cursor);
                SetCursorPos(8, 8);
                SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                await Task.Delay(2000);

                if (e.Args.Contains("--preview", StringComparer.OrdinalIgnoreCase))
                {
                    var preview = window.Dispatcher.Invoke(() => window.OpenPreview());
                    if (preview is not null)
                    {
                        await Task.Delay(1500);
                        preview.Dispatcher.Invoke(() => preview.RefreshBackdrop());
                        await Task.Delay(200);
                        preview.Dispatcher.Invoke(() => ScreenshotService.Capture(preview, screenshot));
                        preview.Close();
                    }
                }
                else
                {
                    window.Dispatcher.Invoke(() => window.RefreshBackdrop());
                    await Task.Delay(200);
                    window.Dispatcher.Invoke(() => ScreenshotService.Capture(window, screenshot));
                }

                if (hasCursor)
                {
                    SetCursorPos(cursor.X, cursor.Y);
                }

                window.Close();
                Shutdown(0);
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Log(ex);
            Shutdown(1);
        }
    }

    private static void Log(Exception exception)
    {
        try
        {
            File.AppendAllText(
                ErrorLog,
                $"[{DateTime.Now:HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // ignored
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

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
