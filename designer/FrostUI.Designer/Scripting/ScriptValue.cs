using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Scripting;

internal enum ScriptType
{
    Any,
    Int,
    Double,
    String,
    Bool,
    Json,
    List,
}

internal enum ScriptValueKind
{
    Null,
    Number,
    String,
    Bool,
    Json,
    Control,
}

internal sealed class ScriptValueException : Exception
{
    public ScriptValueException(string message) : base(message)
    {
    }
}

internal sealed class ScriptValue
{
    private ScriptValue(ScriptValueKind kind)
    {
        Kind = kind;
    }

    public static ScriptValue Null { get; } = new(ScriptValueKind.Null);

    public ScriptValueKind Kind { get; }

    public double Number { get; private init; }

    public string Text { get; private init; } = string.Empty;

    public bool Boolean { get; private init; }

    public JsonNode? Json { get; private init; }

    public DesignControl? Control { get; private init; }

    public static ScriptValue FromNumber(double number) =>
        new(ScriptValueKind.Number) { Number = number };

    public static ScriptValue FromText(string text) =>
        new(ScriptValueKind.String) { Text = text ?? string.Empty };

    public static ScriptValue FromBool(bool value) =>
        new(ScriptValueKind.Bool) { Boolean = value };

    public static ScriptValue FromControl(DesignControl control) =>
        new(ScriptValueKind.Control) { Control = control };

    public static ScriptValue FromJson(JsonNode? node)
    {
        if (node is null)
        {
            return Null;
        }

        switch (node.GetValueKind())
        {
            case JsonValueKind.Null:
                return Null;
            case JsonValueKind.True:
                return FromBool(true);
            case JsonValueKind.False:
                return FromBool(false);
            case JsonValueKind.String:
                return FromText(node.GetValue<string>());
            case JsonValueKind.Number:
                return FromNumber(node.GetValue<double>());
            default:
                return new ScriptValue(ScriptValueKind.Json) { Json = node };
        }
    }

    public bool IsTruthy() => Kind switch
    {
        ScriptValueKind.Null => false,
        ScriptValueKind.Bool => Boolean,
        ScriptValueKind.Number => Math.Abs(Number) > 0.0000001,
        ScriptValueKind.String => !string.IsNullOrEmpty(Text),
        ScriptValueKind.Json => Json is JsonArray { Count: > 0 } or JsonObject { Count: > 0 },
        ScriptValueKind.Control => true,
        _ => false,
    };

    public double AsNumber() => Kind switch
    {
        ScriptValueKind.Number => Number,
        ScriptValueKind.Bool => Boolean ? 1 : 0,
        ScriptValueKind.String when double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        ScriptValueKind.String => throw new ScriptValueException($"“{Text}” 不是数字"),
        ScriptValueKind.Json when Json is JsonValue value && value.TryGetValue<double>(out var jsonNumber) => jsonNumber,
        _ => throw new ScriptValueException($"类型 {TypeName} 不能作为数字使用"),
    };

    public bool AsBoolean() => IsTruthy();

    public JsonNode AsJson()
    {
        switch (Kind)
        {
            case ScriptValueKind.Json when Json is not null:
                return Json;
            case ScriptValueKind.String:
                try
                {
                    return JsonNode.Parse(Text) ?? JsonValue.Create((string?)null)!;
                }
                catch (JsonException ex)
                {
                    throw new ScriptValueException($"JSON 解析失败：{ex.Message}");
                }

            default:
                throw new ScriptValueException($"类型 {TypeName} 不能作为 JSON 使用");
        }
    }

    public DesignControl AsControl() => Kind == ScriptValueKind.Control && Control is not null
        ? Control
        : throw new ScriptValueException($"类型 {TypeName} 不是控件");

    public string ToText() => Kind switch
    {
        ScriptValueKind.Null => "空",
        ScriptValueKind.Bool => Boolean ? "真" : "假",
        ScriptValueKind.Number => FormatNumber(Number),
        ScriptValueKind.String => Text,
        ScriptValueKind.Json => Json?.ToJsonString() ?? "空",
        ScriptValueKind.Control => Control?.Name ?? "控件",
        _ => string.Empty,
    };

