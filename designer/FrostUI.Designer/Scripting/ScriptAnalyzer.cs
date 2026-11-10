using FrostUI.Designer.Models;

namespace FrostUI.Designer.Scripting;

/// <summary>
/// Lightweight static checks on top of the parser. It catches the mistakes
/// that are most common in event scripts: unknown controls, unknown control
/// properties and calls to functions that do not exist.
/// </summary>
public static class ScriptAnalyzer
{
    private static readonly HashSet<string> BuiltinCalls = new(StringComparer.OrdinalIgnoreCase)
    {
        "输出", "log", "提示", "toast", "长度", "len", "类型", "type",
        "文本", "str", "整数", "int", "小数", "double", "逻辑", "bool",
        "随机", "random", "等待", "wait",
    };

    private static readonly HashSet<string> ControlProperties = new(StringComparer.Ordinal)
    {
        "名称", "类型", "文本", "左", "顶", "宽", "高",
        "可见", "启用", "字号", "粗体", "圆角", "开启", "值",
        "选中", "选中项", "选项", "分组", "最小值", "最大值", "步长", "日期",
        "标签页序号", "分栏面板", "当前标签页", "级别", "表格列", "图表类型",
        "可编辑", "可排序",
        "name", "type", "text", "x", "y", "width", "height",
        "visible", "enabled", "fontSize", "bold", "cornerRadius", "isOn", "value",
        "checked", "selectedItem", "items", "groupName", "min", "max", "step", "date",
        "tabIndex", "paneIndex", "selectedIndex", "severity", "tableColumns", "chartType",
        "gridEditable", "gridSortable",
    };

    private static readonly Dictionary<string, ScriptType> ControlPropertyTypes =
        new(StringComparer.Ordinal)
        {
            ["名称"] = ScriptType.String,
            ["name"] = ScriptType.String,
            ["类型"] = ScriptType.String,
            ["type"] = ScriptType.String,
            ["文本"] = ScriptType.String,
            ["text"] = ScriptType.String,
            ["左"] = ScriptType.Double,
            ["x"] = ScriptType.Double,
            ["顶"] = ScriptType.Double,
            ["y"] = ScriptType.Double,
            ["宽"] = ScriptType.Double,
            ["width"] = ScriptType.Double,
            ["高"] = ScriptType.Double,
            ["height"] = ScriptType.Double,
            ["可见"] = ScriptType.Bool,
            ["visible"] = ScriptType.Bool,
            ["启用"] = ScriptType.Bool,
            ["enabled"] = ScriptType.Bool,
            ["字号"] = ScriptType.Double,
            ["fontSize"] = ScriptType.Double,
            ["粗体"] = ScriptType.Bool,
            ["bold"] = ScriptType.Bool,
            ["圆角"] = ScriptType.Double,
            ["cornerRadius"] = ScriptType.Double,
            ["开启"] = ScriptType.Bool,
            ["isOn"] = ScriptType.Bool,
            ["选中"] = ScriptType.Bool,
            ["checked"] = ScriptType.Bool,
            ["选中项"] = ScriptType.String,
            ["selectedItem"] = ScriptType.String,
            ["选项"] = ScriptType.String,
            ["items"] = ScriptType.String,
            ["分组"] = ScriptType.String,
            ["groupName"] = ScriptType.String,
            ["最小值"] = ScriptType.Double,
            ["min"] = ScriptType.Double,
            ["最大值"] = ScriptType.Double,
            ["max"] = ScriptType.Double,
            ["步长"] = ScriptType.Double,
            ["step"] = ScriptType.Double,
            ["日期"] = ScriptType.String,
            ["date"] = ScriptType.String,
            ["标签页序号"] = ScriptType.Double,
            ["tabIndex"] = ScriptType.Double,
            ["分栏面板"] = ScriptType.Double,
            ["paneIndex"] = ScriptType.Double,
            ["当前标签页"] = ScriptType.Double,
            ["selectedIndex"] = ScriptType.Double,
            ["级别"] = ScriptType.String,
            ["severity"] = ScriptType.String,
            ["表格列"] = ScriptType.String,
            ["tableColumns"] = ScriptType.String,
            ["图表类型"] = ScriptType.String,
            ["chartType"] = ScriptType.String,
            ["可编辑"] = ScriptType.Bool,
            ["gridEditable"] = ScriptType.Bool,
            ["可排序"] = ScriptType.Bool,
            ["gridSortable"] = ScriptType.Bool,
            ["值"] = ScriptType.Double,
            ["value"] = ScriptType.Double,
        };

    public static IReadOnlyList<ScriptDiagnostic> Analyze(DesignDocument document, string code)
    {
        ScriptProgram program;
        try
        {
            program = ScriptParser.Parse(code ?? string.Empty);
        }
        catch (ScriptException ex)
        {
            return [new ScriptDiagnostic(ex.Line, ex.Column, ex.Message, ScriptDiagnosticSeverity.Error)];
        }

        var variables = new HashSet<string>(StringComparer.Ordinal);
        var functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var statement in program.Statements)
        {
            Collect(statement, variables, functions);
        }

