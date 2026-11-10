namespace FrostUI.Designer.Scripting;

/// <summary>Line breakpoint with an optional condition or log-only behavior.</summary>
public sealed record BreakpointSpec(int Line, string? Condition = null, bool Log = false);
