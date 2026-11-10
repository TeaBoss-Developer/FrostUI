namespace FrostUI.Designer.Scripting;

internal sealed class ScriptParser
{
    private readonly List<Token> _tokens;
    private int _index;

    private ScriptParser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    internal static ScriptProgram Parse(string source)
    {
        var parser = new ScriptParser(new ScriptLexer(source).Tokenize());
        return parser.ParseProgram();
    }

    private ScriptProgram ParseProgram()
    {
        var statements = new List<Statement>();
        while (Current.Type != TokenType.EndOfFile)
        {
            if (Match(TokenType.Semicolon))
            {
                continue;
            }

            statements.Add(ParseStatement());
        }

        return new ScriptProgram(statements);
    }

    private Statement ParseStatement()
    {
        if (Match(TokenType.Let) || Match(TokenType.Global))
        {
            var isGlobal = Previous.Type == TokenType.Global;
            return ParseVariableDeclaration(isGlobal);
        }

        if (Match(TokenType.If))
        {
            return ParseIf();
        }

        if (Match(TokenType.Loop))
        {
            var loopLine = Previous.Line;
            var count = ParseExpression();
            Match(TokenType.Then);
            return new LoopStatement(count, ParseBlock(), loopLine);
        }

        if (Match(TokenType.ForEach))
        {
            var eachLine = Previous.Line;
            var item = Expect(TokenType.Identifier, "循环变量名").Text;
            Expect(TokenType.In, "“于”");
            var source = ParseExpression();
            return new ForEachStatement(item, source, ParseBlock(), eachLine);
        }

        if (Match(TokenType.Break))
        {
            return new BreakStatement(Previous.Line);
        }

        if (Match(TokenType.Continue))
        {
            return new ContinueStatement(Previous.Line);
        }

        if (Match(TokenType.Function))
        {
            return ParseFunction();
        }

        if (Match(TokenType.Return))
        {
            var returnLine = Previous.Line;
            Expression? value = null;
            if (Current.Type is not (TokenType.RBrace or TokenType.Semicolon or TokenType.EndOfFile))
            {
                value = ParseExpression();
            }

            Match(TokenType.Semicolon);
            return new ReturnStatement(value, returnLine);
        }

        var line = Current.Line;
        var expression = ParseExpression();
        if (Match(TokenType.Assign))
        {
            if (expression is not (VariableExpression or MemberExpression or IndexExpression))
            {
                throw Error("赋值左侧必须是变量、控件属性或 JSON 索引", Current);
            }

            var value = ParseExpression();
            Match(TokenType.Semicolon);
            return new AssignmentStatement(expression, value, line);
        }

        Match(TokenType.Semicolon);
        return new ExpressionStatement(expression, line);
    }

    private Statement ParseVariableDeclaration(bool isGlobal)
    {
        var line = Current.Line;
        var name = Expect(TokenType.Identifier, "变量名").Text;
        var type = ScriptType.Any;
        var hasExplicitType = false;
        if (Match(TokenType.Colon))
        {
            hasExplicitType = true;
            var typeName = Expect(TokenType.Identifier, "类型名").Text;
            if (!ScriptTypeNames.TryParse(typeName, out type))
            {
                throw Error($"未知类型 “{typeName}”，可用：整数、小数、文本、逻辑、JSON、任意", Previous);
            }
        }

        Expression? initializer = null;
        if (Match(TokenType.Assign))
        {
            initializer = ParseExpression();
        }

        Match(TokenType.Semicolon);
        return new VariableDeclarationStatement(name, type, hasExplicitType, initializer, isGlobal, line);
    }

    private Statement ParseIf()
    {
        var line = Previous.Line;
        var condition = ParseExpression();
        Match(TokenType.Then);
        var then = ParseBlock();
        BlockStatement? otherwise = null;
        if (Match(TokenType.Else))
        {
            otherwise = ParseBlock();
        }

        return new IfStatement(condition, then, otherwise, line);
    }

    private Statement ParseFunction()
    {
        var line = Previous.Line;
        var name = Expect(TokenType.Identifier, "函数名").Text;
        Expect(TokenType.LParen, "“(”");
        var parameters = new List<FunctionParameter>();
        if (Current.Type != TokenType.RParen)
        {
            do
            {
                var parameterName = Expect(TokenType.Identifier, "参数名").Text;
                var type = ScriptType.Any;
                if (Match(TokenType.Colon))
                {
                    var typeName = Expect(TokenType.Identifier, "类型名").Text;
                    if (!ScriptTypeNames.TryParse(typeName, out type))
                    {
                        throw Error($"未知参数类型 “{typeName}”", Previous);
                    }
                }

                parameters.Add(new FunctionParameter(parameterName, type));
            }
            while (Match(TokenType.Comma));
        }

        Expect(TokenType.RParen, "“)”");
        var body = ParseBlock();
        return new FunctionDeclarationStatement(name, parameters, body, line);
    }

    private BlockStatement ParseBlock()
    {
        var line = Current.Line;
        Expect(TokenType.LBrace, "“{”");
        var statements = new List<Statement>();
        while (Current.Type is not TokenType.RBrace and not TokenType.EndOfFile)
        {
            if (Match(TokenType.Semicolon))
            {
                continue;
            }

            statements.Add(ParseStatement());
        }

        Expect(TokenType.RBrace, "“}”");
        return new BlockStatement(statements, line);
    }

