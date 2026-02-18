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
    /// Gets or sets the step type name (resolved via IStepRegistry).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional step name override.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the condition expression (for If steps).
    /// </summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets the then step type (for If steps).
    /// </summary>
    [JsonPropertyName("then")]
    public string? Then { get; set; }

    /// <summary>
    /// Gets or sets the else step type (for If steps).
    /// </summary>
    [JsonPropertyName("else")]
    public string? Else { get; set; }

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
    /// Gets or sets parallel step types.
    /// </summary>
    [JsonPropertyName("parallel")]
    public List<string>? Parallel { get; set; }
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
