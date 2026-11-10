using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using FrostUI.Controls;

namespace FrostUI.Demo.Services;

/// <summary>Small live telemetry object shared by the dashboard and the sidebar.</summary>
public sealed class RuntimeMetrics : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer;
    private FrostWindow? _window;
    private int _frameCount;
    private DateTime _lastSample = DateTime.UtcNow;
    private double _fps = 60;
    private double _frameTime = 16.7;
    private double _memoryMb;
    private string _backdrop = "Acrylic";
    private string _backdropDetail = "DWM 实时合成";
    private bool _isBackdropActive;

    private RuntimeMetrics()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(800),
        };
        _timer.Tick += OnTick;
    }

    public static RuntimeMetrics Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Fps
    {
        get => _fps;
        private set => Set(ref _fps, value);
    }

    public double FrameTime
    {
        get => _frameTime;
        private set => Set(ref _frameTime, value);
    }

    public double MemoryMb
    {
        get => _memoryMb;
        private set => Set(ref _memoryMb, value);
    }

    public string Backdrop
    {
        get => _backdrop;
        private set => Set(ref _backdrop, value);
    }

    public string BackdropDetail
    {
        get => _backdropDetail;
        private set => Set(ref _backdropDetail, value);
    }

    public bool IsBackdropActive
    {
        get => _isBackdropActive;
        private set => Set(ref _isBackdropActive, value);
    }

    public string FpsText => $"{Fps:0}";

    public string MemoryText => $"{MemoryMb:0}";

    public string FrameTimeText => $"{FrameTime:0.0} ms";

    public void Start(FrostWindow window)
    {
        _window = window;
        _lastSample = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRendering;
        _timer.Start();
        RefreshWindow();
    }

    public void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        _timer.Stop();
    }

    private void OnRendering(object? sender, EventArgs e) => _frameCount++;

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastSample).TotalSeconds;
        if (elapsed > 0.2)
        {
            var fps = _frameCount / elapsed;
            _frameCount = 0;
            _lastSample = now;
            Fps = Math.Clamp(fps, 1, 240);
            FrameTime = 1000d / Fps;
            OnPropertyChanged(nameof(FpsText));
            OnPropertyChanged(nameof(FrameTimeText));
        }

        MemoryMb = Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d;
        OnPropertyChanged(nameof(MemoryText));
        RefreshWindow();
    }

    private void RefreshWindow()
    {
        if (_window is null)
        {
            return;
        }

        IsBackdropActive = _window.IsBackdropActive;
        Backdrop = _window.EffectiveBackdrop switch
        {
            Interop.WindowBackdrop.Acrylic => "Acrylic",
            Interop.WindowBackdrop.Mica => "Mica",
            Interop.WindowBackdrop.MicaAlt => "Mica Alt",
            Interop.WindowBackdrop.Blur => "Blur",
            _ => "Fallback",
        };

        BackdropDetail = _window.IsBackdropActive ? "DWM 实时合成" : "内置回退材质";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
