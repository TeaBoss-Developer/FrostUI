namespace FrostUI.Designer.Scripting;

internal abstract record ScriptNode(int Line);

internal sealed record ScriptProgram(IReadOnlyList<Statement> Statements);

internal abstract record Statement(int Line) : ScriptNode(Line);

internal sealed record VariableDeclarationStatement(
    string Name,
    ScriptType Type,
    bool HasExplicitType,
    Expression? Initializer,
    bool IsGlobal,
    int Line) : Statement(Line);

internal sealed record AssignmentStatement(
    Expression Target,
    Expression Value,
    int Line) : Statement(Line);

internal sealed record ExpressionStatement(
    Expression Expression,
    int Line) : Statement(Line);

internal sealed record IfStatement(
    Expression Condition,
    BlockStatement Then,
    BlockStatement? Else,
    int Line) : Statement(Line);

internal sealed record BlockStatement(
    IReadOnlyList<Statement> Statements,
    int Line) : Statement(Line);

internal sealed record LoopStatement(
    Expression Count,
    BlockStatement Body,
    int Line) : Statement(Line);

internal sealed record ForEachStatement(
    string ItemName,
    Expression Source,
    BlockStatement Body,
    int Line) : Statement(Line);

internal sealed record FunctionParameter(string Name, ScriptType Type);

internal sealed record FunctionDeclarationStatement(
    string Name,
    IReadOnlyList<FunctionParameter> Parameters,
    BlockStatement Body,
    int Line) : Statement(Line);

internal sealed record ReturnStatement(
    Expression? Value,
    int Line) : Statement(Line);

internal sealed record BreakStatement(int Line) : Statement(Line);

internal sealed record ContinueStatement(int Line) : Statement(Line);

internal sealed record DictionaryExpression(
    IReadOnlyList<KeyValuePair<string, Expression>> Items,
    int Line) : Expression(Line);

internal sealed record ListExpression(
    IReadOnlyList<Expression> Items,
    int Line) : Expression(Line);

internal abstract record Expression(int Line) : ScriptNode(Line);

internal sealed record LiteralExpression(
    ScriptValue Value,
    int Line) : Expression(Line);

internal sealed record VariableExpression(
    string Name,
    int Line) : Expression(Line);

internal sealed record MemberExpression(
    Expression Target,
    string Member,
    int Line) : Expression(Line);

internal sealed record IndexExpression(
    Expression Target,
    Expression Index,
    int Line) : Expression(Line);

internal sealed record CallExpression(
    Expression Callee,
    IReadOnlyList<Expression> Arguments,
    int Line) : Expression(Line);

internal sealed record UnaryExpression(
    TokenType Operator,
    Expression Operand,
    int Line) : Expression(Line);

internal sealed record BinaryExpression(
    Expression Left,
    TokenType Operator,
    Expression Right,
    int Line) : Expression(Line);
