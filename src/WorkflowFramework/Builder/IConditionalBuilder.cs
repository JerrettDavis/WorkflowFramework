namespace WorkflowFramework.Builder;

/// <summary>
/// Builder for conditional (If/Then/Else) workflow branches.
/// </summary>
public interface IConditionalBuilder
{
    /// <summary>
    /// Specifies the step to execute when the condition is true.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>An else builder for optional else branch.</returns>
    IElseBuilder Then<TStep>() where TStep : IStep, new();

    /// <summary>
    /// Specifies the step to execute when the condition is true.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>An else builder for optional else branch.</returns>
    IElseBuilder Then(IStep step);
}

/// <summary>
/// Builder for the else branch of a conditional.
/// </summary>
public interface IElseBuilder
{
    /// <summary>
    /// Specifies the step to execute when the condition is false.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder Else<TStep>() where TStep : IStep, new();

    /// <summary>
    /// Specifies the step to execute when the condition is false.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder Else(IStep step);

    /// <summary>
    /// Ends the conditional without an else branch.
    /// </summary>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder EndIf();
}

/// <summary>
/// Builder for conditional (If/Then/Else) workflow branches (typed).
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IConditionalBuilder<TData> where TData : class
{
    /// <summary>
    /// Specifies the step to execute when the condition is true.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>An else builder for optional else branch.</returns>
    IElseBuilder<TData> Then<TStep>() where TStep : IStep<TData>, new();

    /// <summary>
    /// Specifies the step to execute when the condition is true.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>An else builder for optional else branch.</returns>
    IElseBuilder<TData> Then(IStep<TData> step);
}

/// <summary>
/// Builder for the else branch of a typed conditional.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IElseBuilder<TData> where TData : class
{
    /// <summary>
    /// Specifies the step to execute when the condition is false.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder<TData> Else<TStep>() where TStep : IStep<TData>, new();

    /// <summary>
    /// Specifies the step to execute when the condition is false.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder<TData> Else(IStep<TData> step);

    /// <summary>
    /// Ends the conditional without an else branch.
    /// </summary>
    /// <returns>The parent workflow builder for continued chaining.</returns>
    IWorkflowBuilder<TData> EndIf();
}
