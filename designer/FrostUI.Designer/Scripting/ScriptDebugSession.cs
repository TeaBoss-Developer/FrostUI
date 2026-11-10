namespace FrostUI.Designer.Scripting;

public sealed record DebugSnapshot(
    int Line,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> CallStack);

/// <summary>
/// Cooperative statement-level debugger. The interpreter awaits this session
/// before each statement; the UI resumes it with Continue or Step.
/// </summary>
public sealed class ScriptDebugSession
{
    private TaskCompletionSource<bool>? _resume;

    public Dictionary<int, BreakpointSpec> Breakpoints { get; } = [];

    public bool StepMode { get; set; } = true;

    public Func<DebugSnapshot, Task>? OnPause { get; set; }

    public Action<DebugSnapshot, BreakpointSpec>? OnLog { get; set; }

    public void SetBreakpoints(IEnumerable<BreakpointSpec> specs)
    {
        Breakpoints.Clear();
        foreach (var spec in specs)
        {
            Breakpoints[spec.Line] = spec;
        }
    }

    internal async Task BeforeStatementAsync(
        DebugSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var shouldPause = StepMode;
        if (!shouldPause &&
            Breakpoints.TryGetValue(snapshot.Line, out var breakpoint))
        {
            if (!BreakpointCondition.Matches(breakpoint.Condition, snapshot))
            {
                return;
            }

            if (breakpoint.Log)
            {
                OnLog?.Invoke(snapshot, breakpoint);
                return;
            }

            shouldPause = true;
        }

        if (!shouldPause)
        {
            return;
        }

        StepMode = false;
        if (OnPause is null)
        {
            return;
        }

        _resume = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            () => _resume.TrySetCanceled(cancellationToken));
        await OnPause(snapshot);
        await _resume.Task;
    }

    public void Continue()
    {
        StepMode = false;
        _resume?.TrySetResult(true);
    }

    public void Step()
    {
        StepMode = true;
        _resume?.TrySetResult(true);
    }

    public void Stop()
    {
        _resume?.TrySetCanceled();
    }
}
