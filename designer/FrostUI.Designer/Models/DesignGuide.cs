namespace FrostUI.Designer.Models;

public enum GuideOrientation
{
    Vertical,
    Horizontal,
}

public sealed class DesignGuide : ObservableObject
{
    private double _position;

    public DesignGuide()
    {
    }

    public DesignGuide(GuideOrientation orientation, double position)
    {
        Orientation = orientation;
        Position = position;
    }

    public GuideOrientation Orientation { get; set; }

    public double Position
    {
        get => _position;
        set => Set(ref _position, Math.Round(value));
    }
}
