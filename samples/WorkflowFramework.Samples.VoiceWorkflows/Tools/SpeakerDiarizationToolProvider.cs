using System.Text.Json;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Samples.VoiceWorkflows.Tools;

/// <summary>Mock speaker diarization tool provider.</summary>
public sealed class SpeakerDiarizationToolProvider : IToolProvider
{
    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "label_speakers",
                Description = "Label speakers in a transcript. Takes transcript, audio_path, and speaker_count.",
                ParametersSchema = """{"type":"object","properties":{"transcript":{"type":"string"},"audio_path":{"type":"string"},"speaker_count":{"type":"integer"}},"required":["transcript","audio_path","speaker_count"]}"""
            },
            new()
            {
                Name = "count_speakers",
                Description = "Estimate the number of distinct speakers in an audio file.",
                ParametersSchema = """{"type":"object","properties":{"audio_path":{"type":"string"}},"required":["audio_path"]}"""
            }
        };
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(tools);
    }

    public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        return toolName switch
        {
            "label_speakers" =>
                Task.FromResult(new ToolResult
                {
                    Content = """
                    [Speaker 1 — Host]: So I think the most important thing when we talk about this project is really understanding the core architecture. We spent about three weeks iterating on the design before we wrote a single line of production code.

                    [Speaker 2 — Guest]: And that was crucial. The second thing I'd highlight is the testing strategy. We went with a combination of integration tests and property-based testing, which caught several edge cases that traditional unit tests would have missed entirely.

                    [Speaker 1 — Host]: One thing that surprised us was how much the performance characteristics changed once we moved from the prototype to the real implementation.

                    [Speaker 2 — Guest]: The prototype used in-memory dictionaries everywhere, but in production we needed persistent storage, and that changed the whole latency profile.

                    [Speaker 1 — Host]: I'd also mention the team dynamics. Having a dedicated person for code review made a huge difference.

                    [Speaker 2 — Guest]: We caught architectural drift early and kept the codebase consistent. That's something I'd recommend to any team working on a project of this scale.

                    [Speaker 1 — Host]: Finally, the deployment pipeline. We automated everything from day one — CI/CD, infrastructure as code, monitoring dashboards.

                    [Speaker 2 — Guest]: The upfront investment paid for itself within the first month when we needed to do an emergency rollback.
                    """
                }),
            "count_speakers" =>
                Task.FromResult(new ToolResult
                {
                    Content = JsonSerializer.Serialize(new { speaker_count = 2, confidence = 0.92 })
                }),
            _ => Task.FromResult(new ToolResult { Content = $"Unknown tool: {toolName}", IsError = true })
        };
    }
}
