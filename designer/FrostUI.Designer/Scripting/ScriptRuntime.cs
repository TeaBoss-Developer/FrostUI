using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Scripting;

public enum ScriptLogLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record ScriptValidationResult(bool Success, string Message);

/// <summary>
/// A deliberately small, event-oriented scripting runtime. It is not a general
/// purpose sandbox: scripts are authored by the person using the designer.
/// </summary>
public sealed class ScriptRuntime
{
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DesignDocument _document;
    private readonly Dictionary<string, VariableSlot> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionDefinition> _functions = new(StringComparer.OrdinalIgnoreCase);

    public ScriptRuntime(DesignDocument document)
    {
        _document = document;
    }

    public Action<string, ScriptLogLevel>? Logger { get; set; }

    public Action<string>? Toast { get; set; }

    public ScriptDebugSession? DebugSession { get; set; }

    public Func<string, string?, bool>? Navigate { get; set; }

    public Func<string, string?>? GetPageParameter { get; set; }

    public Action<string, string>? SetPageParameter { get; set; }

    public Action<string>? SetPageResult { get; set; }

    public Func<string, string?>? GetProjectValue { get; set; }

    public Action<string, string>? SetProjectValue { get; set; }

    public bool IsRunning { get; private set; }

    public bool TryGetGlobalValue(string name, out string value)
    {
        if (_globals.TryGetValue(name, out var slot))
        {
            value = slot.Value.ToText();
            return true;
        }

        value = string.Empty;
        return false;
    }

    public static ScriptValidationResult Validate(string code)
    {
        try
        {
            _ = ScriptParser.Parse(code ?? string.Empty);
            return new ScriptValidationResult(true, "语法正确");
        }
        catch (ScriptException ex)
        {
            return new ScriptValidationResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            return new ScriptValidationResult(false, ex.Message);
        }
    }

    public async Task<bool> RunAsync(
        DesignControl? control,
        string eventName,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return false;
        }

        IsRunning = true;
        var targetName = control is null ? "窗口" : control.Name;
        try
        {
            var program = ScriptParser.Parse(code ?? string.Empty);
            _functions.Clear();
            foreach (var function in program.Statements.OfType<FunctionDeclarationStatement>())
            {
                _functions[function.Name] = new FunctionDefinition(function);
            }

            var context = new ExecutionContext(control, cancellationToken);
            await ExecuteStatementsAsync(
                program.Statements.Where(x => x is not FunctionDeclarationStatement).ToArray(),
                context);
            return true;
        }
        catch (ReturnSignal)
        {
            ReportError($"{targetName}.{eventName} · “返回”只能写在函数内部");
            return false;
        }
        catch (ScriptException ex)
        {
            ReportError($"{targetName}.{eventName} · {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            ReportError($"{targetName}.{eventName} · 脚本已取消");
            return false;
        }
        catch (Exception ex)
        {
            ReportError($"{targetName}.{eventName} · {ex.Message}");
            return false;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ExecuteStatementsAsync(
        IReadOnlyList<Statement> statements,
        ExecutionContext context)
    {
        foreach (var statement in statements)
        {
            context.Cancellation.ThrowIfCancellationRequested();
            if (DebugSession is not null)
            {
                await DebugSession.BeforeStatementAsync(
                    CreateDebugSnapshot(statement.Line, context),
                    context.Cancellation);
            }

            context.Step();
            await ExecuteStatementAsync(statement, context);
        }
    }

    private DebugSnapshot CreateDebugSnapshot(int line, ExecutionContext context)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in _globals)
        {
            variables[pair.Key] = pair.Value.Value.ToText();
        }

        foreach (var scope in context.AllLocals)
        {
            foreach (var pair in scope)
            {
                variables[pair.Key] = pair.Value.Value.ToText();
            }
        }

        return new DebugSnapshot(line, variables, context.CallStack.ToArray());
    }

