namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Voice/QA submission payload saved from the dashboard voice studio.
/// </summary>
public sealed class VoiceSubmissionRequest
{
    public string? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioMimeType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<VoiceQaPair> QaPairs { get; set; } = [];
}

public sealed class VoiceSubmission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string Transcript { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioMimeType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<VoiceQaPair> QaPairs { get; set; } = [];
}

public sealed class VoiceQaPair
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}
