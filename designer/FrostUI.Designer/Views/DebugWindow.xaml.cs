using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FrostUI.Controls;
using FrostUI.Designer.Models;
using FrostUI.Designer.Scripting;

namespace FrostUI.Designer.Views;

public partial class DebugWindow : FrostWindow
{
    private readonly DesignDocument _document;
    private readonly string _code;
    private readonly string? _controlName;
    private readonly string _eventName;
    private readonly IReadOnlyCollection<int> _breakpoints;
    private ScriptRuntime? _runtime;
    private ScriptDebugSession? _session;
    private bool _started;
    private bool _closed;

    public DebugWindow(
        DesignDocument document,
        string code,
        string? controlName,
        string eventName,
        IReadOnlyCollection<int> breakpoints)
    {
        InitializeComponent();
        _document = document;
        _code = code;
        _controlName = controlName;
        _eventName = eventName;
        _breakpoints = breakpoints;
        DebugTargetText.Text = controlName is null
            ? $"窗口 · {eventName}"
            : $"{controlName} · {eventName}";
        DebugBreakpointText.Text = breakpoints.Count == 0
            ? "无"
            : string.Join(", ", breakpoints.OrderBy(x => x));
        DebugBreakpointSpecBox.Text = string.Join(
            Environment.NewLine,
            breakpoints.OrderBy(x => x));
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        DebugCodeBox.Text = _code;
        _runtime = new ScriptRuntime(_document);
        _session = new ScriptDebugSession();
        _session.SetBreakpoints(_breakpoints.Select(line => new BreakpointSpec(line)));
        _session.OnLog = (snapshot, spec) =>
        {
            if (!_closed)
            {
                DebugLogBox.AppendText(
                    $"[L{snapshot.Line}] {string.Join(", ", snapshot.Variables.Select(pair => $"{pair.Key}={pair.Value}"))}{Environment.NewLine}");
                DebugLogBox.ScrollToEnd();
            }
        };
        _session.OnPause = OnPauseAsync;
        _runtime.DebugSession = _session;
        DebugStatusText.Text = "运行中";

        try
        {
            var control = string.IsNullOrWhiteSpace(_controlName)
                ? null
                : _document.FindControl(_controlName);
            var success = await _runtime.RunAsync(control, _eventName, _code);
            if (!_closed)
            {
                DebugStatusText.Text = success ? "执行完成" : "执行结束（有错误）";
                DebugContinueButton.IsEnabled = false;
                DebugStepButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                DebugStatusText.Text = "调试失败";
                DebugVariablesBox.Text = ex.Message;
            }
        }
    }

    private void ApplyBreakpoints_OnClick(object sender, RoutedEventArgs e)
    {
        var specs = new List<BreakpointSpec>();
        foreach (var raw in DebugBreakpointSpecBox.Text.Split(
                     '\n',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var lineText = raw;
            string? condition = null;
            var log = false;
            var colon = raw.IndexOf(':');
            if (colon >= 0)
            {
                lineText = raw[..colon].Trim();
                condition = raw[(colon + 1)..].Trim();
                if (condition.Equals("log", StringComparison.OrdinalIgnoreCase))
                {
                    log = true;
                    condition = null;
                }
            }

            if (int.TryParse(lineText, out var line) && line > 0)
            {
                specs.Add(new BreakpointSpec(line, condition, log));
            }
        }

        _session?.SetBreakpoints(specs);
        DebugBreakpointText.Text = specs.Count == 0
            ? "无"
            : string.Join(", ", specs.Select(spec =>
                $"{spec.Line}{(spec.Log ? ":log" : spec.Condition is null ? "" : ":" + spec.Condition)}"));
        DebugStatusText.Text = $"断点已更新：{specs.Count} 个";
    }

    private Task OnPauseAsync(DebugSnapshot snapshot)
    {
        if (_closed)
        {
            return Task.CompletedTask;
        }

        Dispatcher.Invoke(() =>
        {
            DebugStatusText.Text = $"已暂停 · 第 {snapshot.Line} 行";
            DebugLineText.Text = $"LINE {snapshot.Line}";
            DebugVariablesBox.Text = string.Join(
                Environment.NewLine,
                snapshot.Variables
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key} = {pair.Value}"));
            DebugCallStackBox.Text = snapshot.CallStack.Count == 0
                ? "(顶层脚本)"
                : string.Join(Environment.NewLine, snapshot.CallStack.Reverse());
            SelectLine(snapshot.Line);
            DebugContinueButton.IsEnabled = true;
            DebugStepButton.IsEnabled = true;
        });
        return Task.CompletedTask;
    }

    private void SelectLine(int line)
    {
        var lines = _code.Replace("\r\n", "\n").Split('\n');
        var index = 0;
        for (var i = 0; i < Math.Min(lines.Length, line - 1); i++)
        {
            index += lines[i].Length + 1;
        }

        var length = Math.Min(lines[Math.Clamp(line - 1, 0, lines.Length - 1)].Length, _code.Length - index);
        DebugCodeBox.Select(index, Math.Max(1, length));
        DebugCodeBox.ScrollToLine(Math.Clamp(line - 1, 0, Math.Max(0, lines.Length - 1)));
    }

    private void Continue_OnClick(object sender, RoutedEventArgs e)
    {
        DebugContinueButton.IsEnabled = false;
        DebugStepButton.IsEnabled = false;
        _session?.Continue();
    }

    private void Step_OnClick(object sender, RoutedEventArgs e)
    {
        DebugContinueButton.IsEnabled = false;
        DebugStepButton.IsEnabled = false;
        _session?.Step();
    }

    private void Stop_OnClick(object sender, RoutedEventArgs e)
    {
        _session?.Stop();
        DebugStatusText.Text = "已停止";
        DebugContinueButton.IsEnabled = false;
        DebugStepButton.IsEnabled = false;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _closed = true;
        _session?.Stop();
    }
}