    private async Task ExecuteStatementAsync(Statement statement, ExecutionContext context)
    {
        switch (statement)
        {
            case VariableDeclarationStatement declaration:
                await ExecuteDeclarationAsync(declaration, context);
                break;

            case AssignmentStatement assignment:
                await ExecuteAssignmentAsync(assignment, context);
                break;

            case IfStatement conditional:
                var condition = await EvaluateAsync(conditional.Condition, context);
                if (condition.IsTruthy())
                {
                    await ExecuteStatementsAsync(conditional.Then.Statements, context);
                }
                else if (conditional.Else is not null)
                {
                    await ExecuteStatementsAsync(conditional.Else.Statements, context);
                }

                break;

            case LoopStatement loop:
                var count = (int)Math.Truncate((await EvaluateAsync(loop.Count, context)).AsNumber());
                context.LoopDepth++;
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        context.Step();
                        try
                        {
                            await ExecuteStatementsAsync(loop.Body.Statements, context);
                        }
                        catch (ContinueSignal)
                        {
                            continue;
                        }
                        catch (BreakSignal)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    context.LoopDepth--;
                }

                break;

            case ForEachStatement forEach:
                var source = await EvaluateAsync(forEach.Source, context);
                if (source.AsJson() is not JsonArray array)
                {
                    throw new ScriptException("遍历需要列表/JSON 数组", forEach.Line, 0);
                }

                context.LoopDepth++;
                try
                {
                    foreach (var item in array)
                    {
                        context.Locals[forEach.ItemName] = new VariableSlot(
                            ScriptType.Any,
                            ScriptValue.FromJson(item));
                        context.Step();
                        try
                        {
                            await ExecuteStatementsAsync(forEach.Body.Statements, context);
                        }
                        catch (ContinueSignal)
                        {
                            continue;
                        }
                        catch (BreakSignal)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    context.LoopDepth--;
                }

                break;

            case FunctionDeclarationStatement:
                break;

            case BreakStatement breakStatement:
                if (context.LoopDepth == 0)
                {
                    throw new ScriptException("“跳出”只能在循环内使用", breakStatement.Line, 0);
                }

                throw new BreakSignal();

            case ContinueStatement continueStatement:
                if (context.LoopDepth == 0)
                {
                    throw new ScriptException("“继续”只能在循环内使用", continueStatement.Line, 0);
                }

                throw new ContinueSignal();

            case ReturnStatement returnStatement:
                var returnValue = returnStatement.Value is null
                    ? ScriptValue.Null
                    : await EvaluateAsync(returnStatement.Value, context);
                throw new ReturnSignal(returnValue);

            case ExpressionStatement expressionStatement:
                await EvaluateAsync(expressionStatement.Expression, context);
                break;

            case BlockStatement block:
                await ExecuteStatementsAsync(block.Statements, context);
                break;

            default:
                throw new ScriptException("未知语句", statement.Line, 0);
        }
    }

    private async Task ExecuteDeclarationAsync(
        VariableDeclarationStatement declaration,
        ExecutionContext context)
    {
        if (declaration.IsGlobal)
        {
            if (_globals.TryGetValue(declaration.Name, out var existing))
            {
                // Typed global declarations initialize once. An untyped global
                // statement without a type is an update, so a common pattern like
                // "全局 计数 = 计数 + 1" works on every event.
                if (declaration.HasExplicitType || declaration.Initializer is null)
                {
                    return;
                }

                var updated = await EvaluateAsync(declaration.Initializer, context);
                existing.Value = Coerce(updated, existing.Type, declaration.Name, declaration.Line);
                return;
            }

            var value = declaration.Initializer is null
                ? ScriptValue.DefaultFor(declaration.Type)
                : await EvaluateAsync(declaration.Initializer, context);
            _globals[declaration.Name] = new VariableSlot(
                declaration.Type,
                Coerce(value, declaration.Type, declaration.Name, declaration.Line));
            return;
        }

        var localValue = declaration.Initializer is null
            ? ScriptValue.DefaultFor(declaration.Type)
            : await EvaluateAsync(declaration.Initializer, context);
        context.Locals[declaration.Name] = new VariableSlot(
            declaration.Type,
            Coerce(localValue, declaration.Type, declaration.Name, declaration.Line));
    }

    private async Task ExecuteAssignmentAsync(
        AssignmentStatement assignment,
        ExecutionContext context)
    {
        var value = await EvaluateAsync(assignment.Value, context);
        await AssignAsync(assignment.Target, value, context, assignment.Line);
    }

    private async Task AssignAsync(
        Expression target,
        ScriptValue value,
        ExecutionContext context,
        int line)
    {
        switch (target)
        {
            case VariableExpression variable:
                AssignVariable(variable.Name, value, context, line);
                return;

            case MemberExpression member:
                var owner = await EvaluateAsync(member.Target, context);
                SetMember(owner, member.Member, value, line);
                return;

            case IndexExpression index:
                var indexedOwner = await EvaluateAsync(index.Target, context);
                var indexValue = await EvaluateAsync(index.Index, context);
                SetIndex(indexedOwner, indexValue, value, line);
                return;

            default:
                throw new ScriptException("赋值目标无效", line, 0);
        }
    }

    private void AssignVariable(string name, ScriptValue value, ExecutionContext context, int line)
    {
        if (context.TryGetLocal(name, out var local))
        {
            local.Value = Coerce(value, local.Type, name, line);
            return;
        }

        if (_globals.TryGetValue(name, out var global))
        {
            global.Value = Coerce(value, global.Type, name, line);
            return;
        }

        context.Locals[name] = new VariableSlot(ScriptType.Any, value);
    }

