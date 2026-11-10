namespace FrostUI.Designer.Models;

public sealed class ControlDefinition
{
    public required ControlKind Kind { get; init; }

    public required string Title { get; init; }

    public required string Icon { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public required string NamePrefix { get; init; }

    public string DefaultText { get; init; } = string.Empty;

    public double DefaultWidth { get; init; } = 120;

    public double DefaultHeight { get; init; } = 36;

    public double DefaultFontSize { get; init; } = 13;

    public double DefaultValue { get; init; } = 50;

    public string DefaultSelectedItem { get; init; } = string.Empty;

    public string DefaultListItems { get; init; } = string.Empty;

    public string DefaultColumns { get; init; } = string.Empty;

    public string DefaultChartType { get; init; } = "Bar";

    public bool DefaultIsOn { get; init; }

    public double DefaultMinimum { get; init; }

    public double DefaultMaximum { get; init; } = 100;

    public double DefaultStep { get; init; } = 1;

    public double DefaultCornerRadius { get; init; } = 16;

    public required IReadOnlyList<string> Events { get; init; }

    public string PrimaryEvent => Events.Count == 0 ? string.Empty : Events[0];
}
