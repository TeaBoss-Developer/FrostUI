using System.Text.RegularExpressions;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public sealed record CompletionResult(
    IReadOnlyList<CompletionItem> Items,
    int ReplaceStart,
    int ReplaceLength,
    string Prefix,
    string? BaseName,
    bool IsTypeContext,
    bool IsInStringOrComment)
{
    public static CompletionResult Empty(int caret) =>
        new([], caret, 0, string.Empty, null, false, false);
}

/// <summary>
/// Lightweight, design-aware completion for the FrostUI event language.
/// It intentionally avoids a full semantic model and uses the token around
/// the caret plus the live design document.
/// </summary>
public static class ScriptCompletionService
{
    private static readonly CompletionItem[] Keywords =
    [
        CompletionItem.Create("如果", "如果 ", "关键字 · 条件分支", CompletionKind.Keyword),
        CompletionItem.Create("否则", "否则 ", "关键字 · 条件分支", CompletionKind.Keyword),
        CompletionItem.Create("循环", "循环 ", "关键字 · 重复执行", CompletionKind.Keyword),
        CompletionItem.Create("遍历", "遍历 ", "关键字 · 遍历列表", CompletionKind.Keyword),
        CompletionItem.Create("于", "于 ", "关键字 · 遍历来源", CompletionKind.Keyword),
        CompletionItem.Create("函数", "函数 ", "关键字 · 定义函数", CompletionKind.Keyword),
        CompletionItem.Create("返回", "返回 ", "关键字 · 函数返回值", CompletionKind.Keyword),
        CompletionItem.Create("设", "设 ", "关键字 · 局部变量", CompletionKind.Keyword),
        CompletionItem.Create("全局", "全局 ", "关键字 · 持久变量", CompletionKind.Keyword),
        CompletionItem.Create("真", "真", "逻辑值 · true", CompletionKind.Keyword),
        CompletionItem.Create("假", "假", "逻辑值 · false", CompletionKind.Keyword),
        CompletionItem.Create("空", "空", "空值 · null", CompletionKind.Keyword),
        CompletionItem.Create("且", "且 ", "逻辑运算 · &&", CompletionKind.Keyword),
        CompletionItem.Create("或", "或 ", "逻辑运算 · ||", CompletionKind.Keyword),
        CompletionItem.Create("非", "非 ", "逻辑运算 · !", CompletionKind.Keyword),
    ];

    private static readonly CompletionItem[] Types =
    [
        CompletionItem.Create("整数", "整数", "类型 · 整数", CompletionKind.Type),
        CompletionItem.Create("小数", "小数", "类型 · 小数", CompletionKind.Type),
        CompletionItem.Create("文本", "文本", "类型 · 文本", CompletionKind.Type),
        CompletionItem.Create("逻辑", "逻辑", "类型 · 逻辑", CompletionKind.Type),
        CompletionItem.Create("JSON", "JSON", "类型 · JSON", CompletionKind.Type),
        CompletionItem.Create("列表", "列表", "类型 · 列表 / JSON 数组", CompletionKind.Type),
        CompletionItem.Create("任意", "任意", "类型 · 任意", CompletionKind.Type),
    ];

    private static readonly CompletionItem[] Builtins =
    [
        CompletionItem.Create("输出", "输出()", "函数 · 输出日志", CompletionKind.Method, 1),
        CompletionItem.Create("提示", "提示()", "函数 · 预览 Toast", CompletionKind.Method, 1),
        CompletionItem.Create("长度", "长度()", "函数 · 文本 / JSON 长度", CompletionKind.Method, 1),
        CompletionItem.Create("类型", "类型()", "函数 · 值的类型", CompletionKind.Method, 1),
        CompletionItem.Create("文本", "文本()", "函数 · 转为文本", CompletionKind.Method, 1),
        CompletionItem.Create("整数", "整数()", "函数 · 转为整数", CompletionKind.Method, 1),
        CompletionItem.Create("小数", "小数()", "函数 · 转为小数", CompletionKind.Method, 1),
        CompletionItem.Create("逻辑", "逻辑()", "函数 · 转为逻辑", CompletionKind.Method, 1),
        CompletionItem.Create("随机", "随机()", "函数 · 随机整数 (最小, 最大)", CompletionKind.Method, 1),
        CompletionItem.Create("等待", "等待()", "函数 · 毫秒等待", CompletionKind.Method, 1),
        CompletionItem.Create("JSON", "JSON", "命名空间 · JSON.解析 / 取值 / 文本", CompletionKind.Namespace),
        CompletionItem.Create("HTTP", "HTTP", "命名空间 · HTTP.获取 / 获取JSON", CompletionKind.Namespace),
    ];

    private static readonly CompletionItem[] JsonMembers =
    [
        CompletionItem.Create("解析", "解析()", "JSON · 解析文本", CompletionKind.Method, 1),
        CompletionItem.Create("取值", "取值()", "JSON · 读取路径，如 items[0].name", CompletionKind.Method, 1),
        CompletionItem.Create("文本", "文本()", "JSON · 序列化为文本", CompletionKind.Method, 1),
    ];

