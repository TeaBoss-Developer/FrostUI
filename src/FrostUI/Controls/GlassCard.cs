using System.Windows;
using System.Windows.Controls;

namespace FrostUI.Controls;

/// <summary>
/// A translucent card with a gradient glass fill, a bright rim and an optional hover state.
/// The actual surface is defined by the <c>GlassCardStyle</c> resource.
/// </summary>
public class GlassCard : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(GlassCard),
        new FrameworkPropertyMetadata(new CornerRadius(16)));

    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive),
        typeof(bool),
        typeof(GlassCard),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasShadowProperty = DependencyProperty.Register(
        nameof(HasShadow),
        typeof(bool),
        typeof(GlassCard),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty GlowProperty = DependencyProperty.Register(
        nameof(Glow),
        typeof(bool),
        typeof(GlassCard),
        new FrameworkPropertyMetadata(false));

    /// <summary>Corner radius of the glass surface.</summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Enables the hover highlight and the pointing-hand cursor.</summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Adds a soft ambient shadow below the card.</summary>
    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }

    /// <summary>Adds a faint accent-colored aura to the card.</summary>
    public bool Glow
    {
        get => (bool)GetValue(GlowProperty);
        set => SetValue(GlowProperty, value);
    }
}
