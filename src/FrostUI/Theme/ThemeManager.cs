using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FrostUI.Theme;

/// <summary>A named accent color offered by <see cref="ThemeManager"/>.</summary>
public sealed record AccentPreset(string Name, Color Color);

/// <summary>
/// Applies and switches the FrostUI color themes. The manager replaces the merged
/// <c>Colors.Dark.xaml</c> / <c>Colors.Light.xaml</c> dictionary and regenerates
/// the accent brush family at runtime.
/// </summary>
public static class ThemeManager
{
    private static readonly Uri DarkColorsUri =
        new("pack://application:,,,/FrostUI;component/Themes/Colors.Dark.xaml", UriKind.Absolute);

    private static readonly Uri LightColorsUri =
        new("pack://application:,,,/FrostUI;component/Themes/Colors.Light.xaml", UriKind.Absolute);

    private static FrostThemeMode _current = FrostThemeMode.Dark;
    private static Color _accent = Color.FromRgb(0x84, 0xAE, 0xFF);
    private static bool _initialized;

    /// <summary>Raised after the active color theme has been switched.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>Raised after the accent brush family has been regenerated.</summary>
    public static event EventHandler? AccentChanged;

    /// <summary>The active theme.</summary>
    public static FrostThemeMode CurrentTheme => _current;

    /// <summary>True when the dark theme is active.</summary>
    public static bool IsDark => _current == FrostThemeMode.Dark;

    /// <summary>The current accent color.</summary>
    public static Color Accent => _accent;

    /// <summary>A small curated accent palette used by the demo.</summary>
    public static IReadOnlyList<AccentPreset> Presets { get; } =
    [
        new("冰川蓝", Color.FromRgb(0x84, 0xAE, 0xFF)),
        new("薄荷青", Color.FromRgb(0x63, 0xE6, 0xBE)),
        new("暮光紫", Color.FromRgb(0xB7, 0x9C, 0xFF)),
        new("霞光粉", Color.FromRgb(0xFF, 0x9B, 0xC7)),
        new("琥珀金", Color.FromRgb(0xFF, 0xC4, 0x6B)),
    ];

    /// <summary>Reads the currently merged color dictionary and prepares the accent family.</summary>
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (Application.Current?.TryFindResource("AccentColor") is Color color)
        {
            _accent = color;
        }

        ApplyAccentResources(_accent);
    }

    /// <summary>Switches between the dark and light color dictionaries.</summary>
    public static void SetTheme(FrostThemeMode mode)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetTheme(mode));
            return;
        }

        var changed = _current != mode;
        _current = mode;
        ReplaceColorDictionary(mode);

        // The merged dictionaries contain their own accent defaults. The root-level
        // overrides generated here take precedence, so re-apply them after the swap.
        ApplyAccentResources(_accent);
        if (changed)
        {
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Toggles between dark and light.</summary>
    public static void ToggleTheme() =>
        SetTheme(_current == FrostThemeMode.Dark ? FrostThemeMode.Light : FrostThemeMode.Dark);

    /// <summary>Replaces every accent brush with one generated from <paramref name="color"/>.</summary>
    public static void SetAccent(Color color)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetAccent(color));
            return;
        }

        if (_accent == color && _initialized)
        {
            return;
        }

        _accent = color;
        ApplyAccentResources(color);
        AccentChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ReplaceColorDictionary(FrostThemeMode mode)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var replacement = new ResourceDictionary
        {
            Source = mode == FrostThemeMode.Dark ? DarkColorsUri : LightColorsUri,
        };

        if (TryReplace(app.Resources, replacement))
        {
            return;
        }

        app.Resources.MergedDictionaries.Insert(0, replacement);
    }

    private static bool TryReplace(ResourceDictionary dictionary, ResourceDictionary replacement)
    {
        foreach (var merged in dictionary.MergedDictionaries)
        {
            var source = merged.Source?.OriginalString;
            if (source is not null &&
                (source.Contains("Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                 source.Contains("Colors.Light.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                var index = dictionary.MergedDictionaries.IndexOf(merged);
                dictionary.MergedDictionaries[index] = replacement;
                return true;
            }
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryReplace(merged, replacement))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyAccentResources(Color accent)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var deep = ColorUtil.Darken(accent, 0.18);
        var light = ColorUtil.Lighten(accent, 0.18);
        var secondary = ColorUtil.Blend(accent, Color.FromRgb(0xC0, 0x84, 0xFC), 0.52);
        var tertiary = ColorUtil.Blend(accent, Color.FromRgb(0x6F, 0xE3, 0xD2), 0.55);
        var contrast = ColorUtil.Luminance(accent) > 0.54
            ? Color.FromRgb(0x07, 0x10, 0x1F)
            : Colors.White;

        // Light surfaces need a deeper accent for text, glyphs and thin strokes.
        var textAccent = _current == FrostThemeMode.Light ? ColorUtil.Darken(accent, 0.30) : accent;

        app.Resources["AccentColor"] = accent;
        app.Resources["AccentBrush"] = Frozen(new SolidColorBrush(textAccent));
        app.Resources["AccentDeepBrush"] = Frozen(new SolidColorBrush(
            _current == FrostThemeMode.Light ? ColorUtil.Darken(accent, 0.34) : deep));
        app.Resources["AccentSoftBrush"] = Frozen(new SolidColorBrush(ColorUtil.WithAlpha(accent, 0x2E)));
        app.Resources["AccentGlowBrush"] = Frozen(new SolidColorBrush(ColorUtil.WithAlpha(accent, 0x58)));
        app.Resources["AccentTrackBrush"] = Frozen(new SolidColorBrush(ColorUtil.WithAlpha(accent, 0x33)));
        app.Resources["AccentBorderBrush"] = Frozen(new SolidColorBrush(ColorUtil.WithAlpha(accent, 0x55)));
        app.Resources["AccentContrastBrush"] = Frozen(new SolidColorBrush(contrast));
        app.Resources["SelectionBrush"] = Frozen(new SolidColorBrush(ColorUtil.WithAlpha(accent, 0x33)));

        app.Resources["AccentGradientBrush"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(light, 0),
                new GradientStop(secondary, 1),
            },
        });

        app.Resources["BrandGradientBrush"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(ColorUtil.Lighten(accent, 0.36), 0),
                new GradientStop(secondary, 0.56),
                new GradientStop(tertiary, 1),
            },
        });

        app.Resources["AvatarGradientBrush"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(accent, 0),
                new GradientStop(secondary, 1),
            },
        });

        app.Resources["OrbBrush"] = Frozen(new RadialGradientBrush
        {
            Center = new Point(0.35, 0.3),
            GradientOrigin = new Point(0.35, 0.3),
            RadiusX = 0.7,
            RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(ColorUtil.WithAlpha(ColorUtil.Lighten(accent, 0.55), 0xDC), 0),
                new GradientStop(ColorUtil.WithAlpha(accent, 0xA8), 0.36),
                new GradientStop(ColorUtil.WithAlpha(deep, 0x4D), 0.7),
                new GradientStop(ColorUtil.WithAlpha(deep, 0x00), 1),
            },
        });

        app.Resources["SoftGlowEffect"] = new DropShadowEffect
        {
            BlurRadius = 26,
            ShadowDepth = 0,
            Direction = 0,
            Color = accent,
            Opacity = 0.45,
            RenderingBias = RenderingBias.Quality,
        };
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
