namespace FrostUI.Designer.Models;

public sealed class DesignToken
{
    public string Name { get; set; } = string.Empty;

    public string FontFamily { get; set; } = string.Empty;

    public double FontSize { get; set; } = 13;

    public bool IsBold { get; set; }

    public double CornerRadius { get; set; }

    public double Opacity { get; set; } = 1;

    public DesignToken Clone() => new()
    {
        Name = Name,
        FontFamily = FontFamily,
        FontSize = FontSize,
        IsBold = IsBold,
        CornerRadius = CornerRadius,
        Opacity = Opacity,
    };
}