    private static readonly CompletionItem[] HttpMembers =
    [
        CompletionItem.Create("获取", "获取()", "HTTP · GET，返回文本", CompletionKind.Method, 1),
        CompletionItem.Create("获取JSON", "获取JSON()", "HTTP · GET，返回 JSON", CompletionKind.Method, 1),
        CompletionItem.Create("提交JSON", "提交JSON()", "HTTP · POST JSON", CompletionKind.Method, 1),
    ];

    private static readonly CompletionItem[] ControlProperties =
    [
        CompletionItem.Create("名称", "名称", "控件 · 只读名称", CompletionKind.Property),
        CompletionItem.Create("类型", "类型", "控件 · 只读类型", CompletionKind.Property),
        CompletionItem.Create("文本", "文本", "控件 · 可读写文本", CompletionKind.Property),
        CompletionItem.Create("左", "左", "控件 · 可读写 X", CompletionKind.Property),
        CompletionItem.Create("顶", "顶", "控件 · 可读写 Y", CompletionKind.Property),
        CompletionItem.Create("宽", "宽", "控件 · 可读写宽度", CompletionKind.Property),
        CompletionItem.Create("高", "高", "控件 · 可读写高度", CompletionKind.Property),
        CompletionItem.Create("可见", "可见", "控件 · 可读写可见性", CompletionKind.Property),
        CompletionItem.Create("启用", "启用", "控件 · 可读写启用状态", CompletionKind.Property),
        CompletionItem.Create("字号", "字号", "控件 · 可读写字号", CompletionKind.Property),
        CompletionItem.Create("粗体", "粗体", "控件 · 可读写粗体", CompletionKind.Property),
        CompletionItem.Create("圆角", "圆角", "控件 · 可读写圆角", CompletionKind.Property),
        CompletionItem.Create("开启", "开启", "控件 · 开关状态", CompletionKind.Property),
        CompletionItem.Create("值", "值", "控件 · 数值 / 开关值", CompletionKind.Property),
    ];

    private static readonly Regex VariableRegex = new(
        @"(?:设|全局)\s+([\p{L}_][\p{L}\p{Nd}_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CompletionResult GetCompletions(DesignDocument document, string text, int caret)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        if (IsInsideStringOrComment(text, caret))
        {
            return new CompletionResult([], caret, 0, string.Empty, null, false, true);
        }

        var start = caret;
        while (start > 0 && IsIdentifierPart(text[start - 1]))
        {
            start--;
        }

        var prefix = text[start..caret];
        string? baseName = null;
        if (start > 0 && text[start - 1] == '.')
        {
            var end = start - 1;
            while (end > 0 && char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }

            var nameStart = end;
            while (nameStart > 0 && IsIdentifierPart(text[nameStart - 1]))
            {
                nameStart--;
            }

            if (nameStart < end)
            {
                baseName = text[nameStart..end];
            }
        }

        var isTypeContext = IsTypeContext(text, start);
        List<CompletionItem> candidates;
        if (isTypeContext)
        {
            candidates = [.. Types];
        }
        else if (baseName is not null)
        {
            candidates = GetMemberCompletions(document, baseName);
        }
        else
        {
            candidates = [.. Keywords, .. Builtins];
            candidates.AddRange(Types);
            candidates.AddRange(document.Controls.Select(control =>
                CompletionItem.Create(control.Name, control.Name, $"控件 · {control.KindTitle}", CompletionKind.Control)));
            candidates.AddRange(GetVariables(text));
        }

        var filtered = candidates
            .Where(x => prefix.Length == 0 ||
                        x.Display.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        x.InsertText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => (int)x.Kind)
            .ThenBy(x => x.Display, StringComparer.CurrentCulture)
            .Take(80)
            .ToArray();

        return new CompletionResult(filtered, start, prefix.Length, prefix, baseName, isTypeContext, false);
    }

    private static List<CompletionItem> GetMemberCompletions(DesignDocument document, string baseName)
    {
        if (string.Equals(baseName, "JSON", StringComparison.OrdinalIgnoreCase))
        {
            return [.. JsonMembers];
        }

        if (string.Equals(baseName, "HTTP", StringComparison.OrdinalIgnoreCase))
        {
            return [.. HttpMembers];
        }

        var control = document.FindControl(baseName);
        return control is null ? [] : [.. ControlProperties];
    }

    private static IEnumerable<CompletionItem> GetVariables(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in VariableRegex.Matches(text))
        {
            var name = match.Groups[1].Value;
            if (seen.Add(name))
            {
                yield return CompletionItem.Create(name, name, "变量 · 当前脚本", CompletionKind.Variable);
            }
        }
    }

    private static bool IsTypeContext(string text, int start)
    {
        var index = start - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        if (index < 0 || text[index] is not (':' or '：'))
        {
            return false;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var line = text[lineStart..index];
        return line.Contains("设", StringComparison.Ordinal) ||
               line.Contains("全局", StringComparison.Ordinal);
    }

    private static bool IsInsideStringOrComment(string text, int caret)
    {
        var inString = false;
        var quote = '\0';
        var inLineComment = false;
        var inBlockComment = false;
        for (var i = 0; i < caret; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (c is '/' or '#')
            {
                if (c == '#')
                {
                    inLineComment = true;
                    continue;
                }

                if (next == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }
            }

            if (c is '"' or '\'' or '“')
            {
                inString = true;
                quote = c == '“' ? '”' : c;
            }
        }

        return inString || inLineComment || inBlockComment;
    }

    private static bool IsIdentifierPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_';
}
