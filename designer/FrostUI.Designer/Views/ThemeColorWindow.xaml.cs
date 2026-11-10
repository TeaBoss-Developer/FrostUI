using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FrostUI.Controls;
using FrostUI.Designer.Models;
using FrostUI.Theme;

namespace FrostUI.Designer.Views;

public partial class ThemeColorWindow : FrostWindow
{
    private readonly bool _livePreview;
    private readonly Color _original;
    private readonly IReadOnlyList<string> _customPalette;
    private Color _color;
    private bool _suppress;

    public event Action<string>? PresetSaved;

    public ThemeColorWindow(
        Color initial,
        bool livePreview,
        IEnumerable<string>? customPalette = null)
    {
        _livePreview = livePreview;
        _original = initial;
        _customPalette = customPalette?.ToArray() ?? [];
        _color = initial;
        InitializeComponent();

        Title = livePreview ? "主题色" : "控件主题色";
        HeaderText.Text = Title;
        HintText.Text = livePreview
            ? "修改会立即应用到设计器；取消会恢复原主题色。"
            : "只应用到当前选中的控件，不影响全局主题。";

        BuildPresets();
        _suppress = true;
        RedSlider.Value = initial.R;
        GreenSlider.Value = initial.G;
        BlueSlider.Value = initial.B;
        HexBox.Text = ThemeColorHelper.ToHex(initial);
        _suppress = false;
        UpdatePreview();

        if (livePreview)
        {
            ThemeManager.SetAccent(initial);
        }

        Closing += (_, _) =>
        {
            if (_livePreview && !_accepted)
            {
                ThemeManager.SetAccent(_original);
            }
        };
    }

    private bool _accepted;

    public Color SelectedColor => _color;

    private void BuildPresets()
    {
        foreach (var preset in ThemeManager.Presets)
        {
            AddPresetSwatch(preset.Color, preset.Name);
        }

        foreach (var value in _customPalette)
        {
            if (ThemeColorHelper.TryParse(value, out var color))
            {
                AddPresetSwatch(color, "自定义");
            }
        }
    }

    private void AddPresetSwatch(Color color, string name)
    {
        var swatch = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 7, 0),
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(ThemeColorHelper.WithAlpha(color, 0x66)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = $"{name}  {ThemeColorHelper.ToHex(color)}",
        };
        swatch.MouseLeftButtonUp += (_, _) => SelectColor(color);
        PresetPanel.Children.Add(swatch);
    }

    private void SavePreset_OnClick(object sender, RoutedEventArgs e)
    {
        var hex = ThemeColorHelper.ToHex(_color);
        PresetSaved?.Invoke(hex);
        AddPresetSwatch(_color, "自定义");
    }

    private void SelectColor(Color color)
    {
        _suppress = true;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        _suppress = false;
        UpdatePreview();
        if (_livePreview)
        {
            ThemeManager.SetAccent(color);
        }
    }

    private void RgbSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress)
        {
            return;
        }

        var color = Color.FromRgb(
            (byte)Math.Round(RedSlider.Value),
            (byte)Math.Round(GreenSlider.Value),
            (byte)Math.Round(BlueSlider.Value));
        _suppress = true;
        HexBox.Text = ThemeColorHelper.ToHex(color);
        _suppress = false;
        UpdatePreview();
        if (_livePreview)
        {
            ThemeManager.SetAccent(color);
        }
    }

    private void HexBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        if (!ThemeColorHelper.TryParse(HexBox.Text, out var color))
        {
            return;
        }

        _suppress = true;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        _suppress = false;
        UpdatePreview();
        if (_livePreview)
        {
            ThemeManager.SetAccent(color);
        }
    }

    private void UpdatePreview()
    {
        if (RedSlider is null || GreenSlider is null || BlueSlider is null)
        {
            return;
        }

        _color = Color.FromRgb(
            (byte)Math.Round(RedSlider.Value),
            (byte)Math.Round(GreenSlider.Value),
            (byte)Math.Round(BlueSlider.Value));
        var brush = new SolidColorBrush(_color);
        PreviewSwatch.Background = brush;
        HexPreview.Background = brush;
        PreviewCard.BorderBrush = new SolidColorBrush(ThemeColorHelper.WithAlpha(_color, 0x66));
        RedValue.Text = _color.R.ToString();
        GreenValue.Text = _color.G.ToString();
        BlueValue.Text = _color.B.ToString();

        if (!_suppress && !HexBox.IsKeyboardFocusWithin)
        {
            _suppress = true;
            HexBox.Text = ThemeColorHelper.ToHex(_color);
            _suppress = false;
        }
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e)
    {
        _accepted = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
