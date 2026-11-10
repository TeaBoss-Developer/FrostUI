using System.Globalization;

namespace FrostUI.Designer.Scripting;

internal static class BreakpointCondition
{
    public static bool Matches(string? condition, DebugSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var text = condition.Trim();
        foreach (var op in new[] { ">=", "<=", "!=", "==", ">", "<" })
        {
            var index = text.IndexOf(op, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var name = text[..index].Trim();
            var right = text[(index + op.Length)..].Trim();
            if (!snapshot.Variables.TryGetValue(name, out var left))
            {
                return false;
            }

            return Compare(left, right, op);
        }

        if (snapshot.Variables.TryGetValue(text, out var value))
        {
            return value.Trim().ToLowerInvariant() is "true" or "1" or "是" or "真";
        }

        return false;
    }

    private static bool Compare(string left, string right, string op)
    {
        right = right.Trim().Trim('"', '\'');
        left = left.Trim();
        if (double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var ln) &&
            double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rn))
        {
            return op switch
            {
                ">" => ln > rn,
                "<" => ln < rn,
                ">=" => ln >= rn,
                "<=" => ln <= rn,
                "==" => Math.Abs(ln - rn) < 0.000001,
                "!=" => Math.Abs(ln - rn) >= 0.000001,
                _ => false,
            };
        }

        var comparison = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            _ => false,
        };
    }
}
