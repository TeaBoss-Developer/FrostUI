using System.Windows;
using System.Windows.Media;

namespace FrostUI.Designer.Models;

public static class ThemeColorHelper
{
    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (ColorConverter.ConvertFromString(value.Trim()) is Color parsed)
            {
                color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public static string ToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static void ApplyLocalAccent(FrameworkElement element, Color accent)
    {
        var resources = element.Resources;
        var deep = Darken(accent, 0.18);
        var light = Lighten(accent, 0.18);
        var secondary = Blend(accent, Color.FromRgb(0xC0, 0x84, 0xFC), 0.52);
        var contrast = Luminance(accent) > 0.54
            ? Color.FromRgb(0x07, 0x10, 0x1F)
            : Colors.White;

        resources["AccentColor"] = accent;
        resources["AccentBrush"] = new SolidColorBrush(accent);
        resources["AccentDeepBrush"] = new SolidColorBrush(deep);
        resources["AccentSoftBrush"] = new SolidColorBrush(WithAlpha(accent, 0x2E));
        resources["AccentGlowBrush"] = new SolidColorBrush(WithAlpha(accent, 0x58));
        resources["AccentTrackBrush"] = new SolidColorBrush(WithAlpha(accent, 0x33));
        resources["AccentBorderBrush"] = new SolidColorBrush(WithAlpha(accent, 0x55));
        resources["AccentContrastBrush"] = new SolidColorBrush(contrast);
        resources["SelectionBrush"] = new SolidColorBrush(WithAlpha(accent, 0x33));
        resources["AccentGradientBrush"] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(light, 0),
                new(secondary, 1),
            },
            new Point(0, 0),
            new Point(1, 1));
    }

    public static Color Lighten(Color color, double amount) =>
        Color.FromArgb(
            color.A,
            (byte)Math.Round(color.R + (255 - color.R) * amount),
            (byte)Math.Round(color.G + (255 - color.G) * amount),
            (byte)Math.Round(color.B + (255 - color.B) * amount));

    public static Color Darken(Color color, double amount) =>
        Color.FromArgb(
            color.A,
            (byte)Math.Round(color.R * (1 - amount)),
            (byte)Math.Round(color.G * (1 - amount)),
            (byte)Math.Round(color.B * (1 - amount)));

    public static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * amount),
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }

    public static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double Luminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
}
