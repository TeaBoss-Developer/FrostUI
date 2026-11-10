namespace FrostUI.Designer.Models;

public enum CompletionKind
{
    Variable,
    Control,
    Keyword,
    Namespace,
    Type,
    Method,
    Property,
}

public sealed record CompletionItem(
    string Display,
    string InsertText,
    string Detail,
    CompletionKind Kind,
    int CaretOffsetFromEnd = 0,
    string Icon = "")
{
    public static CompletionItem Create(
        string display,
        string insertText,
        string detail,
        CompletionKind kind,
        int caretOffsetFromEnd = 0) =>
        new(display, insertText, detail, kind, caretOffsetFromEnd, kind switch
        {
            CompletionKind.Variable => "V",
            CompletionKind.Control => "C",
            CompletionKind.Keyword => "K",
            CompletionKind.Namespace => "N",
            CompletionKind.Type => "T",
            CompletionKind.Method => "M",
            _ => "P",
        });
}
