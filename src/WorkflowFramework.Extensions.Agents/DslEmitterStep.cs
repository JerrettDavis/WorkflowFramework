using System.Text.Json;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// A workflow step that instructs an AI agent to emit a JSON array of DSL pipeline steps
/// rather than executing tools directly.  Any tool calls the model attempts are absorbed
/// (never forwarded) so the agent's only output channel is the DSL specification.
/// </summary>
public sealed class DslEmitterStep : IStep
{
    private readonly IAgentProvider _provider;
    private readonly DslEmitterOptions _options;

    /// <summary>Initialises a new <see cref="DslEmitterStep"/>.</summary>
    public DslEmitterStep(IAgentProvider provider, DslEmitterOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "DslEmitter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var task = context.Properties.TryGetValue("task", out var t) ? t?.ToString() : null;

        var systemPrompt =
            "You are a workflow DSL emitter. " +
            "Respond ONLY with a valid JSON array of workflow step objects. " +
            "Do NOT invoke any tools. Do NOT include any prose outside the JSON array. " +
            "Example: [{\"step\":\"build\",\"command\":\"dotnet build\"}]";

        var userPrompt = string.IsNullOrWhiteSpace(task)
            ? "Emit a JSON array of workflow DSL steps for the current task."
            : task;

        var emittedSteps = new List<object?>();
        var iteration = 0;
        var maxIterations = _options.MaxIterations > 0 ? _options.MaxIterations : 3;

        while (iteration < maxIterations)
        {
            iteration++;

            var request = new LlmRequest
            {
                Prompt = $"{systemPrompt}\n\nUser: {userPrompt}",
                Variables = new Dictionary<string, object?>(context.Properties),
                // No tools provided — the emitter must not call any tools
                Tools = new List<AgentTool>()
            };

            LlmResponse response;
            try
            {
                response = await _provider.CompleteAsync(request, context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Absorb any tool calls — never expose them (requirement: agent only emits DSL)
            // context.Properties[Name + ".ToolCalls"] is intentionally NOT set.

            var content = response.Content ?? string.Empty;

            // Try to extract a JSON array from the response
            var steps = TryParseStepArray(content);
            if (steps.Count > 0)
            {
                emittedSteps.AddRange(steps);
                break; // We have what we need
            }

            // If the model didn't emit a parseable array yet, loop again with a clarification
            userPrompt = "Respond ONLY with a JSON array of workflow DSL steps. No prose.";
        }

        context.Properties[$"{Name}.EmittedSteps"] = emittedSteps;
        context.Properties[$"{Name}.Iterations"] = iteration;
    }

    private static List<object?> TryParseStepArray(string content)
    {
        var results = new List<object?>();
        if (string.IsNullOrWhiteSpace(content)) return results;

        // Find the first '[' and last ']' to extract the JSON array
        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        if (start < 0 || end <= start) return results;

        var json = content.Substring(start, end - start + 1);
        try
        {
            var array = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (array != null)
            {
                foreach (var element in array)
                    results.Add((object?)element);
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty; the loop will retry
        }

        return results;
    }
}

/// <summary>Options for configuring a <see cref="DslEmitterStep"/>.</summary>
public sealed class DslEmitterOptions
{
    /// <summary>Gets or sets the step name. Defaults to "DslEmitter".</summary>
    public string? StepName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of LLM iterations the emitter will attempt
    /// before giving up. Defaults to 3.
    /// </summary>
    public int MaxIterations { get; set; } = 3;
}
