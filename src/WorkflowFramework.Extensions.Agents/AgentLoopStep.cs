using System.Diagnostics;
using System.Text;
using WorkflowFramework.Extensions.Agents.Diagnostics;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>Autonomous agent loop step with compaction and checkpointing.</summary>
public sealed class AgentLoopStep : IStep
{
    private readonly IAgentProvider _provider;
    private readonly ToolRegistry _registry;
    private readonly AgentLoopOptions _options;

    /// <summary>Initializes a new AgentLoopStep.</summary>
    public AgentLoopStep(IAgentProvider provider, ToolRegistry registry, AgentLoopOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "AgentLoop";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        using var loopActivity = AgentActivitySource.Instance.StartActivity(
            AgentActivitySource.AgentLoop,
            ActivityKind.Internal);

        loopActivity?.SetTag(AgentActivitySource.TagStepName, Name);
        loopActivity?.SetTag(AgentActivitySource.TagProviderType, _provider.GetType().Name);
        loopActivity?.SetTag(AgentActivitySource.TagProviderName, _provider.Name);

        var hookPipeline = _options.Hooks ?? new HookPipeline();
        var contextManager = _options.ContextManager ?? new DefaultContextManager();
        var toolResults = new List<ToolResult>();
        var iteration = 0;

        // Build context from sources
        var contextAggregator = new ContextAggregator(_options.ContextSources);
        var contextPrompt = await contextAggregator.BuildContextPromptAsync(context.CancellationToken).ConfigureAwait(false);

        // Get tool definitions
        var tools = await _registry.ListAllToolsAsync(context.CancellationToken).ConfigureAwait(false);
        var agentTools = new List<AgentTool>();
        foreach (var tool in tools)
        {
            agentTools.Add(new AgentTool
            {
                Name = tool.Name,
                Description = tool.Description,
                ParametersSchema = tool.ParametersSchema
            });
        }

        if (!string.IsNullOrEmpty(_options.SystemPrompt))
        {
            contextManager.AddMessage(new ConversationMessage
            {
                Role = ConversationRole.System,
                Content = _options.SystemPrompt!
            });
        }
        if (!string.IsNullOrEmpty(contextPrompt))
        {
            contextManager.AddMessage(new ConversationMessage
            {
                Role = ConversationRole.System,
                Content = contextPrompt
            });
        }

        string lastResponse = string.Empty;

        while (iteration < _options.MaxIterations)
        {
            iteration++;

            using var iterationActivity = AgentActivitySource.Instance.StartActivity(
                AgentActivitySource.AgentIteration,
                ActivityKind.Internal);

            iterationActivity?.SetTag(AgentActivitySource.TagStepName, Name);
            iterationActivity?.SetTag(AgentActivitySource.TagIteration, iteration);

            // Auto-compaction
            if (_options.AutoCompact && contextManager.EstimateTokenCount() > _options.MaxContextTokens)
            {
                using var compactActivity = AgentActivitySource.Instance.StartActivity(
                    AgentActivitySource.ContextCompaction,
                    ActivityKind.Internal);

                compactActivity?.SetTag(AgentActivitySource.TagStepName, Name);
                compactActivity?.SetTag(AgentActivitySource.TagCompactionOriginalTokens, contextManager.EstimateTokenCount());

                var preCtx = new HookContext { Event = AgentHookEvent.PreCompact, StepName = Name, WorkflowContext = context };
                await hookPipeline.FireAsync(AgentHookEvent.PreCompact, preCtx, context.CancellationToken).ConfigureAwait(false);

                var compactionOpts = new CompactionOptions
                {
                    MaxTokens = _options.MaxContextTokens,
                    Strategy = _options.CompactionStrategy,
                    FocusInstructions = _options.CompactionFocusInstructions
                };
                await contextManager.CompactAsync(compactionOpts, context.CancellationToken).ConfigureAwait(false);

                compactActivity?.SetTag(AgentActivitySource.TagCompactionCompactedTokens, contextManager.EstimateTokenCount());

                var postCtx = new HookContext { Event = AgentHookEvent.PostCompact, StepName = Name, WorkflowContext = context };
                await hookPipeline.FireAsync(AgentHookEvent.PostCompact, postCtx, context.CancellationToken).ConfigureAwait(false);
            }

            var sb = new StringBuilder();
            foreach (var msg in contextManager.GetMessages())
            {
                sb.AppendLine(msg.Content);
            }

            var request = new LlmRequest
            {
                Prompt = sb.ToString(),
                Variables = new Dictionary<string, object?>(context.Properties),
                Tools = agentTools
            };

            var response = await _provider.CompleteAsync(request, context.CancellationToken).ConfigureAwait(false);
            lastResponse = response.Content;

            // Record token usage if available
            if (response.Usage != null)
            {
                iterationActivity?.SetTag(AgentActivitySource.TagPromptTokens, response.Usage.PromptTokens);
                iterationActivity?.SetTag(AgentActivitySource.TagCompletionTokens, response.Usage.CompletionTokens);
                iterationActivity?.SetTag(AgentActivitySource.TagTotalTokens, response.Usage.TotalTokens);
            }

            contextManager.AddMessage(new ConversationMessage
            {
                Role = ConversationRole.Assistant,
                Content = response.Content
            });

            if (response.ToolCalls.Count == 0) break;

            iterationActivity?.SetTag(AgentActivitySource.TagToolCallCount, response.ToolCalls.Count);

            foreach (var toolCall in response.ToolCalls)
            {
                using var toolActivity = AgentActivitySource.Instance.StartActivity(
                    AgentActivitySource.ToolCall,
                    ActivityKind.Internal);

                toolActivity?.SetTag(AgentActivitySource.TagStepName, Name);
                toolActivity?.SetTag(AgentActivitySource.TagToolName, toolCall.ToolName);

                var preHookCtx = new HookContext
                {
                    Event = AgentHookEvent.PreToolCall,
                    StepName = Name,
                    ToolName = toolCall.ToolName,
                    ToolArgs = toolCall.Arguments,
                    WorkflowContext = context
                };
                var hookResult = await hookPipeline.FireAsync(AgentHookEvent.PreToolCall, preHookCtx, context.CancellationToken).ConfigureAwait(false);

                if (hookResult.Decision == HookDecision.Deny)
                {
                    toolActivity?.SetTag(AgentActivitySource.TagToolIsError, true);
                    toolActivity?.SetStatus(ActivityStatusCode.Error, "Denied by hook");
                    contextManager.AddToolCall(toolCall.ToolName, toolCall.Arguments,
                        "Tool call denied: " + (hookResult.Reason ?? "denied by hook"));
                    continue;
                }

                var args = hookResult.Decision == HookDecision.Modify && hookResult.ModifiedArgs != null
                    ? hookResult.ModifiedArgs : toolCall.Arguments;

                ToolResult result;
                try
                {
                    result = await _registry.InvokeAsync(toolCall.ToolName, args, context.CancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    result = new ToolResult { Content = ex.Message, IsError = true };
                    toolActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    var failCtx = new HookContext
                    {
                        Event = AgentHookEvent.PostToolCallFailure, StepName = Name,
                        ToolName = toolCall.ToolName, ToolArgs = args, ToolResult = result, WorkflowContext = context
                    };
                    await hookPipeline.FireAsync(AgentHookEvent.PostToolCallFailure, failCtx, context.CancellationToken).ConfigureAwait(false);
                }

                toolActivity?.SetTag(AgentActivitySource.TagToolIsError, result.IsError);
                toolResults.Add(result);
                contextManager.AddToolCall(toolCall.ToolName, args, result.Content);

                var postHookCtx = new HookContext
                {
                    Event = AgentHookEvent.PostToolCall, StepName = Name,
                    ToolName = toolCall.ToolName, ToolArgs = args, ToolResult = result, WorkflowContext = context
                };
                await hookPipeline.FireAsync(AgentHookEvent.PostToolCall, postHookCtx, context.CancellationToken).ConfigureAwait(false);
            }

            if (_options.CheckpointStore != null && iteration % _options.CheckpointInterval == 0)
            {
                var snapshot = contextManager.CreateSnapshot();
                snapshot.StepName = Name;
                await _options.CheckpointStore.SaveAsync(
                    context.WorkflowId, Name + "-iteration-" + iteration, snapshot,
                    context.CancellationToken).ConfigureAwait(false);
            }
        }

        loopActivity?.SetTag(AgentActivitySource.TagIterationTotal, iteration);

        context.Properties[Name + ".Response"] = lastResponse;
        context.Properties[Name + ".Iterations"] = iteration;
        context.Properties[Name + ".ToolResults"] = toolResults;
    }
}
