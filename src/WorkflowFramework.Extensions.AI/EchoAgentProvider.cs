namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// A simple echo agent provider for testing that returns the prompt as the response.
/// </summary>
public sealed class EchoAgentProvider : IAgentProvider
{
    /// <inheritdoc />
    public string Name => "echo";

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmResponse
        {
            Content = $"Echo: {request.Prompt}",
            FinishReason = "stop",
            Usage = new TokenUsage
            {
                PromptTokens = request.Prompt.Length,
                CompletionTokens = request.Prompt.Length + 6,
                TotalTokens = request.Prompt.Length * 2 + 6
            }
        });
    }

    /// <inheritdoc />
    public Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default)
    {
        // Return the first option by default
        return Task.FromResult(request.Options.Count > 0 ? request.Options[0] : "default");
    }
}
