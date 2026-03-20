using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.Configuration;

/// <summary>
/// Represents a workflow definition that can be deserialized from YAML or JSON.
/// </summary>
public sealed class WorkflowDefinition
{
    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Workflow";

    /// <summary>
    /// Gets or sets the workflow version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets an optional description of the workflow.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the workflow steps.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<StepDefinition> Steps { get; set; } = new();

    /// <summary>
    /// Gets or sets whether compensation is enabled.
    /// </summary>
    [JsonPropertyName("compensation")]
    public bool Compensation { get; set; }
}

/// <summary>
/// Represents a step definition within a workflow configuration.
/// </summary>
public sealed class StepDefinition
{
    /// <summary>
    /// Gets or sets the step type name (resolved via IStepRegistry) or a composite step category
    /// (step, conditional, parallel, foreach, while, dowhile, retry, try, subworkflow, approval, saga).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step class name used when <see cref="Type"/> is a composite step category (e.g. "step").
    /// When present, this value is resolved via the step registry instead of <see cref="Type"/>.
    /// </summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets an optional step name override.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the condition expression key.
    /// <list type="bullet">
    ///   <item>For <c>conditional</c>, <c>while</c>, and <c>dowhile</c> steps: the property key whose value is
    ///   checked to be <c>true</c> or <c>"true"</c> (via <c>ctx.Properties[Condition]</c>).</item>
    ///   <item>For <c>foreach</c> steps: the property key whose value is the items collection to iterate
    ///   (must implement <see cref="System.Collections.IEnumerable"/>). Defaults to <c>"items"</c> when not set.</item>
    /// </list>
    /// </summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets the then step class name for a conditional (legacy single-class format).
    /// Use <see cref="ThenSteps"/> for rich nested step definitions.
    /// </summary>
    [JsonPropertyName("then")]
    public string? Then { get; set; }

    /// <summary>
    /// Gets or sets the else step class name for a conditional (legacy single-class format).
    /// Use <see cref="ElseSteps"/> for rich nested step definitions.
    /// </summary>
    [JsonPropertyName("else")]
    public string? Else { get; set; }

    /// <summary>
    /// Gets or sets the then branch step definitions for a conditional step (new nested format).
    /// </summary>
    [JsonPropertyName("thenSteps")]
    public List<StepDefinition>? ThenSteps { get; set; }

    /// <summary>
    /// Gets or sets the else branch step definitions for a conditional step (new nested format).
    /// </summary>
    [JsonPropertyName("elseSteps")]
    public List<StepDefinition>? ElseSteps { get; set; }

    /// <summary>
    /// Gets or sets retry configuration.
    /// </summary>
    [JsonPropertyName("retry")]
    public RetryDefinition? Retry { get; set; }

    /// <summary>
    /// Gets or sets timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public double? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets parallel step types (legacy format — list of class names).
    /// Use <see cref="Type"/> = "parallel" with <see cref="Steps"/> for the rich format.
    /// </summary>
    [JsonPropertyName("parallel")]
    public List<string>? Parallel { get; set; }

    /// <summary>
    /// Gets or sets loop configuration (for forEach/while/doWhile steps).
    /// </summary>
    [JsonPropertyName("loop")]
    public LoopDefinition? Loop { get; set; }

    /// <summary>
    /// Gets or sets sub-workflow name (for subworkflow type).
    /// </summary>
    [JsonPropertyName("subWorkflow")]
    public string? SubWorkflow { get; set; }

    /// <summary>
    /// Gets or sets the child steps (for composite step types such as parallel, retry, loop, try, saga).
    /// </summary>
    [JsonPropertyName("steps")]
    public List<StepDefinition>? Steps { get; set; }

    /// <summary>
    /// Gets or sets the human-readable message shown during an approval step.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of approvers required (for approval steps).
    /// </summary>
    [JsonPropertyName("requiredApprovers")]
    public int? RequiredApprovers { get; set; }

    /// <summary>
    /// Gets or sets the approval timeout in minutes (for approval steps).
    /// </summary>
    [JsonPropertyName("timeoutMinutes")]
    public int? TimeoutMinutes { get; set; }

    /// <summary>
    /// Gets or sets the catch handler definitions (for <c>type: try</c> steps).
    /// Each handler specifies the exception type and the steps to execute when that exception is thrown.
    /// </summary>
    [JsonPropertyName("catch")]
    public List<CatchDefinition>? Catch { get; set; }
}

/// <summary>
/// Loop configuration in a workflow definition.
/// </summary>
public sealed class LoopDefinition
{
    /// <summary>
    /// Gets or sets the loop type (forEach, while, doWhile).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "while";

    /// <summary>
    /// Gets or sets the maximum iterations (safety limit).
    /// </summary>
    [JsonPropertyName("maxIterations")]
    public int MaxIterations { get; set; } = 1000;
}

/// <summary>
/// Retry configuration in a workflow definition.
/// </summary>
public sealed class RetryDefinition
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the backoff strategy (none, linear, exponential).
    /// </summary>
    [JsonPropertyName("backoff")]
    public string Backoff { get; set; } = "none";

    /// <summary>
    /// Gets or sets the base delay in milliseconds.
    /// </summary>
    [JsonPropertyName("baseDelayMs")]
    public int BaseDelayMs { get; set; } = 100;
}

/// <summary>
/// Defines a catch block for a <c>type: try</c> step.
/// </summary>
public sealed class CatchDefinition
{
    /// <summary>
    /// Gets or sets the fully-qualified or short exception type name to catch
    /// (e.g. <c>"Exception"</c>, <c>"InvalidOperationException"</c>).
    /// If the type cannot be resolved at runtime, <see cref="System.Exception"/> is used as the fallback.
    /// </summary>
    [JsonPropertyName("exception")]
    public string Exception { get; set; } = "Exception";

    /// <summary>
    /// Gets or sets the steps to execute when the exception is caught.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<StepDefinition> Steps { get; set; } = new();
}