    private static ScriptValue Coerce(ScriptValue value, ScriptType type, string name, int line)
    {
        try
        {
            return ScriptValue.Coerce(value, type);
        }
        catch (ScriptValueException ex)
        {
            throw new ScriptException($"变量 “{name}”：{ex.Message}", line, 0);
        }
    }

    private async Task<ScriptValue> EvaluateAsync(Expression expression, ExecutionContext context)
    {
        context.Step();
        context.Cancellation.ThrowIfCancellationRequested();
        try
        {
            return await EvaluateCoreAsync(expression, context);
        }
        catch (ScriptValueException ex)
        {
            throw new ScriptException(ex.Message, expression.Line, 0);
        }
    }

    private async Task<ScriptValue> EvaluateCoreAsync(Expression expression, ExecutionContext context)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                return literal.Value;

            case VariableExpression variable:
                if (context.TryGetLocal(variable.Name, out var local))
                {
                    return local.Value;
                }

                if (_globals.TryGetValue(variable.Name, out var global))
                {
                    return global.Value;
                }

                var control = _document.FindControl(variable.Name);
                if (control is not null)
                {
                    return ScriptValue.FromControl(control);
                }

                throw new ScriptException($"未定义的变量或控件 “{variable.Name}”", variable.Line, 0);

            case MemberExpression member:
                var owner = await EvaluateAsync(member.Target, context);
                return GetMember(owner, member.Member, member.Line);

            case IndexExpression index:
                var indexedOwner = await EvaluateAsync(index.Target, context);
                var indexValue = await EvaluateAsync(index.Index, context);
                return GetIndex(indexedOwner, indexValue, index.Line);

            case UnaryExpression unary:
                var operand = await EvaluateAsync(unary.Operand, context);
                return unary.Operator switch
                {
                    TokenType.Bang => ScriptValue.FromBool(!operand.IsTruthy()),
                    TokenType.Minus => ScriptValue.FromNumber(-operand.AsNumber()),
                    _ => operand,
                };

            case BinaryExpression binary:
                return await EvaluateBinaryAsync(binary, context);

            case ListExpression list:
                var array = new JsonArray();
                foreach (var item in list.Items)
                {
                    array.Add(ScriptValue.ToJsonNode(await EvaluateAsync(item, context)));
                }

                return ScriptValue.FromJson(array);

            case DictionaryExpression dictionary:
                var jsonObject = new JsonObject();
                foreach (var pair in dictionary.Items)
                {
                    jsonObject[pair.Key] = ScriptValue.ToJsonNode(await EvaluateAsync(pair.Value, context));
                }

                return ScriptValue.FromJson(jsonObject);

            case CallExpression call:
                return await InvokeAsync(call, context);

