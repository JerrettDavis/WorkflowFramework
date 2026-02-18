namespace WorkflowFramework.Builder;

/// <summary>
/// Fluent builder for constructing workflows.
/// </summary>
public interface IWorkflowBuilder
{
    /// <summary>
    /// Adds a step to the workflow.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Step<TStep>() where TStep : IStep, new();

    /// <summary>
    /// Adds a step instance to the workflow.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Step(IStep step);

    /// <summary>
    /// Adds an inline step using a delegate.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Step(string name, Func<IWorkflowContext, Task> action);

    /// <summary>
    /// Adds a conditional branch to the workflow.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <returns>A conditional builder for specifying Then/Else branches.</returns>
    IConditionalBuilder If(Func<IWorkflowContext, bool> condition);

    /// <summary>
    /// Adds parallel step execution.
    /// </summary>
    /// <param name="configure">A delegate to configure the parallel steps.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Parallel(Action<IParallelBuilder> configure);

    /// <summary>
    /// Adds middleware to the workflow pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Use<TMiddleware>() where TMiddleware : IWorkflowMiddleware, new();

    /// <summary>
    /// Adds a middleware instance to the workflow pipeline.
    /// </summary>
    /// <param name="middleware">The middleware instance.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder Use(IWorkflowMiddleware middleware);

    /// <summary>
    /// Registers an event handler for this workflow.
    /// </summary>
    /// <param name="events">The event handler.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder WithEvents(IWorkflowEvents events);

    /// <summary>
    /// Sets the service provider for resolving steps and middleware from DI.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder WithServiceProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Sets the name of the workflow.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder WithName(string name);

    /// <summary>
    /// Enables saga/compensation support for this workflow.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder WithCompensation();

    /// <summary>
    /// Builds the workflow.
    /// </summary>
    /// <returns>The built workflow.</returns>
    IWorkflow Build();
}

/// <summary>
/// Fluent builder for constructing strongly-typed workflows.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IWorkflowBuilder<TData> where TData : class
{
    /// <summary>
    /// Adds a step to the workflow.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Step<TStep>() where TStep : IStep<TData>, new();

    /// <summary>
    /// Adds a step instance to the workflow.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Step(IStep<TData> step);

    /// <summary>
    /// Adds an inline step using a delegate.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Step(string name, Func<IWorkflowContext<TData>, Task> action);

    /// <summary>
    /// Adds a conditional branch to the workflow.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <returns>A conditional builder for specifying Then/Else branches.</returns>
    IConditionalBuilder<TData> If(Func<IWorkflowContext<TData>, bool> condition);

    /// <summary>
    /// Adds parallel step execution.
    /// </summary>
    /// <param name="configure">A delegate to configure the parallel steps.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Parallel(Action<IParallelBuilder<TData>> configure);

    /// <summary>
    /// Adds middleware to the workflow pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Use<TMiddleware>() where TMiddleware : IWorkflowMiddleware, new();

    /// <summary>
    /// Adds a middleware instance to the workflow pipeline.
    /// </summary>
    /// <param name="middleware">The middleware instance.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> Use(IWorkflowMiddleware middleware);

    /// <summary>
    /// Registers an event handler for this workflow.
    /// </summary>
    /// <param name="events">The event handler.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> WithEvents(IWorkflowEvents events);

    /// <summary>
    /// Sets the service provider for resolving steps and middleware from DI.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> WithServiceProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Sets the name of the workflow.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> WithName(string name);

    /// <summary>
    /// Enables saga/compensation support for this workflow.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    IWorkflowBuilder<TData> WithCompensation();

    /// <summary>
    /// Builds the workflow.
    /// </summary>
    /// <returns>The built workflow.</returns>
    IWorkflow<TData> Build();
}
