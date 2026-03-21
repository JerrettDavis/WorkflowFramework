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

    [JsonPropertyName("canvas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowCanvasDto? Canvas { get; set; }
}

public sealed class WorkflowCanvasDto
{
    [JsonPropertyName("nodes")]
    public List<WorkflowCanvasNodeDto> Nodes { get; set; } = new();

    [JsonPropertyName("edges")]
    public List<WorkflowCanvasEdgeDto> Edges { get; set; } = new();
}

public sealed class WorkflowCanvasNodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; set; }

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Config { get; set; }
}

public sealed class WorkflowCanvasEdgeDto
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }
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

    [JsonPropertyName("then")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionApiDto? Then { get; set; }

    [JsonPropertyName("else")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionApiDto? Else { get; set; }

    [JsonPropertyName("inner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepDefinitionApiDto? Inner { get; set; }

    [JsonPropertyName("maxAttempts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MaxAttempts { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double TimeoutSeconds { get; set; }

    [JsonPropertyName("delaySeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double DelaySeconds { get; set; }

    [JsonPropertyName("tryBody")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionApiDto>? TryBody { get; set; }

    [JsonPropertyName("catchTypes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CatchTypes { get; set; }

    [JsonPropertyName("finallyBody")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StepDefinitionApiDto>? FinallyBody { get; set; }

    [JsonPropertyName("subWorkflowName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubWorkflowName { get; set; }

    [JsonPropertyName("config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Config { get; set; }
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
    public string? Source { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
    public Dictionary<string, string>? StepResults { get; set; }
    public string? Error { get; set; }
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
    [JsonConverter(typeof(ValidationSeverityStringConverter))]
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

public sealed class ValidationSeverityStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? "Error" : value;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number))
        {
            return number switch
            {
                0 => "Info",
                1 => "Warning",
                2 => "Error",
                _ => "Error"
            };
        }

        return "Error";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(string.IsNullOrWhiteSpace(value) ? "Error" : value);
    }
}

public sealed class DashboardSettingsDto
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string? OpenAiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? HuggingFaceApiKey { get; set; }
    public string? OpenAiBaseUrl { get; set; }
    public string? DefaultProvider { get; set; }
    public string? DefaultModel { get; set; }
    public int DefaultTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentRuns { get; set; } = 5;
}

public sealed class OllamaTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public sealed class WorkflowExportDto
{
    public string FormatVersion { get; set; } = "1.0";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public WorkflowDefinitionDto Definition { get; set; } = new();
    public DateTimeOffset ExportedAt { get; set; }
}

public sealed class WebhookTriggerResponseDto
{
    public string RunId { get; set; } = "";
    public string Status { get; set; } = "";
    public string WebhookId { get; set; } = "";
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

// Auth models

public sealed class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class UserProfile
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ApiKeyInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public sealed class CreateApiKeyResult
{
    public string Id { get; set; } = "";
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CreateApiKeyRequest
{
    public string Name { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
}

// Trigger models

public sealed class TriggerDefinitionDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "";
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> Configuration { get; set; } = [];
}

public sealed class TriggerTypeInfoDto
{
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string? ConfigSchema { get; set; }
    public string? Icon { get; set; }
}

public sealed class StartRunRequestDto
{
    public Dictionary<string, object?>? Inputs { get; set; }
    public string? Source { get; set; }
}

public sealed class VoiceSubmissionRequestDto
{
    public string? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string Transcript { get; set; } = "";
    public string? Language { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioMimeType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<VoiceQaPairDto> QaPairs { get; set; } = [];
}

public sealed class VoiceSubmissionDto
{
    public string Id { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string Transcript { get; set; } = "";
    public string? Language { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioMimeType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<VoiceQaPairDto> QaPairs { get; set; } = [];
}

public sealed class VoiceQaPairDto
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}
