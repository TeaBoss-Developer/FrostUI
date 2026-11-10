namespace FrostUI.Designer.Models;

public sealed class AnimationKeyframe
{
    public string Property { get; set; } = "Opacity";

    public int TimeMs { get; set; }

    public double Value { get; set; }

    public string Easing { get; set; } = "EaseOut";

    public AnimationKeyframe Clone() => new()
    {
        Property = Property,
        TimeMs = TimeMs,
        Value = Value,
        Easing = Easing,
    };
}
