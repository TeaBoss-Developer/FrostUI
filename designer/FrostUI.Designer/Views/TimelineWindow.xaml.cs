using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FrostUI.Controls;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Views;

public partial class TimelineWindow : FrostWindow
{
    private readonly DesignControl _control;

    public event Action? Changed;

    public event Action? PreviewRequested;

    public TimelineWindow(DesignControl control)
    {
        InitializeComponent();
        _control = control;
        Owner = Application.Current.MainWindow;
        TargetText.Text = $"{control.Name} · {control.KindTitle}";
        PropertyBox.ItemsSource = new[] { "Opacity", "X", "Y", "Scale", "Rotation" };
        PropertyBox.SelectedIndex = 0;
        EasingBox.ItemsSource = new[] { "EaseOut", "Linear", "BackOut", "BounceOut" };
        EasingBox.SelectedIndex = 0;
        TimeBox.Text = "300";
        ValueBox.Text = "1";
        KeyframeList.ItemsSource = control.Timeline;
        Refresh();
    }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeBox.Text, out var time) || time < 0)
        {
            return;
        }

        if (!double.TryParse(ValueBox.Text, NumberStyles.Float,
                CultureInfo.CurrentCulture, out var value))
        {
            return;
        }

        _control.Timeline.Add(new AnimationKeyframe
        {
            Property = PropertyBox.SelectedItem as string ?? "Opacity",
            TimeMs = time,
            Value = value,
            Easing = EasingBox.SelectedItem as string ?? "EaseOut",
        });
        Refresh();
        Changed?.Invoke();
    }

    private void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (KeyframeList.SelectedItem is AnimationKeyframe keyframe)
        {
            _control.Timeline.Remove(keyframe);
            Refresh();
            Changed?.Invoke();
        }
    }

    private void Refresh()
    {
        CountText.Text = $"{_control.Timeline.Count} 个关键帧 · 拖动播放头预览尚未实现";
        TrackCanvas.Children.Clear();
        const double left = 16;
        const double width = 680;
        var max = Math.Max(1000, _control.Timeline.Count == 0
            ? 1000
            : _control.Timeline.Max(frame => frame.TimeMs));
        TrackCanvas.Width = left + width + 16;
        for (var tick = 0; tick <= 4; tick++)
        {
            var x = left + width * tick / 4;
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 4,
                Y2 = 62,
                Stroke = (Brush)FindResource("CardBorderBrush"),
                StrokeThickness = 1,
            };
            TrackCanvas.Children.Add(line);
            var label = new TextBlock
            {
                Text = $"{max * tick / 4:0}ms",
                FontSize = 7,
                Foreground = (Brush)FindResource("TextDisabledBrush"),
            };
            Canvas.SetLeft(label, x - 10);
            Canvas.SetTop(label, 64);
            TrackCanvas.Children.Add(label);
        }

        foreach (var keyframe in _control.Timeline)
        {
            var dot = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = (Brush)FindResource("AccentBrush"),
                ToolTip = $"{keyframe.Property} = {keyframe.Value} @ {keyframe.TimeMs}ms",
            };
            Canvas.SetLeft(dot, left + width * keyframe.TimeMs / max - 4.5);
            Canvas.SetTop(dot, 25);
            TrackCanvas.Children.Add(dot);
        }
    }

    private void Preview_OnClick(object sender, RoutedEventArgs e) => PreviewRequested?.Invoke();

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
