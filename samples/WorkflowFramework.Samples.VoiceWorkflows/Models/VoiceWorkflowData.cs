namespace WorkflowFramework.Samples.VoiceWorkflows.Models;

/// <summary>
/// Typed workflow data carrying state through voice processing pipelines.
/// </summary>
public class VoiceWorkflowData
{
    public string? AudioPath { get; set; }
    public string? RawTranscript { get; set; }
    public string? LabeledTranscript { get; set; }
    public string? ProcessedText { get; set; }
    public string? Summary { get; set; }
    public string? FinalOutput { get; set; }
    public int SpeakerCount { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<string> Questions { get; set; } = new();
    public List<QAPair> QAPairs { get; set; } = new();
}

/// <summary>A question-answer pair from interview workflows.</summary>
public record QAPair(string Question, string Answer);
