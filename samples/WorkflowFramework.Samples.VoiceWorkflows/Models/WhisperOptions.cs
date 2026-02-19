namespace WorkflowFramework.Samples.VoiceWorkflows.Models;

/// <summary>Options for the Whisper transcription tool provider.</summary>
public sealed class WhisperOptions
{
    public string ModelPath { get; set; } = "models/ggml-base.en.bin";
    public string Language { get; set; } = "en";
    public int MaxSegmentLength { get; set; } = 500;
}
