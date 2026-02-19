using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Dashboard.Web.Models;

// Mirror of API DTOs for the Web client

public sealed class WorkflowListItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public int StepCount { get; set; }
}

public sealed class SavedWorkflowDefinition
{
    public string Id { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}

public sealed class WorkflowDefinitionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Workflow";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("steps")]
    public List<StepDefinitionApiDto> Steps { get; set; } = new();
}

public sealed class StepDefinitionApiDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionApiDto>? Steps { get; set; }
}

public sealed class CreateWorkflowRequest
{
    public string? Description { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}

public sealed class StepTypeInfo
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Description { get; set; }
    public JsonElement? ConfigSchema { get; set; }
}

public sealed class RunSummary
{
    public string RunId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum TemplateDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

public sealed class WorkflowTemplateSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public TemplateDifficulty Difficulty { get; set; }
    public int StepCount { get; set; }
    public string? PreviewImageUrl { get; set; }
}

public sealed class ValidationErrorDto
{
    public string Severity { get; set; } = "Error";
    public string? StepName { get; set; }
    public string Message { get; set; } = "";
}

public sealed class ValidationResultDto
{
    public List<ValidationErrorDto> Errors { get; set; } = new();
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public sealed class WorkflowTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public TemplateDifficulty Difficulty { get; set; }
    public int StepCount { get; set; }
    public string? PreviewImageUrl { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}
