using System.IO;
using System.Windows;
using FrostUI.Controls;
using FrostUI.Designer.Models;
using FrostUI.Designer.Services;
using Microsoft.Win32;

namespace FrostUI.Designer.Views;

public partial class ExportDialog : FrostWindow
{
    private readonly DesignDocument _document;
    private bool _busy;

    public ExportDialog(DesignDocument document)
    {
        InitializeComponent();
        _document = document;
        ProjectSummaryText.Text =
            $"{document.Name} · v{document.Version} · {document.Controls.Count} 个控件 · " +
            $"{document.WindowWidth:0} × {document.WindowHeight:0}";

        var safeName = MakeSafeName(document.Name);
        var runDirectory = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(runDirectory) || !Directory.Exists(runDirectory))
        {
            runDirectory = AppContext.BaseDirectory;
        }

        DestinationBox.Text = Path.Combine(runDirectory, $"{safeName}-release");
        Closing += (_, e) =>
        {
            if (_busy)
            {
                e.Cancel = true;
            }
        };
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 release 导出文件夹",
            InitialDirectory = Directory.Exists(DestinationBox.Text)
                ? DestinationBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        };

        if (dialog.ShowDialog(this) == true)
        {
            DestinationBox.Text = dialog.FolderName;
        }
    }

    private async void Export_OnClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var destination = DestinationBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destination))
        {
            AppendLog("请先选择导出文件夹。");
            return;
        }

        var publishMode = PublishModeBox.SelectedIndex switch
        {
            0 => "TemplateSingle",
            1 => "TemplateFolder",
            2 => "Dev",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(publishMode))
        {
            AppendLog("请先选择发布方式。");
            return;
        }

        _busy = true;
        SetBusy(true);
        LogBox.Clear();
        AppendLog($"开始导出：{_document.Name}");
        AppendLog(publishMode switch
        {
            "TemplateSingle" => "模式：内置模板 · 单文件",
            "TemplateFolder" => "模式：内置模板 · 文件夹",
            _ => SelfContainedCheck.IsChecked == true
                ? "模式：开发者编译 · 自包含"
                : "模式：开发者编译 · 框架依赖",
        });

        try
        {
            var options = new ExportOptions
            {
                DestinationDirectory = destination,
                PublishMode = publishMode,
                SelfContained = SelfContainedCheck.IsChecked == true,
                SingleFile = SingleFileCheck.IsChecked == true,
                ReadyToRun = ReadyToRunCheck.IsChecked == true,
                OpenFolderAfterExport = OpenFolderCheck.IsChecked == true,
            };
            var progress = new Progress<string>(AppendLog);
            var result = await ProjectExporter.ExportAsync(_document, options, progress);
            StatusText.Text = "完成";
            AppendLog($"完成：{result}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "失败";
            AppendLog($"导出失败：{ex.Message}");
        }
        finally
        {
            _busy = false;
            SetBusy(false);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_busy)
        {
            Close();
        }
    }

    private void PublishModeBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var isDeveloper = PublishModeBox.SelectedIndex == 2;
        SelfContainedCheck.IsEnabled = isDeveloper;
        SingleFileCheck.IsEnabled = isDeveloper;
        ReadyToRunCheck.IsEnabled = isDeveloper;
    }

    private void SetBusy(bool busy)
    {
        ExportButton.IsEnabled = !busy;
        PublishModeBox.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
        ExportButton.Content = busy ? "正在导出…" : "开始导出";
        if (busy)
        {
            StatusText.Text = "发布中";
        }
    }

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private static string MakeSafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "FrostUI";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var safe = new string(chars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(safe) ? "FrostUI" : safe;
    }
}