            default:
                throw new ScriptException("无法计算的表达式", expression.Line, 0);
        }
    }

    private async Task<ScriptValue> EvaluateBinaryAsync(
        BinaryExpression binary,
        ExecutionContext context)
    {
        var left = await EvaluateAsync(binary.Left, context);

        if (binary.Operator == TokenType.AndAnd)
        {
            if (!left.IsTruthy())
            {
                return ScriptValue.FromBool(false);
            }

            var right = await EvaluateAsync(binary.Right, context);
            return ScriptValue.FromBool(right.IsTruthy());
        }

        if (binary.Operator == TokenType.OrOr)
        {
            if (left.IsTruthy())
            {
                return ScriptValue.FromBool(true);
            }

            var right = await EvaluateAsync(binary.Right, context);
            return ScriptValue.FromBool(right.IsTruthy());
        }

        var rhs = await EvaluateAsync(binary.Right, context);
        switch (binary.Operator)
        {
            case TokenType.Plus:
                if (left.Kind == ScriptValueKind.String || rhs.Kind == ScriptValueKind.String)
                {
                    return ScriptValue.FromText(left.ToText() + rhs.ToText());
                }

                return ScriptValue.FromNumber(left.AsNumber() + rhs.AsNumber());

            case TokenType.Minus:
                return ScriptValue.FromNumber(left.AsNumber() - rhs.AsNumber());

            case TokenType.Star:
                return ScriptValue.FromNumber(left.AsNumber() * rhs.AsNumber());

            case TokenType.Slash:
                var divisor = rhs.AsNumber();
                if (Math.Abs(divisor) < 0.0000001)
                {
                    throw new ScriptValueException("不能除以 0");
                }

                return ScriptValue.FromNumber(left.AsNumber() / divisor);

            case TokenType.Percent:
                var modulo = rhs.AsNumber();
                if (Math.Abs(modulo) < 0.0000001)
                {
                    throw new ScriptValueException("不能对 0 取余");
                }

                return ScriptValue.FromNumber(left.AsNumber() % modulo);

            case TokenType.Equal:
                return ScriptValue.FromBool(left.ContentEquals(rhs));

            case TokenType.NotEqual:
                return ScriptValue.FromBool(!left.ContentEquals(rhs));

            case TokenType.Less:
                return ScriptValue.FromBool(Compare(left, rhs) < 0);

            case TokenType.LessEqual:
                return ScriptValue.FromBool(Compare(left, rhs) <= 0);

            case TokenType.Greater:
                return ScriptValue.FromBool(Compare(left, rhs) > 0);

            case TokenType.GreaterEqual:
                return ScriptValue.FromBool(Compare(left, rhs) >= 0);

            default:
                throw new ScriptValueException($"不支持运算符 {binary.Operator}");
        }
    }

    private static int Compare(ScriptValue left, ScriptValue right)
    {
        if (left.Kind == ScriptValueKind.String && right.Kind == ScriptValueKind.String)
        {
            return string.CompareOrdinal(left.Text, right.Text);
        }

        return left.AsNumber().CompareTo(right.AsNumber());
    }

    private ScriptValue GetMember(ScriptValue owner, string member, int line)
    {
        switch (owner.Kind)
        {
            case ScriptValueKind.Control:
                return GetControlProperty(owner.AsControl(), member, line);

            case ScriptValueKind.Json:
                return GetJsonProperty(owner, member, line);

            case ScriptValueKind.String when member is "长度" or "length":
                return ScriptValue.FromNumber(owner.Text.Length);

            default:
                throw new ScriptValueException($"类型 {owner.TypeName} 没有属性 “{member}”");
        }
    }

    private ScriptValue GetControlProperty(DesignControl control, string property, int line)
    {
        return property switch
        {
            "名称" or "name" => ScriptValue.FromText(control.Name),
            "类型" or "type" => ScriptValue.FromText(control.KindTitle),
            "文本" or "text" => ScriptValue.FromText(control.Text),
            "左" or "x" => ScriptValue.FromNumber(control.X),
            "顶" or "y" => ScriptValue.FromNumber(control.Y),
            "宽" or "width" => ScriptValue.FromNumber(control.Width),
            "高" or "height" => ScriptValue.FromNumber(control.Height),
            "可见" or "visible" => ScriptValue.FromBool(control.IsVisible),
            "启用" or "enabled" => ScriptValue.FromBool(control.IsEnabled),
            "字号" or "fontSize" => ScriptValue.FromNumber(control.FontSize),
            "粗体" or "bold" => ScriptValue.FromBool(control.IsBold),
            "圆角" or "cornerRadius" => ScriptValue.FromNumber(control.CornerRadius),
            "开启" or "isOn" or "选中" or "checked" => ScriptValue.FromBool(control.IsOn),
            "选中项" or "selectedItem" => ScriptValue.FromText(control.SelectedItem),
            "选项" or "items" => ScriptValue.FromText(control.ListItems),
            "标签页序号" or "tabIndex" => ScriptValue.FromNumber(control.TabIndex),
            "分栏面板" or "paneIndex" => ScriptValue.FromNumber(control.PaneIndex),
            "当前标签页" or "selectedIndex" => ScriptValue.FromNumber(control.SelectedIndex),
            "级别" or "severity" => ScriptValue.FromText(control.Severity),
            "表格列" or "tableColumns" => ScriptValue.FromText(control.TableColumns),
            "图表类型" or "chartType" => ScriptValue.FromText(control.ChartType),
            "可编辑" or "gridEditable" => ScriptValue.FromBool(control.GridEditable),
            "可排序" or "gridSortable" => ScriptValue.FromBool(control.GridSortable),
            "分组" or "groupName" => ScriptValue.FromText(control.GroupName),
            "最小值" or "min" => ScriptValue.FromNumber(control.Minimum),
            "最大值" or "max" => ScriptValue.FromNumber(control.Maximum),
            "步长" or "step" => ScriptValue.FromNumber(control.Step),
            "日期" or "date" => ScriptValue.FromText(control.Text),
            "值" or "value" => control.Kind is ControlKind.ToggleSwitch
                or ControlKind.CheckBox or ControlKind.RadioButton
                ? ScriptValue.FromBool(control.IsOn)
                : ScriptValue.FromNumber(control.Value),
            _ => throw new ScriptValueException($"控件 “{control.Name}” 没有属性 “{property}”"),
        };
    }

    private ScriptValue GetJsonProperty(ScriptValue owner, string member, int line)
    {
        var json = owner.Json;
        if (member is "长度" or "length")
        {
            return json switch
            {
                JsonArray array => ScriptValue.FromNumber(array.Count),
                JsonObject obj => ScriptValue.FromNumber(obj.Count),
                JsonValue value when value.TryGetValue<string>(out var text) => ScriptValue.FromNumber(text.Length),
                _ => throw new ScriptValueException("当前 JSON 值没有长度"),
            };
        }

        if (json is JsonObject jsonObject &&
            jsonObject.TryGetPropertyValue(member, out var node))
        {
            return ScriptValue.FromJson(node);
        }

        throw new ScriptValueException($"JSON 中没有字段 “{member}”");
    }

    private static void SetMember(ScriptValue owner, string member, ScriptValue value, int line)
    {
        if (owner.Kind != ScriptValueKind.Control)
        {
            throw new ScriptValueException("只有控件属性可以在脚本中赋值");
        }

        var control = owner.AsControl();
        switch (member)
        {
            case "文本" or "text":
                control.Text = value.ToText();
                return;
            case "左" or "x":
                control.X = value.AsNumber();
                return;
            case "顶" or "y":
                control.Y = value.AsNumber();
                return;
            case "宽" or "width":
                control.Width = value.AsNumber();
                return;
            case "高" or "height":
                control.Height = value.AsNumber();
                return;
            case "可见" or "visible":
                control.IsVisible = value.AsBoolean();
                return;
            case "启用" or "enabled":
                control.IsEnabled = value.AsBoolean();
                return;
            case "字号" or "fontSize":
                control.FontSize = value.AsNumber();
                return;
            case "粗体" or "bold":
                control.IsBold = value.AsBoolean();
                return;
            case "圆角" or "cornerRadius":
                control.CornerRadius = value.AsNumber();
                return;
            case "开启" or "isOn" or "选中" or "checked":
                control.IsOn = value.AsBoolean();
                return;
            case "选中项" or "selectedItem":
                control.SelectedItem = value.ToText();
                return;
            case "选项" or "items":
                control.ListItems = value.ToText();
                return;
            case "标签页序号" or "tabIndex":
                control.TabIndex = (int)value.AsNumber();
                return;
            case "分栏面板" or "paneIndex":
                control.PaneIndex = (int)value.AsNumber();
                return;
            case "当前标签页" or "selectedIndex":
                control.SelectedIndex = (int)value.AsNumber();
                return;
            case "级别" or "severity":
                control.Severity = value.ToText();
                return;
            case "表格列" or "tableColumns":
                control.TableColumns = value.ToText();
                return;
            case "图表类型" or "chartType":
                control.ChartType = value.ToText();
                return;
            case "可编辑" or "gridEditable":
                control.GridEditable = value.AsBoolean();
                return;
            case "可排序" or "gridSortable":
                control.GridSortable = value.AsBoolean();
                return;
            case "分组" or "groupName":
                control.GroupName = value.ToText();
                return;
            case "最小值" or "min":
                control.Minimum = value.AsNumber();
                return;
            case "最大值" or "max":
                control.Maximum = value.AsNumber();
                return;
            case "步长" or "step":
                control.Step = value.AsNumber();
                return;
            case "日期" or "date":
                control.Text = value.ToText();
                return;
            case "值" or "value":
                if (control.Kind is ControlKind.ToggleSwitch
                    or ControlKind.CheckBox or ControlKind.RadioButton)
                {
                    control.IsOn = value.AsBoolean();
                }
                else
                {
                    control.Value = value.AsNumber();
                }

                return;
            case "名称" or "name":
            case "类型" or "type":
                throw new ScriptValueException($"属性 “{member}” 是只读的");
            default:
                throw new ScriptValueException($"控件 “{control.Name}” 没有可写属性 “{member}”");
        }
    }

    private ScriptValue GetIndex(ScriptValue owner, ScriptValue index, int line)
    {
        if (owner.Kind != ScriptValueKind.Json)
        {
            throw new ScriptValueException("只有 JSON 支持索引访问");
        }

        var json = owner.Json;
        if (index.Kind == ScriptValueKind.Number)
        {
            var arrayIndex = (int)Math.Truncate(index.Number);
            if (json is JsonArray array && arrayIndex >= 0 && arrayIndex < array.Count)
            {
                return ScriptValue.FromJson(array[arrayIndex]);
            }

            throw new ScriptValueException($"JSON 数组索引越界：{arrayIndex}");
        }

        if (json is JsonObject obj && obj.TryGetPropertyValue(index.ToText(), out var node))
        {
            return ScriptValue.FromJson(node);
        }

        throw new ScriptValueException($"JSON 中没有键 “{index.ToText()}”");
    }

    private static void SetIndex(ScriptValue owner, ScriptValue index, ScriptValue value, int line)
    {
        throw new ScriptValueException("JSON 暂不支持在脚本中写回；请重新构造 JSON");
    }

    private async Task<ScriptValue> InvokeFunctionAsync(
        FunctionDefinition function,
        IReadOnlyList<ScriptValue> arguments,
        ExecutionContext context,
        int line)
    {
        var parameters = function.Declaration.Parameters;
        if (parameters.Count != arguments.Count)
        {
            throw new ScriptException(
                $"函数 “{function.Declaration.Name}” 需要 {parameters.Count} 个参数，实际 {arguments.Count} 个",
                line,
                0);
        }

        if (context.CallDepth >= 64)
        {
            throw new ScriptException("函数调用层级过深", line, 0);
        }

        context.PushScope();
        context.CallDepth++;
        context.CallStack.Add(function.Declaration.Name);
        try
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                context.Locals[parameter.Name] = new VariableSlot(
                    parameter.Type,
                    Coerce(arguments[i], parameter.Type, parameter.Name, line));
            }

            try
            {
                await ExecuteStatementsAsync(function.Declaration.Body.Statements, context);
                return ScriptValue.Null;
            }
            catch (ReturnSignal signal)
            {
                return signal.Value;
            }
        }
        finally
        {
            context.CallDepth--;
            if (context.CallStack.Count > 0)
            {
                context.CallStack.RemoveAt(context.CallStack.Count - 1);
            }

            context.PopScope();
        }
    }

    private string NormalizePageAlias(string path)
    {
        var dot = path.LastIndexOf('.');
        if (dot <= 0)
        {
            return path;
        }

        var pageName = path[..dot];
        var method = path[(dot + 1)..].ToLowerInvariant();
        if (!_document.Pages.Any(page =>
                string.Equals(page.Name, pageName, StringComparison.CurrentCultureIgnoreCase)))
        {
            return path;
        }

        return method switch
        {
            "切换" or "switch" => "页面.切换",
            "打开" or "open" => "页面.打开",
            "打开窗口" or "openwindow" => "页面.打开窗口",
            "打开弹窗" or "opendialog" => "页面.打开弹窗",
            "关闭" or "close" => "页面.关闭",
            "传参" or "setparameter" => "页面.传参",
            "参数" or "parameter" => "页面.参数",
            "设置结果" or "setresult" => "页面.设置结果",
            "结果" or "result" => "页面.结果",
            _ => path,
        };
    }

    private async Task<ScriptValue> InvokeAsync(CallExpression call, ExecutionContext context)
    {
        var path = NormalizePageAlias(GetCallPath(call.Callee, call.Line));
        var arguments = new List<ScriptValue>();
        foreach (var argument in call.Arguments)
        {
            arguments.Add(await EvaluateAsync(argument, context));
        }

        if (_functions.TryGetValue(path, out var function))
        {
            return await InvokeFunctionAsync(function, arguments, context, call.Line);
        }

        var normalized = path.ToLowerInvariant();
        switch (normalized)
        {
            case "页面.切换":
            case "page.goto":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromBool(
                    Navigate?.Invoke("Switch", arguments[0].ToText()) == true);

            case "页面.打开窗口":
            case "page.openwindow":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromBool(
                    Navigate?.Invoke("Window", arguments[0].ToText()) == true);

            case "页面.打开弹窗":
            case "page.opendialog":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromBool(
                    Navigate?.Invoke("Dialog", arguments[0].ToText()) == true);

            case "页面.关闭":
            case "page.close":
                Navigate?.Invoke("Close", null);
                return ScriptValue.Null;

            case "页面.参数":
            case "page.parameter":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromText(
                    GetPageParameter?.Invoke(arguments[0].ToText()) ?? string.Empty);

            case "页面.打开":
            case "page.open":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromBool(
                    Navigate?.Invoke("Open", arguments[0].ToText()) == true);

            case "页面.传参":
            case "page.setparameter":
                RequireCount(arguments, 2, path);
                SetPageParameter?.Invoke(arguments[0].ToText(), arguments[1].ToText());
                return ScriptValue.Null;

            case "页面.返回":
            case "页面.设置结果":
            case "page.return":
            case "page.setresult":
                RequireCount(arguments, 1, path);
                SetPageResult?.Invoke(arguments[0].ToText());
                return ScriptValue.Null;

            case "页面.结果":
            case "page.result":
                return ScriptValue.FromText(
                    GetPageParameter?.Invoke("结果") ?? string.Empty);

            case "项目.获取":
            case "project.get":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromText(
                    GetProjectValue?.Invoke(arguments[0].ToText()) ?? string.Empty);

            case "项目.设置":
            case "project.set":
                RequireCount(arguments, 2, path);
                SetProjectValue?.Invoke(arguments[0].ToText(), arguments[1].ToText());
                return ScriptValue.Null;

            case "输出":
            case "log":
                Logger?.Invoke(string.Join(" ", arguments.Select(x => x.ToText())), ScriptLogLevel.Info);
                return ScriptValue.Null;

            case "提示":
            case "toast":
                var message = arguments.Count == 0 ? string.Empty : arguments[0].ToText();
                Logger?.Invoke(message, ScriptLogLevel.Info);
                Toast?.Invoke(message);
                return ScriptValue.Null;

            case "长度":
            case "len":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromNumber(Length(arguments[0]));

            case "类型":
            case "type":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromText(arguments[0].TypeName);

            case "文本":
            case "str":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromText(arguments[0].ToText());

            case "整数":
            case "int":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromNumber(Math.Truncate(arguments[0].AsNumber()));

            case "小数":
            case "double":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromNumber(arguments[0].AsNumber());

            case "逻辑":
            case "bool":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromBool(arguments[0].AsBoolean());

            case "json.解析":
            case "json.parse":
                RequireCount(arguments, 1, path);
                return ParseJson(arguments[0].ToText(), call.Line);

            case "json.取值":
            case "json.get":
                RequireCount(arguments, 2, path);
                return ScriptValue.FromJson(
                    SelectJsonPath(arguments[0].AsJson(), arguments[1].ToText(), call.Line));

            case "json.文本":
            case "json.stringify":
                RequireCount(arguments, 1, path);
                var pretty = arguments.Count > 1 && arguments[1].AsBoolean();
                return ScriptValue.FromText(
                    arguments[0].AsJson().ToJsonString(pretty ? PrettyJsonOptions : null));

            case "http.获取":
            case "http.get":
                RequireCount(arguments, 1, path);
                return ScriptValue.FromText(await GetStringAsync(arguments[0].ToText(), context));

            case "http.获取json":
            case "http.getjson":
                RequireCount(arguments, 1, path);
                return ParseJson(await GetStringAsync(arguments[0].ToText(), context), call.Line);

            case "http.提交json":
            case "http.postjson":
                RequireCount(arguments, 2, path);
                return ScriptValue.FromText(await PostJsonAsync(
                    arguments[0].ToText(),
                    arguments[1].ToText(),
                    context));

            case "随机":
            case "random":
                RequireCount(arguments, 2, path);
                var min = (int)Math.Truncate(arguments[0].AsNumber());
                var max = (int)Math.Truncate(arguments[1].AsNumber());
                if (max < min)
                {
                    (min, max) = (max, min);
                }

                return ScriptValue.FromNumber(Random.Shared.Next(min, max + 1));

            case "等待":
            case "sleep":
                RequireCount(arguments, 1, path);
                var delay = Math.Clamp((int)Math.Truncate(arguments[0].AsNumber()), 0, 30_000);
                await Task.Delay(delay, context.Cancellation);
                return ScriptValue.Null;

            default:
                throw new ScriptException($"未知函数 “{path}”", call.Line, 0);
        }
    }

    private static string GetCallPath(Expression expression, int line)
    {
        return expression switch
        {
            VariableExpression variable => variable.Name,
            MemberExpression member => GetCallPath(member.Target, line) + "." + member.Member,
            _ => throw new ScriptException("只能调用内置函数", line, 0),
        };
    }

    private static void RequireCount(IReadOnlyList<ScriptValue> arguments, int count, string function)
    {
        if (arguments.Count < count)
        {
            throw new ScriptValueException($"函数 {function} 需要至少 {count} 个参数");
        }
    }

    private static double Length(ScriptValue value) => value.Kind switch
    {
        ScriptValueKind.String => value.Text.Length,
        ScriptValueKind.Json when value.Json is JsonArray array => array.Count,
        ScriptValueKind.Json when value.Json is JsonObject obj => obj.Count,
        _ => throw new ScriptValueException($"类型 {value.TypeName} 没有长度"),
    };

    private static ScriptValue ParseJson(string text, int line)
    {
        try
        {
            var node = JsonNode.Parse(text);
            return ScriptValue.FromJson(node);
        }
        catch (JsonException ex)
        {
            throw new ScriptException($"JSON 解析失败：{ex.Message}", line, 0);
        }
    }

    private static JsonNode SelectJsonPath(JsonNode root, string path, int line)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return root;
        }

        var current = root;
        foreach (var step in ParseJsonPath(path, line))
        {
            if (step.Index is { } index)
            {
                if (current is not JsonArray array || index < 0 || index >= array.Count)
                {
                    throw new ScriptException($"JSON 路径 “{path}” 的数组索引无效", line, 0);
                }

                current = array[index] ?? throw new ScriptException($"JSON 路径 “{path}” 的值是空", line, 0);
            }
            else
            {
                if (current is not JsonObject obj ||
                    !obj.TryGetPropertyValue(step.Property!, out var next) ||
                    next is null)
                {
                    throw new ScriptException($"JSON 路径 “{path}” 不存在", line, 0);
                }

                current = next;
            }
        }

        return current;
    }

    private static IEnumerable<PathStep> ParseJsonPath(string path, int line)
    {
        var index = 0;
        while (index < path.Length)
        {
            while (index < path.Length && (path[index] == '.' || path[index] == ' '))
            {
                index++;
            }

            if (index >= path.Length)
            {
                break;
            }

            if (path[index] == '[')
            {
                var closing = path.IndexOf(']', index + 1);
                if (closing < 0 ||
                    !int.TryParse(path[(index + 1)..closing], out var arrayIndex))
                {
                    throw new ScriptException($"JSON 路径 “{path}” 的数组索引格式错误", line, 0);
                }

                yield return new PathStep(null, arrayIndex);
                index = closing + 1;
                continue;
            }

            var start = index;
            while (index < path.Length && path[index] != '.' && path[index] != '[')
            {
                index++;
            }

            var property = path[start..index].Trim();
            if (property.Length == 0)
            {
                throw new ScriptException($"JSON 路径 “{path}” 格式错误", line, 0);
            }

            yield return new PathStep(property, null);
        }
    }

    private static async Task<string> GetStringAsync(string url, ExecutionContext context)
    {
        var uri = ParseUrl(url);
        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, context.Cancellation);
            var content = await response.Content.ReadAsStringAsync(context.Cancellation);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScriptValueException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}：{Truncate(content)}");
            }

            return content;
        }
        catch (ScriptValueException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new ScriptValueException("HTTP 请求超时或被取消");
        }
        catch (Exception ex)
        {
            throw new ScriptValueException($"HTTP 请求失败：{ex.Message}");
        }
    }

    private static async Task<string> PostJsonAsync(string url, string body, ExecutionContext context)
    {
        var uri = ParseUrl(url);
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(uri, content, context.Cancellation);
            var result = await response.Content.ReadAsStringAsync(context.Cancellation);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScriptValueException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}：{Truncate(result)}");
            }

            return result;
        }
        catch (ScriptValueException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new ScriptValueException("HTTP 请求超时或被取消");
        }
        catch (Exception ex)
        {
            throw new ScriptValueException($"HTTP 请求失败：{ex.Message}");
        }
    }

    private static Uri ParseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ScriptValueException($"无效的 URL：{url}（只支持 http/https）");
        }

        return uri;
    }

    private static string Truncate(string text) =>
        text.Length <= 160 ? text : text[..160] + "…";

    private void ReportError(string message)
    {
        Logger?.Invoke($"[错误] {message}", ScriptLogLevel.Error);
        Toast?.Invoke(message);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FrostUI.Designer/1.0");
        return client;
    }

    private sealed class ExecutionContext
    {
        private const int MaxSteps = 100_000;
        private readonly Stack<Dictionary<string, VariableSlot>> _scopes = new();

        public ExecutionContext(DesignControl? source, CancellationToken cancellation)
        {
            Source = source;
            Cancellation = cancellation;
            _scopes.Push(new Dictionary<string, VariableSlot>(StringComparer.Ordinal));
        }

        public DesignControl? Source { get; }

        public CancellationToken Cancellation { get; }

        public Dictionary<string, VariableSlot> Locals => _scopes.Peek();

        public IEnumerable<Dictionary<string, VariableSlot>> AllLocals => _scopes;

        public List<string> CallStack { get; } = [];

        public int CallDepth { get; set; }

        public int LoopDepth { get; set; }

        public int Steps { get; private set; }

        public void PushScope() =>
            _scopes.Push(new Dictionary<string, VariableSlot>(StringComparer.Ordinal));

        public void PopScope()
        {
            if (_scopes.Count > 1)
            {
                _scopes.Pop();
            }
        }

        public bool TryGetLocal(string name, out VariableSlot slot)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out slot!))
                {
                    return true;
                }
            }

            slot = null!;
            return false;
        }

        public void Step()
        {
            if (++Steps > MaxSteps)
            {
                throw new ScriptException("脚本执行步骤过多，已中止", 0, 0);
            }
        }
    }

    private sealed class FunctionDefinition
    {
        public FunctionDefinition(FunctionDeclarationStatement declaration)
        {
            Declaration = declaration;
        }

        public FunctionDeclarationStatement Declaration { get; }
    }

    private sealed class ReturnSignal : Exception
    {
        public ReturnSignal(ScriptValue value)
        {
            Value = value;
        }

        public ScriptValue Value { get; }
    }

    private sealed class VariableSlot
    {
        public VariableSlot(ScriptType type, ScriptValue value)
        {
            Type = type;
            Value = value;
        }

        public ScriptType Type { get; }

        public ScriptValue Value { get; set; }
    }

    private sealed class BreakSignal : Exception;

    private sealed class ContinueSignal : Exception;

    private readonly record struct PathStep(string? Property, int? Index);
}
