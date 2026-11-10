namespace FrostUI.Designer.Models;

public enum ScriptDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record ScriptDiagnostic(
    int Line,
    int Column,
    string Message,
    ScriptDiagnosticSeverity Severity)
{
    public string Display => $"第 {Line} 行 · {Message}";

    public string Icon => Severity == ScriptDiagnosticSeverity.Error ? "✕" : "!";
}