    public string TypeName => Kind switch
    {
        ScriptValueKind.Null => "空",
        ScriptValueKind.Bool => "逻辑",
        ScriptValueKind.String => "文本",
        ScriptValueKind.Json when Json is JsonArray => "列表",
        ScriptValueKind.Json => "JSON",
        ScriptValueKind.Control => "控件",
        ScriptValueKind.Number => Math.Abs(Number - Math.Truncate(Number)) < 0.0000001 ? "整数" : "小数",
        _ => "任意",
    };

    public bool ContentEquals(ScriptValue other)
    {
        if (Kind == ScriptValueKind.Null || other.Kind == ScriptValueKind.Null)
        {
            return Kind == other.Kind;
        }

        if (Kind == ScriptValueKind.Number && other.Kind == ScriptValueKind.Number)
        {
            return Math.Abs(Number - other.Number) < 0.0000001;
        }

        if (Kind == ScriptValueKind.Bool && other.Kind == ScriptValueKind.Bool)
        {
            return Boolean == other.Boolean;
        }

        if (Kind == ScriptValueKind.String && other.Kind == ScriptValueKind.String)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        if (Kind == ScriptValueKind.Json && other.Kind == ScriptValueKind.Json)
        {
            return JsonNode.DeepEquals(Json, other.Json);
        }

        if (Kind == ScriptValueKind.Control && other.Kind == ScriptValueKind.Control)
        {
            return ReferenceEquals(Control, other.Control);
        }

        return false;
    }

    public static ScriptValue Coerce(ScriptValue value, ScriptType type)
    {
        try
        {
            return type switch
            {
                ScriptType.Any => value,
                ScriptType.Int => FromNumber(Math.Truncate(value.AsNumber())),
                ScriptType.Double => FromNumber(value.AsNumber()),
                ScriptType.String => FromText(value.ToText()),
                ScriptType.Bool => FromBool(value.AsBoolean()),
                ScriptType.Json => FromJson(value.AsJson()),
                ScriptType.List => value.AsJson() is JsonArray
                    ? FromJson(value.AsJson())
                    : throw new ScriptValueException("值不是列表"),
                _ => value,
            };
        }
        catch (ScriptValueException ex)
        {
            throw new ScriptValueException($"无法转换为 {ScriptTypeNames.Display(type)}：{ex.Message}");
        }
    }

    public static JsonNode? ToJsonNode(ScriptValue value) => value.Kind switch
    {
        ScriptValueKind.Null => null,
        ScriptValueKind.Bool => JsonValue.Create(value.Boolean),
        ScriptValueKind.Number => JsonValue.Create(value.Number),
        ScriptValueKind.String => JsonValue.Create(value.Text),
        ScriptValueKind.Json => value.Json?.DeepClone(),
        _ => JsonValue.Create(value.ToText()),
    };

    public static ScriptValue DefaultFor(ScriptType type) => type switch
    {
        ScriptType.Int or ScriptType.Double => FromNumber(0),
        ScriptType.String => FromText(string.Empty),
        ScriptType.Bool => FromBool(false),
        ScriptType.Json => FromJson(new JsonObject()),
        ScriptType.List => FromJson(new JsonArray()),
        _ => Null,
    };

    private static string FormatNumber(double number)
    {
        if (Math.Abs(number - Math.Truncate(number)) < 0.0000001)
        {
            return ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture);
        }

        return number.ToString("0.##########", CultureInfo.InvariantCulture);
    }
}

internal static class ScriptTypeNames
{
    public static bool TryParse(string name, out ScriptType type)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "整数":
            case "int":
            case "integer":
                type = ScriptType.Int;
                return true;
            case "小数":
            case "double":
            case "float":
            case "number":
                type = ScriptType.Double;
                return true;
            case "文本":
            case "string":
            case "str":
                type = ScriptType.String;
                return true;
            case "逻辑":
            case "bool":
            case "boolean":
                type = ScriptType.Bool;
                return true;
            case "json":
                type = ScriptType.Json;
                return true;
            case "列表":
            case "list":
            case "array":
                type = ScriptType.List;
                return true;
            case "任意":
            case "any":
            case "var":
                type = ScriptType.Any;
                return true;
            default:
                type = ScriptType.Any;
                return false;
        }
    }

    public static string Display(ScriptType type) => type switch
    {
        ScriptType.Int => "整数",
        ScriptType.Double => "小数",
        ScriptType.String => "文本",
        ScriptType.Bool => "逻辑",
        ScriptType.Json => "JSON",
        ScriptType.List => "列表",
        _ => "任意",
    };
}
