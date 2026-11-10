using System.Globalization;
using System.Text;

namespace FrostUI.Designer.Scripting;

internal sealed class ScriptLexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["设"] = TokenType.Let,
        ["let"] = TokenType.Let,
        ["全局"] = TokenType.Global,
        ["global"] = TokenType.Global,
        ["如果"] = TokenType.If,
        ["if"] = TokenType.If,
        ["那么"] = TokenType.Then,
        ["then"] = TokenType.Then,
        ["否则"] = TokenType.Else,
        ["else"] = TokenType.Else,
        ["循环"] = TokenType.Loop,
        ["loop"] = TokenType.Loop,
        ["遍历"] = TokenType.ForEach,
        ["foreach"] = TokenType.ForEach,
        ["于"] = TokenType.In,
        ["in"] = TokenType.In,
        ["函数"] = TokenType.Function,
        ["function"] = TokenType.Function,
        ["fn"] = TokenType.Function,
        ["返回"] = TokenType.Return,
        ["return"] = TokenType.Return,
        ["跳出"] = TokenType.Break,
        ["break"] = TokenType.Break,
        ["继续"] = TokenType.Continue,
        ["continue"] = TokenType.Continue,
        ["真"] = TokenType.True,
        ["true"] = TokenType.True,
        ["假"] = TokenType.False,
        ["false"] = TokenType.False,
        ["空"] = TokenType.Null,
        ["null"] = TokenType.Null,
        ["且"] = TokenType.AndAnd,
        ["与"] = TokenType.AndAnd,
        ["并且"] = TokenType.AndAnd,
        ["and"] = TokenType.AndAnd,
        ["或"] = TokenType.OrOr,
        ["或者"] = TokenType.OrOr,
        ["or"] = TokenType.OrOr,
        ["非"] = TokenType.Bang,
        ["not"] = TokenType.Bang,
    };

    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    internal ScriptLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    internal List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var token = NextToken();
            tokens.Add(token);
            if (token.Type == TokenType.EndOfFile)
            {
                return tokens;
            }
        }
    }

    private Token NextToken()
    {
        SkipTrivia();

        if (_position >= _source.Length)
        {
            return new Token(TokenType.EndOfFile, string.Empty, _line, _column);
        }

        var line = _line;
        var column = _column;
        var current = _source[_position];

        if (char.IsLetter(current) || current == '_')
        {
            var identifier = ReadIdentifier();
            return Keywords.TryGetValue(identifier, out var keyword)
                ? new Token(keyword, identifier, line, column)
                : new Token(TokenType.Identifier, identifier, line, column);
        }

        if (char.IsDigit(current) || (current == '.' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1])))
        {
            return ReadNumber(line, column);
        }

        if (current is '"' or '\'' or '“')
        {
            var text = ReadString(current, line, column);
            return new Token(TokenType.String, text, line, column);
        }

        Advance();
        return current switch
        {
            '+' => new Token(TokenType.Plus, "+", line, column),
            '-' => new Token(TokenType.Minus, "-", line, column),
            '*' => new Token(TokenType.Star, "*", line, column),
            '/' => new Token(TokenType.Slash, "/", line, column),
            '%' => new Token(TokenType.Percent, "%", line, column),
            '=' when Match('=') => new Token(TokenType.Equal, "==", line, column),
            '=' => new Token(TokenType.Assign, "=", line, column),
            '!' when Match('=') => new Token(TokenType.NotEqual, "!=", line, column),
            '!' => new Token(TokenType.Bang, "!", line, column),
            '<' when Match('=') => new Token(TokenType.LessEqual, "<=", line, column),
            '<' => new Token(TokenType.Less, "<", line, column),
            '>' when Match('=') => new Token(TokenType.GreaterEqual, ">=", line, column),
            '>' => new Token(TokenType.Greater, ">", line, column),
            '&' when Match('&') => new Token(TokenType.AndAnd, "&&", line, column),
            '|' when Match('|') => new Token(TokenType.OrOr, "||", line, column),
            '(' or '（' => new Token(TokenType.LParen, "(", line, column),
            ')' or '）' => new Token(TokenType.RParen, ")", line, column),
            '{' or '｛' => new Token(TokenType.LBrace, "{", line, column),
            '}' or '｝' => new Token(TokenType.RBrace, "}", line, column),
            '[' or '【' => new Token(TokenType.LBracket, "[", line, column),
            ']' or '】' => new Token(TokenType.RBracket, "]", line, column),
            ',' or '，' => new Token(TokenType.Comma, ",", line, column),
            ':' or '：' => new Token(TokenType.Colon, ":", line, column),
            ';' or '；' => new Token(TokenType.Semicolon, ";", line, column),
            '.' or '。' => new Token(TokenType.Dot, ".", line, column),
            '＝' => new Token(TokenType.Assign, "=", line, column),
            _ => throw new ScriptException($"无法识别的字符 “{current}”", line, column),
        };
    }

    private void SkipTrivia()
    {
        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (char.IsWhiteSpace(current))
            {
                Advance();
                continue;
            }

            if (current == '#' || (current == '/' && _position + 1 < _source.Length && _source[_position + 1] == '/'))
            {
                while (_position < _source.Length && _source[_position] != '\n')
                {
                    Advance();
                }

                continue;
            }

            if (current == '/' && _position + 1 < _source.Length && _source[_position + 1] == '*')
            {
                Advance();
                Advance();
                while (_position + 1 < _source.Length && !(_source[_position] == '*' && _source[_position + 1] == '/'))
                {
                    Advance();
                }

                if (_position + 1 >= _source.Length)
                {
                    throw new ScriptException("块注释没有结束", _line, _column);
                }

                Advance();
                Advance();
                continue;
            }

            break;
        }
    }

    private string ReadIdentifier()
    {
        var builder = new StringBuilder();
        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (!char.IsLetterOrDigit(current) && current != '_')
            {
                break;
            }

            builder.Append(current);
            Advance();
        }

        return builder.ToString();
    }

    private Token ReadNumber(int line, int column)
    {
        var start = _position;
        while (_position < _source.Length && char.IsDigit(_source[_position]))
        {
            Advance();
        }

        if (_position < _source.Length && _source[_position] == '.')
        {
            Advance();
            while (_position < _source.Length && char.IsDigit(_source[_position]))
            {
                Advance();
            }
        }

        if (_position < _source.Length && (_source[_position] == 'e' || _source[_position] == 'E'))
        {
            Advance();
            if (_position < _source.Length && (_source[_position] == '+' || _source[_position] == '-'))
            {
                Advance();
            }

            while (_position < _source.Length && char.IsDigit(_source[_position]))
            {
                Advance();
            }
        }

        var text = _source[start.._position];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            throw new ScriptException($"数字格式错误：{text}", line, column);
        }

        return new Token(TokenType.Number, text, line, column, number);
    }

    private string ReadString(char opening, int line, int column)
    {
        Advance();
        var closing = opening == '“' ? '”' : opening;
        var builder = new StringBuilder();
        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current == closing)
            {
                Advance();
                return builder.ToString();
            }

            if (current == '\n' || current == '\r')
            {
                throw new ScriptException("字符串不能跨行", line, column);
            }

            if (current == '\\')
            {
                Advance();
                if (_position >= _source.Length)
                {
                    break;
                }

                var escaped = _source[_position];
                Advance();
                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '0' => '\0',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    'u' => ReadUnicodeEscape(line, column),
                    _ => escaped,
                });
                continue;
            }

            builder.Append(current);
            Advance();
        }

        throw new ScriptException("字符串没有结束", line, column);
    }

    private char ReadUnicodeEscape(int line, int column)
    {
        if (_position + 4 > _source.Length)
        {
            throw new ScriptException("Unicode 转义不完整", line, column);
        }

        var hex = _source.Substring(_position, 4);
        _position += 4;
        _column += 4;
        return ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
            ? (char)code
            : throw new ScriptException($"Unicode 转义无效：\\u{hex}", line, column);
    }

    private bool Match(char expected)
    {
        if (_position >= _source.Length || _source[_position] != expected)
        {
            return false;
        }

        Advance();
        return true;
    }

    private void Advance()
    {
        if (_position >= _source.Length)
        {
            return;
        }

        if (_source[_position] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _position++;
    }
}
