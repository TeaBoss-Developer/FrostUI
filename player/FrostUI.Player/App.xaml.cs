using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FrostUI.Controls;
using FrostUI.Designer.Models;
using FrostUI.Designer.Services;
using FrostUI.Designer.Scripting;
using FrostUI.Designer.Views;
using FrostUI.Theme;

namespace FrostUI.Player;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize();
        ThemeManager.SetTheme(FrostThemeMode.Dark);

        var designPath = e.Args.Length > 0
            ? Path.GetFullPath(e.Args[0])
            : Path.Combine(AppContext.BaseDirectory, "design.frostui.json");

        try
        {
            if (!File.Exists(designPath))
            {
                FrostMessageBox.Show(
                    null,
                    $"找不到设计文件：{designPath}\n\n请把 design.frostui.json 放在 FrostUI.Player.exe 旁边，或把文件路径作为启动参数。",
                    "FrostUI Player",
                    FrostMessageBoxButtons.OK,
                    danger: true);
                Shutdown(1);
                return;
            }

            var document = DesignSerializer.Load(designPath);
            foreach (var image in document.Controls.Where(
                         control => control.Kind == ControlKind.Image &&
                                    !string.IsNullOrWhiteSpace(control.ImagePath)))
            {
                if (!Path.IsPathRooted(image.ImagePath))
                {
                    image.ImagePath = Path.GetFullPath(
                        Path.Combine(AppContext.BaseDirectory, image.ImagePath));
                }
            }

            var logPath = Path.Combine(AppContext.BaseDirectory, "player.log");
            void Log(string message, ScriptLogLevel level)
            {
                try
                {
                    File.AppendAllText(
                        logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
                }
                catch
                {
                    // Logging must never take down the player.
                }
            }

            var window = new PreviewWindow(document, Log, standalone: true);
            var iconPath = Directory
                .GetFiles(AppContext.BaseDirectory, "app-icon.*")
                .FirstOrDefault();
            if (iconPath is not null)
            {
                window.Icon = BitmapFrame.Create(
                    new Uri(iconPath),
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
            }

            MainWindow = window;
            window.Show();
            Log($"已加载设计：{designPath}", ScriptLogLevel.Success);
        }
        catch (Exception ex)
        {
            var errorPath = Path.Combine(AppContext.BaseDirectory, "player-error.log");
            try
            {
                File.WriteAllText(errorPath, ex.ToString());
            }
            catch
            {
                // ignored
            }

            FrostMessageBox.Show(
                null,
                $"无法启动 FrostUI 应用：\n{ex.Message}\n\n详细信息已写入 player-error.log。",
                "FrostUI Player",
                FrostMessageBoxButtons.OK,
                danger: true);
            Shutdown(1);
        }
    }
}
