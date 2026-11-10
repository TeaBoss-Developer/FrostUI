namespace FrostUI.Designer.Scripting;

public sealed class ScriptException : Exception
{
    public ScriptException(string message, int line, int column)
        : base($"第 {line} 行：{message}")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}
