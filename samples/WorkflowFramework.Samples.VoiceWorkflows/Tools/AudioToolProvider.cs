using System.Text.Json;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Samples.VoiceWorkflows.Tools;

/// <summary>Mock audio capture/processing tool provider.</summary>
public sealed class AudioToolProvider : IToolProvider
{
    private int _recordingCounter;

    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "record_audio",
                Description = "Record audio from the microphone. Returns the file path of the recording.",
                ParametersSchema = """{"type":"object","properties":{"duration_seconds":{"type":"integer"},"output_format":{"type":"string"}},"required":[]}"""
            },
            new()
            {
                Name = "normalize_audio",
                Description = "Normalize audio levels in a file. Returns the path to the normalized file.",
                ParametersSchema = """{"type":"object","properties":{"audio_path":{"type":"string"}},"required":["audio_path"]}"""
            }
        };
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(tools);
    }

    public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        return toolName switch
        {
            "record_audio" =>
                Task.FromResult(new ToolResult
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        file_path = $"recordings/session_{++_recordingCounter:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.wav",
                        duration_seconds = 45,
                        sample_rate = 16000,
                        channels = 1
                    })
                }),
            "normalize_audio" =>
                Task.FromResult(new ToolResult
                {
                    Content = JsonSerializer.Serialize(new
                    {
                        normalized_path = "recordings/normalized_output.wav",
                        peak_db = -1.0,
                        rms_db = -18.5
                    })
                }),
            _ => Task.FromResult(new ToolResult { Content = $"Unknown tool: {toolName}", IsError = true })
        };
    }
}
