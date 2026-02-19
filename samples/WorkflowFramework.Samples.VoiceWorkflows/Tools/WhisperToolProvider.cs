using System.Text.Json;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Samples.VoiceWorkflows.Models;

namespace WorkflowFramework.Samples.VoiceWorkflows.Tools;

/// <summary>Mock Whisper transcription tool provider.</summary>
public sealed class WhisperToolProvider : IToolProvider
{
    private readonly WhisperOptions _options;

    public WhisperToolProvider(WhisperOptions options) => _options = options;

    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "transcribe",
                Description = "Transcribe an audio file to text using Whisper. Takes audio_path and optional model_name.",
                ParametersSchema = """{"type":"object","properties":{"audio_path":{"type":"string"},"model_name":{"type":"string"}},"required":["audio_path"]}"""
            },
            new()
            {
                Name = "detect_language",
                Description = "Detect the spoken language in an audio file.",
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
            "transcribe" => Task.FromResult(new ToolResult
            {
                Content = $"""
                So I think the most important thing when we talk about this project is really understanding 
                the core architecture. We spent about three weeks iterating on the design before we wrote 
                a single line of production code. And that was crucial.

                The second thing I'd highlight is the testing strategy. We went with a combination of 
                integration tests and property-based testing, which caught several edge cases that 
                traditional unit tests would have missed entirely.

                One thing that surprised us was how much the performance characteristics changed once we 
                moved from the prototype to the real implementation. The prototype used in-memory 
                dictionaries everywhere, but in production we needed persistent storage, and that 
                changed the whole latency profile.

                I'd also mention the team dynamics. Having a dedicated person for code review made a 
                huge difference. We caught architectural drift early and kept the codebase consistent. 
                That's something I'd recommend to any team working on a project of this scale.

                Finally, the deployment pipeline. We automated everything from day one — CI/CD, 
                infrastructure as code, monitoring dashboards. The upfront investment paid for itself 
                within the first month when we needed to do an emergency rollback.
                [Transcribed using model: {_options.ModelPath}, language: {_options.Language}]
                """
            }),
            "detect_language" => Task.FromResult(new ToolResult
            {
                Content = JsonSerializer.Serialize(new { language = _options.Language, confidence = 0.97 })
            }),
            _ => Task.FromResult(new ToolResult { Content = $"Unknown tool: {toolName}", IsError = true })
        };
    }
}
