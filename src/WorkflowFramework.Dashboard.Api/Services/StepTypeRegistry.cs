using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Registry of all available step types with metadata for the designer UI.
/// </summary>
public sealed class StepTypeRegistry
{
    private readonly Dictionary<string, StepTypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets all registered step types.</summary>
    public IReadOnlyList<StepTypeInfo> All => _types.Values.ToList();

    /// <summary>Gets a step type by its type key.</summary>
    public StepTypeInfo? Get(string type) => _types.GetValueOrDefault(type);

    /// <summary>Registers a step type.</summary>
    public StepTypeRegistry Register(StepTypeInfo info)
    {
        _types[info.Type] = info;
        return this;
    }

    /// <summary>
    /// Creates a registry pre-populated with all known step types.
    /// </summary>
    public static StepTypeRegistry CreateDefault()
    {
        var registry = new StepTypeRegistry();

        // Core
        Register(registry, "Action", "Core", "Executes a custom action delegate.");
        Register(registry, "Conditional", "Core", "Branches execution based on a condition.");
        Register(registry, "Parallel", "Core", "Executes child steps in parallel.");
        Register(registry, "ForEach", "Core", "Iterates over a collection executing child steps for each item.");
        Register(registry, "While", "Core", "Loops while a condition is true (checked before each iteration).");
        Register(registry, "DoWhile", "Core", "Loops while a condition is true (checked after each iteration).");
        Register(registry, "Retry", "Core", "Retries child steps on failure with configurable attempts.");
        Register(registry, "Timeout", "Core", "Wraps an inner step with a timeout duration.");
        Register(registry, "Delay", "Core", "Pauses execution for a specified duration.");
        Register(registry, "TryCatch", "Core", "Provides try/catch/finally error handling around steps.");
        Register(registry, "SubWorkflow", "Core", "Invokes another workflow by name.");
        Register(registry, "Saga", "Core", "Executes steps with compensation/rollback on failure.");

        // Integration
        Register(registry, "ContentBasedRouter", "Integration", "Routes messages based on content evaluation.");
        Register(registry, "MessageFilter", "Integration", "Filters messages based on criteria.");
        Register(registry, "RecipientList", "Integration", "Sends messages to a dynamic list of recipients.");
        Register(registry, "Splitter", "Integration", "Splits a message into multiple parts for individual processing.");
        Register(registry, "Aggregator", "Integration", "Aggregates multiple messages into a single message.");
        Register(registry, "ScatterGather", "Integration", "Broadcasts to multiple recipients and aggregates responses.");
        Register(registry, "WireTap", "Integration", "Sends a copy of the message to a secondary channel.");
        Register(registry, "DeadLetter", "Integration", "Routes failed messages to a dead letter channel.");

        // AI/Agents
        Register(registry, "AgentLoopStep", "AI/Agents", "Runs an autonomous agent loop with tool access.");
        Register(registry, "AgentDecisionStep", "AI/Agents", "Uses an AI agent to make a routing decision.");
        Register(registry, "AgentPlanStep", "AI/Agents", "Generates an execution plan using an AI agent.");
        Register(registry, "LlmCallStep", "AI/Agents", "Makes a direct call to a language model.");
        Register(registry, "ToolCallStep", "AI/Agents", "Invokes a registered tool/function.");

        // Data
        Register(registry, "DataMapStep", "Data", "Transforms data using field mapping rules.");
        Register(registry, "FormatConvertStep", "Data", "Converts data between formats (JSON, XML, CSV, etc.).");
        Register(registry, "SchemaValidateStep", "Data", "Validates data against a JSON schema.");
        Register(registry, "BatchProcessStep", "Data", "Processes data in configurable batch sizes.");

        // HTTP
        Register(registry, "HttpStep", "HTTP", "Makes an HTTP request to an external service.");
        Register(registry, "WebhookTriggerStep", "HTTP", "Waits for an incoming webhook request.");

        // Events
        Register(registry, "PublishEventStep", "Events", "Publishes an event to the event bus.");
        Register(registry, "WaitForEventStep", "Events", "Pauses execution until a matching event is received.");

        // Human
        Register(registry, "HumanTaskStep", "Human", "Creates a task for human completion.");
        Register(registry, "ApprovalStep", "Human", "Pauses workflow pending human approval.");

        return registry;
    }

    private static void Register(StepTypeRegistry registry, string type, string category, string description)
    {
        registry.Register(new StepTypeInfo
        {
            Type = type,
            Name = type,
            Category = category,
            Description = description
        });
    }
}
