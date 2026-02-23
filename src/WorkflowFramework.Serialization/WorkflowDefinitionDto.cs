using System.Text.Json.Serialization;

namespace WorkflowFramework.Serialization;

/// <summary>
/// Serializable workflow definition DTO.
/// </summary>
public sealed class WorkflowDefinitionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Workflow";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("steps")]
    public List<StepDefinitionDto> Steps { get; set; } = new();
}

/// <summary>
/// Serializable step definition DTO supporting all core step types.
/// </summary>
public sealed class StepDefinitionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Child steps (for parallel, forEach, while, doWhile, retry, tryCatch, saga, subWorkflow).</summary>
    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionDto>? Steps { get; set; }

    /// <summary>Then branch (for conditional).</summary>
    [JsonPropertyName("then")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionDto? Then { get; set; }

    /// <summary>Else branch (for conditional).</summary>
    [JsonPropertyName("else")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionDto? Else { get; set; }

    /// <summary>Inner step (for timeout).</summary>
    [JsonPropertyName("inner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionDto? Inner { get; set; }

    /// <summary>Max attempts (for retry).</summary>
    [JsonPropertyName("maxAttempts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MaxAttempts { get; set; }

    /// <summary>Timeout in seconds (for timeout step).</summary>
    [JsonPropertyName("timeoutSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double TimeoutSeconds { get; set; }

    /// <summary>Delay in seconds (for delay step).</summary>
    [JsonPropertyName("delaySeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DelaySeconds { get; set; }

    /// <summary>Try body (for tryCatch).</summary>
    [JsonPropertyName("tryBody")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionDto>? TryBody { get; set; }

    /// <summary>Catch handler type names (for tryCatch).</summary>
    [JsonPropertyName("catchTypes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CatchTypes { get; set; }

    /// <summary>Finally body (for tryCatch).</summary>
    [JsonPropertyName("finallyBody")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionDto>? FinallyBody { get; set; }

    /// <summary>Sub-workflow name.</summary>
    [JsonPropertyName("subWorkflowName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubWorkflowName { get; set; }

    /// <summary>Step-specific configuration (e.g., url, model, prompt, expression).</summary>
    [JsonPropertyName("config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Config { get; set; }
}