    private Expression ParseExpression() => ParseOr();

    private Expression ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenType.OrOr))
        {
            left = new BinaryExpression(left, TokenType.OrOr, ParseAnd(), left.Line);
        }

        return left;
    }

    private Expression ParseAnd()
    {
        var left = ParseEquality();
        while (Match(TokenType.AndAnd))
        {
            left = new BinaryExpression(left, TokenType.AndAnd, ParseEquality(), left.Line);
        }

        return left;
    }

    private Expression ParseEquality()
    {
        var left = ParseComparison();
        while (Match(TokenType.Equal) || Match(TokenType.NotEqual))
        {
            left = new BinaryExpression(left, Previous.Type, ParseComparison(), left.Line);
        }

        return left;
    }

    private Expression ParseComparison()
    {
        var left = ParseAdditive();
        while (Match(TokenType.Less) || Match(TokenType.Greater) ||
               Match(TokenType.LessEqual) || Match(TokenType.GreaterEqual))
        {
            left = new BinaryExpression(left, Previous.Type, ParseAdditive(), left.Line);
        }

        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Match(TokenType.Plus) || Match(TokenType.Minus))
        {
            left = new BinaryExpression(left, Previous.Type, ParseMultiplicative(), left.Line);
        }

        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Match(TokenType.Star) || Match(TokenType.Slash) || Match(TokenType.Percent))
        {
            left = new BinaryExpression(left, Previous.Type, ParseUnary(), left.Line);
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.Bang) || Match(TokenType.Minus) || Match(TokenType.Plus))
        {
            var op = Previous.Type;
            if (op == TokenType.Plus)
            {
                return ParseUnary();
            }

            return new UnaryExpression(op, ParseUnary(), Previous.Line);
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expression = ParsePrimary();
        while (true)
        {
            if (Match(TokenType.Dot))
            {
                var member = Expect(TokenType.Identifier, "属性名").Text;
                expression = new MemberExpression(expression, member, expression.Line);
                continue;
            }

            if (Match(TokenType.LBracket))
            {
                var index = ParseExpression();
                Expect(TokenType.RBracket, "“]”");
                expression = new IndexExpression(expression, index, expression.Line);
                continue;
            }

            if (Match(TokenType.LParen))
            {
                var arguments = new List<Expression>();
                if (Current.Type != TokenType.RParen)
                {
                    do
                    {
                        arguments.Add(ParseExpression());
                    }
                    while (Match(TokenType.Comma));
                }

                Expect(TokenType.RParen, "“)”");
                expression = new CallExpression(expression, arguments, expression.Line);
                continue;
            }

            break;
        }

        return expression;
    }

    private Expression ParsePrimary()
    {
        if (Match(TokenType.Number))
        {
            return new LiteralExpression(ScriptValue.FromNumber(Previous.Number), Previous.Line);
        }

        if (Match(TokenType.String))
        {
            return new LiteralExpression(ScriptValue.FromText(Previous.Text), Previous.Line);
        }

        if (Match(TokenType.True))
        {
            return new LiteralExpression(ScriptValue.FromBool(true), Previous.Line);
        }

        if (Match(TokenType.False))
        {
            return new LiteralExpression(ScriptValue.FromBool(false), Previous.Line);
        }

        if (Match(TokenType.Null))
        {
            return new LiteralExpression(ScriptValue.Null, Previous.Line);
        }

        if (Match(TokenType.LBracket))
        {
            var line = Previous.Line;
            var items = new List<Expression>();
            if (Current.Type != TokenType.RBracket)
            {
                do
                {
                    items.Add(ParseExpression());
                }
                while (Match(TokenType.Comma));
            }

            Expect(TokenType.RBracket, "“]”");
            return new ListExpression(items, line);
        }

        if (Match(TokenType.LBrace))
        {
            var line = Previous.Line;
            var items = new List<KeyValuePair<string, Expression>>();
            if (Current.Type != TokenType.RBrace)
            {
                do
                {
                    string key;
                    if (Match(TokenType.String) || Match(TokenType.Identifier))
                    {
                        key = Previous.Text;
                    }
                    else
                    {
                        throw Error("字典键需要字符串或标识符", Current);
                    }

                    Expect(TokenType.Colon, "“:”");
                    items.Add(new KeyValuePair<string, Expression>(key, ParseExpression()));
                }
                while (Match(TokenType.Comma));
            }

            Expect(TokenType.RBrace, "“}”");
            return new DictionaryExpression(items, line);
        }

        if (Match(TokenType.Identifier))
        {
            return new VariableExpression(Previous.Text, Previous.Line);
        }

        if (Match(TokenType.LParen))
        {
            var expression = ParseExpression();
            Expect(TokenType.RParen, "“)”");
            return expression;
        }

        throw Error($"无法解析表达式，遇到 “{Current.Text}”", Current);
    }

    private bool Match(TokenType type)
    {
        if (Current.Type != type)
        {
            return false;
        }

        _index++;
        return true;
    }

    private Token Expect(TokenType type, string description)
    {
        if (Current.Type != type)
        {
            throw Error($"需要 {description}，但遇到 “{Current.Text}”", Current);
        }

        var token = Current;
        _index++;
        return token;
    }

    private Token Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

    private Token Previous => _tokens[Math.Max(0, _index - 1)];

    private static ScriptException Error(string message, Token token) =>
        new(message, token.Line, token.Column);
}
