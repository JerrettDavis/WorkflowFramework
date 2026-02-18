namespace WorkflowFramework.Testing;

/// <summary>
/// A mock step that records invocations and can be configured to throw.
/// </summary>
public sealed class MockStep : IStep
{
    private readonly Exception? _exceptionToThrow;
    private readonly Func<IWorkflowContext, Task>? _action;

    /// <summary>Creates a mock step.</summary>
    public MockStep(string name, Func<IWorkflowContext, Task>? action = null, Exception? throwException = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _action = action;
        _exceptionToThrow = throwException;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Gets the number of invocations.</summary>
    public int InvocationCount { get; private set; }

    /// <summary>Gets the contexts passed to each invocation.</summary>
    public List<IWorkflowContext> Invocations { get; } = new();

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        InvocationCount++;
        Invocations.Add(context);

        if (_exceptionToThrow != null)
            throw _exceptionToThrow;

        if (_action != null)
            await _action(context).ConfigureAwait(false);
    }
}