        var diagnostics = new List<ScriptDiagnostic>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in program.Statements)
        {
            Walk(statement, document, variables, functions, diagnostics, seen);
        }

        return diagnostics;
    }

    private static void Collect(
        Statement statement,
        HashSet<string> variables,
        HashSet<string> functions)
    {
        switch (statement)
        {
            case VariableDeclarationStatement declaration:
                variables.Add(declaration.Name);
                break;
            case LoopStatement loop:
                CollectBlock(loop.Body, variables, functions);
                break;
            case ForEachStatement forEach:
                variables.Add(forEach.ItemName);
                CollectBlock(forEach.Body, variables, functions);
                break;
            case FunctionDeclarationStatement function:
                functions.Add(function.Name);
                foreach (var parameter in function.Parameters)
                {
                    variables.Add(parameter.Name);
                }

                CollectBlock(function.Body, variables, functions);
                break;
            case IfStatement conditional:
                CollectBlock(conditional.Then, variables, functions);
                if (conditional.Else is not null)
                {
                    CollectBlock(conditional.Else, variables, functions);
                }

                break;
            case BlockStatement block:
                CollectBlock(block, variables, functions);
                break;
        }
    }

    private static void CollectBlock(
        BlockStatement block,
        HashSet<string> variables,
        HashSet<string> functions)
    {
        foreach (var statement in block.Statements)
        {
            Collect(statement, variables, functions);
        }
    }

    private static void Walk(
        Statement statement,
        DesignDocument document,
        HashSet<string> variables,
        HashSet<string> functions,
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> seen)
    {
        switch (statement)
        {
            case VariableDeclarationStatement declaration:
                if (declaration.HasExplicitType &&
                    declaration.Initializer is LiteralExpression literal &&
                    !IsCompatible(declaration.Type, literal.Value))
                {
                    Add(diagnostics, seen, declaration.Line,
                        $"变量 “{declaration.Name}” 期望 {DisplayType(declaration.Type)}，实际是 {literal.Value.TypeName}");
                }

                if (declaration.Initializer is not null)
                {
                    Walk(declaration.Initializer, document, variables, functions, diagnostics, seen);
                }

                break;
            case AssignmentStatement assignment:
                CheckAssignmentType(assignment, document, diagnostics, seen);
                Walk(assignment.Target, document, variables, functions, diagnostics, seen);
                Walk(assignment.Value, document, variables, functions, diagnostics, seen);
                break;
            case ExpressionStatement expression:
                Walk(expression.Expression, document, variables, functions, diagnostics, seen);
                break;
            case IfStatement conditional:
                Walk(conditional.Condition, document, variables, functions, diagnostics, seen);
                WalkBlock(conditional.Then, document, variables, functions, diagnostics, seen);
                if (conditional.Else is not null)
                {
                    WalkBlock(conditional.Else, document, variables, functions, diagnostics, seen);
                }

                break;
            case LoopStatement loop:
                Walk(loop.Count, document, variables, functions, diagnostics, seen);
                WalkBlock(loop.Body, document, variables, functions, diagnostics, seen);
                break;
            case ForEachStatement forEach:
                Walk(forEach.Source, document, variables, functions, diagnostics, seen);
                WalkBlock(forEach.Body, document, variables, functions, diagnostics, seen);
                break;
            case FunctionDeclarationStatement function:
                WalkBlock(function.Body, document, variables, functions, diagnostics, seen);
                break;
            case ReturnStatement { Value: not null } returning:
                Walk(returning.Value, document, variables, functions, diagnostics, seen);
                break;
            case BlockStatement block:
                WalkBlock(block, document, variables, functions, diagnostics, seen);
                break;
        }
    }

    private static void WalkBlock(
        BlockStatement block,
        DesignDocument document,
        HashSet<string> variables,
        HashSet<string> functions,
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> seen)
    {
        foreach (var statement in block.Statements)
        {
            Walk(statement, document, variables, functions, diagnostics, seen);
        }
    }

    private static void Walk(
        Expression expression,
        DesignDocument document,
        HashSet<string> variables,
        HashSet<string> functions,
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> seen)
    {
        switch (expression)
        {
            case VariableExpression variable:
                if (!IsKnownName(variable.Name, document, variables) &&
                    !BuiltinCalls.Contains(variable.Name) &&
                    !string.Equals(variable.Name, "真", StringComparison.Ordinal) &&
                    !string.Equals(variable.Name, "假", StringComparison.Ordinal) &&
                    !string.Equals(variable.Name, "空", StringComparison.Ordinal))
                {
                    Add(diagnostics, seen, variable.Line,
                        $"未定义的变量或控件 “{variable.Name}”");
                }

                break;

            case MemberExpression member:
                if (member.Target is VariableExpression owner &&
                    !string.Equals(owner.Name, "JSON", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(owner.Name, "HTTP", StringComparison.OrdinalIgnoreCase))
                {
                    var control = document.FindControl(owner.Name);
                    if (control is not null)
                    {
                        if (!ControlProperties.Contains(member.Member))
                        {
                            Add(diagnostics, seen, member.Line,
                                $"控件 “{owner.Name}” 没有属性 “{member.Member}”");
                        }
                    }
                    else if (!variables.Contains(owner.Name) &&
                             !string.Equals(owner.Name, "this", StringComparison.OrdinalIgnoreCase))
                    {
                        Add(diagnostics, seen, member.Line,
                            $"未定义的对象 “{owner.Name}”");
                    }
                }

                if (member.Target is not VariableExpression)
                {
                    Walk(member.Target, document, variables, functions, diagnostics, seen);
                }

                break;

            case IndexExpression index:
                Walk(index.Target, document, variables, functions, diagnostics, seen);
                Walk(index.Index, document, variables, functions, diagnostics, seen);
                break;

            case CallExpression call:
                var path = GetCallPath(call.Callee);
                if (!IsKnownCall(path, functions, document))
                {
                    Add(diagnostics, seen, call.Line, $"未知函数 “{path}”");
                }

                foreach (var argument in call.Arguments)
                {
                    Walk(argument, document, variables, functions, diagnostics, seen);
                }

                break;

            case UnaryExpression unary:
                Walk(unary.Operand, document, variables, functions, diagnostics, seen);
                break;

            case BinaryExpression binary:
                Walk(binary.Left, document, variables, functions, diagnostics, seen);
                Walk(binary.Right, document, variables, functions, diagnostics, seen);
                break;

            case ListExpression list:
                foreach (var item in list.Items)
                {
                    Walk(item, document, variables, functions, diagnostics, seen);
                }

                break;

            case DictionaryExpression dictionary:
                foreach (var pair in dictionary.Items)
                {
                    Walk(pair.Value, document, variables, functions, diagnostics, seen);
                }

                break;
        }
    }

    private static void CheckAssignmentType(
        AssignmentStatement assignment,
        DesignDocument document,
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> seen)
    {
        if (assignment.Value is not LiteralExpression literal ||
            assignment.Target is not MemberExpression
            {
                Target: VariableExpression owner
            } member)
        {
            return;
        }

        if (document.FindControl(owner.Name) is null ||
            !ControlPropertyTypes.TryGetValue(member.Member, out var expected) ||
            IsCompatible(expected, literal.Value))
        {
            return;
        }

        Add(diagnostics, seen, assignment.Line,
            $"属性 “{owner.Name}.{member.Member}” 期望 {DisplayType(expected)}，实际是 {literal.Value.TypeName}");
    }

    private static bool IsCompatible(ScriptType expected, ScriptValue value)
    {
        if (expected == ScriptType.Any || value.Kind == ScriptValueKind.Null)
        {
            return true;
        }

        return expected switch
        {
            ScriptType.Int => value.TypeName == "整数",
            ScriptType.Double => value.TypeName is "整数" or "小数",
            ScriptType.String => value.TypeName == "文本",
            ScriptType.Bool => value.TypeName == "逻辑",
            ScriptType.Json => value.TypeName is "JSON" or "列表",
            ScriptType.List => value.TypeName == "列表",
            _ => true,
        };
    }

    private static string DisplayType(ScriptType type) => type switch
    {
        ScriptType.Int => "整数",
        ScriptType.Double => "小数",
        ScriptType.String => "文本",
        ScriptType.Bool => "逻辑",
        ScriptType.Json => "JSON",
        ScriptType.List => "列表",
        _ => "任意",
    };

    private static bool IsKnownName(
        string name,
        DesignDocument document,
        HashSet<string> variables) =>
        variables.Contains(name) || document.FindControl(name) is not null;

    private static bool IsKnownCall(
        string path,
        HashSet<string> functions,
        DesignDocument document)
    {
        if (functions.Contains(path) ||
            BuiltinCalls.Contains(path) ||
            path.StartsWith("JSON.", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("HTTP.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dot = path.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var pageName = path[..dot];
        var method = path[(dot + 1)..].ToLowerInvariant();
        if (!document.Pages.Any(page =>
                string.Equals(page.Name, pageName, StringComparison.CurrentCultureIgnoreCase)))
        {
            return false;
        }

        return method is "切换" or "switch" or "打开" or "open" or "打开窗口"
            or "openwindow" or "打开弹窗" or "opendialog" or "关闭" or "close"
            or "传参" or "setparameter" or "参数" or "parameter"
            or "设置结果" or "setresult" or "结果" or "result";
    }

    private static string GetCallPath(Expression expression) => expression switch
    {
        VariableExpression variable => variable.Name,
        MemberExpression member => GetCallPath(member.Target) + "." + member.Member,
        _ => "<expression>",
    };

    private static void Add(
        List<ScriptDiagnostic> diagnostics,
        HashSet<string> seen,
        int line,
        string message)
    {
        var key = $"{line}:{message}";
        if (seen.Add(key))
        {
            diagnostics.Add(new ScriptDiagnostic(
                line,
                0,
                message,
                ScriptDiagnosticSeverity.Warning));
        }
    }
}
