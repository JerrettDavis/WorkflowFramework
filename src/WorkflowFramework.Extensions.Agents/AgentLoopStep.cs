using System.Text;
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

            // Auto-compaction
            if (_options.AutoCompact && contextManager.EstimateTokenCount() > _options.MaxContextTokens)
            {
                var preCtx = new HookContext { Event = AgentHookEvent.PreCompact, StepName = Name, WorkflowContext = context };
                await hookPipeline.FireAsync(AgentHookEvent.PreCompact, preCtx, context.CancellationToken).ConfigureAwait(false);

                var compactionOpts = new CompactionOptions
                {
                    MaxTokens = _options.MaxContextTokens,
                    Strategy = _options.CompactionStrategy,
                    FocusInstructions = _options.CompactionFocusInstructions
                };
                await contextManager.CompactAsync(compactionOpts, context.CancellationToken).ConfigureAwait(false);

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

            contextManager.AddMessage(new ConversationMessage
            {
                Role = ConversationRole.Assistant,
                Content = response.Content
            });

            if (response.ToolCalls.Count == 0) break;

            foreach (var toolCall in response.ToolCalls)
            {
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
                    var failCtx = new HookContext
                    {
                        Event = AgentHookEvent.PostToolCallFailure, StepName = Name,
                        ToolName = toolCall.ToolName, ToolArgs = args, ToolResult = result, WorkflowContext = context
                    };
                    await hookPipeline.FireAsync(AgentHookEvent.PostToolCallFailure, failCtx, context.CancellationToken).ConfigureAwait(false);
                }

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

        context.Properties[Name + ".Response"] = lastResponse;
        context.Properties[Name + ".Iterations"] = iteration;
        context.Properties[Name + ".ToolResults"] = toolResults;
    }
}


