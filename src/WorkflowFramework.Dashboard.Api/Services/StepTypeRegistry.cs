using System.Text.Json;
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

    private static JsonElement? Schema(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>
    /// Creates a registry pre-populated with all known step types.
    /// </summary>
    public static StepTypeRegistry CreateDefault()
    {
        var registry = new StepTypeRegistry();

        // ── Core ──────────────────────────────────────────────────────
        Register(registry, "Action", "Core", "Executes a custom action delegate.",
            Schema("""
            {
              "properties": {
                "expression": { "type": "string", "uiType": "textarea", "label": "Expression", "helpText": "C# expression to evaluate", "rows": 3 }
              },
              "required": []
            }
            """));

        Register(registry, "Conditional", "Core", "Branches execution based on a condition.",
            Schema("""
            {
              "properties": {
                "expression": { "type": "string", "uiType": "textarea", "label": "Condition Expression", "helpText": "Boolean expression that determines which branch to take", "required": true, "rows": 2 },
                "thenStep":   { "type": "string", "label": "Then Step", "helpText": "Step to execute when condition is true" },
                "elseStep":   { "type": "string", "label": "Else Step", "helpText": "Step to execute when condition is false" }
              },
              "required": ["expression"]
            }
            """));

        Register(registry, "Parallel", "Core", "Executes child steps in parallel.",
            Schema("""
            {
              "properties": {
                "maxConcurrency": { "type": "number", "uiType": "number", "label": "Max Concurrency", "helpText": "Maximum number of steps to run simultaneously (0 = unlimited)", "min": 0, "max": 100, "default": "0" }
              },
              "required": []
            }
            """));

        Register(registry, "ForEach", "Core", "Iterates over a collection executing child steps for each item.",
            Schema("""
            {
              "properties": {
                "collectionExpression": { "type": "string", "uiType": "textarea", "label": "Collection Expression", "helpText": "Expression that resolves to an enumerable collection", "required": true, "rows": 2 },
                "itemVariable":        { "type": "string", "label": "Item Variable", "helpText": "Variable name for the current item in each iteration", "default": "item" }
              },
              "required": ["collectionExpression"]
            }
            """));

        Register(registry, "While", "Core", "Loops while a condition is true (checked before each iteration).",
            Schema("""
            {
              "properties": {
                "expression": { "type": "string", "uiType": "textarea", "label": "Loop Condition", "helpText": "Boolean expression checked before each iteration", "required": true, "rows": 2 }
              },
              "required": ["expression"]
            }
            """));

        Register(registry, "DoWhile", "Core", "Loops while a condition is true (checked after each iteration).",
            Schema("""
            {
              "properties": {
                "expression": { "type": "string", "uiType": "textarea", "label": "Loop Condition", "helpText": "Boolean expression checked after each iteration", "required": true, "rows": 2 }
              },
              "required": ["expression"]
            }
            """));

        Register(registry, "Retry", "Core", "Retries child steps on failure with configurable attempts.",
            Schema("""
            {
              "properties": {
                "maxAttempts":       { "type": "number", "uiType": "number", "label": "Max Attempts", "helpText": "Maximum number of retry attempts", "min": 1, "max": 50, "default": "3" },
                "delayMs":           { "type": "number", "uiType": "number", "label": "Delay (ms)", "helpText": "Delay between retries in milliseconds", "min": 0, "max": 300000, "default": "1000" },
                "backoffMultiplier": { "type": "number", "uiType": "number", "label": "Backoff Multiplier", "helpText": "Multiplier applied to delay after each attempt (e.g. 2.0 for exponential backoff)", "min": 1, "max": 10, "step": 0.1 }
              },
              "required": []
            }
            """));

        Register(registry, "Timeout", "Core", "Wraps an inner step with a timeout duration.",
            Schema("""
            {
              "properties": {
                "durationMs": { "type": "number", "uiType": "number", "label": "Timeout (ms)", "helpText": "Maximum time to wait before cancelling the step", "required": true, "min": 100, "max": 3600000 }
              },
              "required": ["durationMs"]
            }
            """));

        Register(registry, "Delay", "Core", "Pauses execution for a specified duration.",
            Schema("""
            {
              "properties": {
                "durationMs": { "type": "number", "uiType": "slider", "label": "Delay (ms)", "helpText": "Duration to pause in milliseconds", "required": true, "min": 0, "max": 60000, "step": 100 }
              },
              "required": ["durationMs"]
            }
            """));

        Register(registry, "TryCatch", "Core", "Provides try/catch/finally error handling around steps.",
            Schema("""
            {
              "properties": {
                "catchTypes": { "type": "string", "label": "Catch Exception Types", "helpText": "Comma-separated list of exception type names to catch (empty = catch all)" }
              },
              "required": []
            }
            """));

        Register(registry, "SubWorkflow", "Core", "Invokes another workflow by name.",
            Schema("""
            {
              "properties": {
                "workflowName": { "type": "string", "label": "Workflow Name", "helpText": "Name of the workflow to invoke", "required": true }
              },
              "required": ["workflowName"]
            }
            """));

        Register(registry, "Saga", "Core", "Executes steps with compensation/rollback on failure.",
            Schema("""
            {
              "properties": {
                "compensateOnFailure": { "type": "boolean", "uiType": "boolean", "label": "Compensate on Failure", "helpText": "Automatically run compensation steps when a failure occurs", "default": "true" }
              },
              "required": []
            }
            """));

        // ── Integration ───────────────────────────────────────────────
        Register(registry, "ContentBasedRouter", "Integration", "Routes messages based on content evaluation.",
            Schema("""
            {
              "properties": {
                "routingExpression": { "type": "string", "uiType": "textarea", "label": "Routing Expression", "helpText": "Expression evaluated to determine the routing destination", "required": true, "rows": 3 }
              },
              "required": ["routingExpression"]
            }
            """));

        Register(registry, "MessageFilter", "Integration", "Filters messages based on criteria.",
            Schema("""
            {
              "properties": {
                "filterExpression": { "type": "string", "uiType": "textarea", "label": "Filter Expression", "helpText": "Boolean expression — messages that evaluate to true pass through", "required": true, "rows": 3 }
              },
              "required": ["filterExpression"]
            }
            """));

        Register(registry, "RecipientList", "Integration", "Sends messages to a dynamic list of recipients.",
            Schema("""
            {
              "properties": {
                "recipientExpression": { "type": "string", "uiType": "textarea", "label": "Recipient Expression", "helpText": "Expression that resolves to a list of recipient endpoints", "required": true, "rows": 3 }
              },
              "required": ["recipientExpression"]
            }
            """));

        Register(registry, "Splitter", "Integration", "Splits a message into multiple parts for individual processing.",
            Schema("""
            {
              "properties": {
                "splitExpression":   { "type": "string", "uiType": "textarea", "label": "Split Expression", "helpText": "Expression that splits the message into parts", "required": true, "rows": 2 },
                "aggregateResults":  { "type": "boolean", "uiType": "boolean", "label": "Aggregate Results", "helpText": "Whether to aggregate individual results back into a single message" }
              },
              "required": ["splitExpression"]
            }
            """));

        Register(registry, "Aggregator", "Integration", "Aggregates multiple messages into a single message.",
            Schema("""
            {
              "properties": {
                "correlationExpression": { "type": "string", "uiType": "textarea", "label": "Correlation Expression", "helpText": "Expression to correlate related messages", "required": true, "rows": 2 },
                "completionSize":       { "type": "number", "uiType": "number", "label": "Completion Size", "helpText": "Number of messages to collect before aggregating", "min": 1, "max": 10000 },
                "completionTimeout":    { "type": "number", "uiType": "number", "label": "Completion Timeout (ms)", "helpText": "Max time to wait for messages before aggregating", "min": 0, "max": 3600000 }
              },
              "required": ["correlationExpression"]
            }
            """));

        Register(registry, "ScatterGather", "Integration", "Broadcasts to multiple recipients and aggregates responses.",
            Schema("""
            {
              "properties": {
                "timeout":              { "type": "number", "uiType": "number", "label": "Timeout (ms)", "helpText": "Maximum time to wait for all responses", "min": 0, "max": 3600000 },
                "aggregationStrategy":  { "type": "string", "uiType": "select", "label": "Aggregation Strategy", "helpText": "How to combine responses", "options": ["first", "all", "best"] }
              },
              "required": []
            }
            """));

        Register(registry, "WireTap", "Integration", "Sends a copy of the message to a secondary channel.",
            Schema("""
            {
              "properties": {
                "destinationChannel": { "type": "string", "label": "Destination Channel", "helpText": "Channel to send the message copy to", "required": true }
              },
              "required": ["destinationChannel"]
            }
            """));

        Register(registry, "DeadLetter", "Integration", "Routes failed messages to a dead letter channel.",
            Schema("""
            {
              "properties": {
                "channelName": { "type": "string", "label": "Channel Name", "helpText": "Dead letter channel name", "required": true },
                "maxRetries":  { "type": "number", "uiType": "number", "label": "Max Retries", "helpText": "Number of retries before sending to dead letter", "min": 0, "max": 100, "default": "3" }
              },
              "required": ["channelName"]
            }
            """));

        // ── AI/Agents ─────────────────────────────────────────────────
        Register(registry, "AgentLoopStep", "AI/Agents", "Runs an autonomous agent loop with tool access.",
            Schema("""
            {
              "properties": {
                "provider":      { "type": "string", "uiType": "providerSelect", "label": "AI Provider", "helpText": "The AI provider to use for this agent", "required": true },
                "model":         { "type": "string", "uiType": "modelSelect", "label": "Model", "helpText": "Model identifier (provider-specific)", "required": true, "dependsOn": "provider" },
                "systemPrompt":  { "type": "string", "uiType": "textarea", "label": "System Prompt", "helpText": "Instructions that define the agent's behavior and role", "rows": 6 },
                "maxIterations": { "type": "number", "uiType": "number", "label": "Max Iterations", "helpText": "Maximum number of agent loop iterations", "min": 1, "max": 100, "default": "10" },
                "tools":         { "type": "string", "uiType": "json", "label": "Tools", "helpText": "JSON array of tool definitions available to the agent", "rows": 6 }
              },
              "required": ["provider", "model"]
            }
            """));

        Register(registry, "AgentDecisionStep", "AI/Agents", "Uses an AI agent to make a routing decision.",
            Schema("""
            {
              "properties": {
                "provider": { "type": "string", "uiType": "providerSelect", "label": "AI Provider", "helpText": "The AI provider to use", "required": true },
                "model":    { "type": "string", "uiType": "modelSelect", "label": "Model", "helpText": "Model identifier", "dependsOn": "provider" },
                "prompt":   { "type": "string", "uiType": "textarea", "label": "Decision Prompt", "helpText": "Prompt describing the decision to make and available options", "required": true, "rows": 6 },
                "options":  { "type": "string", "uiType": "json", "label": "Decision Options", "helpText": "JSON array of possible decision outcomes", "rows": 4 }
              },
              "required": ["provider", "prompt"]
            }
            """));

        Register(registry, "AgentPlanStep", "AI/Agents", "Generates an execution plan using an AI agent.",
            Schema("""
            {
              "properties": {
                "provider":  { "type": "string", "uiType": "providerSelect", "label": "AI Provider", "helpText": "The AI provider to use", "required": true },
                "model":     { "type": "string", "uiType": "modelSelect", "label": "Model", "helpText": "Model identifier", "dependsOn": "provider" },
                "objective": { "type": "string", "uiType": "textarea", "label": "Objective", "helpText": "High-level objective for the agent to plan", "required": true, "rows": 4 },
                "maxSteps":  { "type": "number", "uiType": "number", "label": "Max Plan Steps", "helpText": "Maximum number of steps in the generated plan", "min": 1, "max": 50 }
              },
              "required": ["provider", "objective"]
            }
            """));

        Register(registry, "LlmCallStep", "AI/Agents", "Makes a direct call to a language model.",
            Schema("""
            {
              "properties": {
                "provider":    { "type": "string", "uiType": "providerSelect", "label": "AI Provider", "helpText": "The AI provider to use", "required": true },
                "model":       { "type": "string", "uiType": "modelSelect", "label": "Model", "helpText": "Model identifier", "required": true, "dependsOn": "provider" },
                "prompt":      { "type": "string", "uiType": "textarea", "label": "Prompt", "helpText": "The prompt to send to the language model", "required": true, "rows": 6 },
                "temperature": { "type": "number", "uiType": "slider", "label": "Temperature", "helpText": "Controls randomness: 0 = deterministic, 2 = very creative", "min": 0, "max": 2, "step": 0.1, "default": "0.7" },
                "maxTokens":   { "type": "number", "uiType": "number", "label": "Max Tokens", "helpText": "Maximum number of tokens in the response", "min": 1, "max": 128000 }
              },
              "required": ["provider", "model", "prompt"]
            }
            """));

        Register(registry, "ToolCallStep", "AI/Agents", "Invokes a registered tool/function.",
            Schema("""
            {
              "properties": {
                "toolName":   { "type": "string", "label": "Tool Name", "helpText": "Name of the registered tool to invoke", "required": true },
                "parameters": { "type": "string", "uiType": "json", "label": "Parameters", "helpText": "JSON object of parameters to pass to the tool", "rows": 6 }
              },
              "required": ["toolName"]
            }
            """));

        // ── Data ──────────────────────────────────────────────────────
        Register(registry, "DataMapStep", "Data", "Transforms data using field mapping rules.",
            Schema("""
            {
              "properties": {
                "mappings": { "type": "string", "uiType": "json", "label": "Field Mappings", "helpText": "JSON object mapping source fields to destination fields", "required": true, "rows": 8 }
              },
              "required": ["mappings"]
            }
            """));

        Register(registry, "FormatConvertStep", "Data", "Converts data between formats (JSON, XML, CSV, etc.).",
            Schema("""
            {
              "properties": {
                "sourceFormat": { "type": "string", "uiType": "select", "label": "Source Format", "helpText": "Format of the input data", "required": true, "options": ["JSON", "XML", "CSV", "YAML"] },
                "targetFormat": { "type": "string", "uiType": "select", "label": "Target Format", "helpText": "Desired output format", "required": true, "options": ["JSON", "XML", "CSV", "YAML"] }
              },
              "required": ["sourceFormat", "targetFormat"]
            }
            """));

        Register(registry, "SchemaValidateStep", "Data", "Validates data against a JSON schema.",
            Schema("""
            {
              "properties": {
                "schema": { "type": "string", "uiType": "json", "label": "JSON Schema", "helpText": "JSON schema to validate data against", "required": true, "rows": 10 }
              },
              "required": ["schema"]
            }
            """));

        Register(registry, "BatchProcessStep", "Data", "Processes data in configurable batch sizes.",
            Schema("""
            {
              "properties": {
                "batchSize": { "type": "number", "uiType": "number", "label": "Batch Size", "helpText": "Number of items to process per batch", "required": true, "min": 1, "max": 10000, "default": "100" }
              },
              "required": ["batchSize"]
            }
            """));

        // ── HTTP ──────────────────────────────────────────────────────
        Register(registry, "HttpStep", "HTTP", "Makes an HTTP request to an external service.",
            Schema("""
            {
              "properties": {
                "url":         { "type": "string", "label": "URL", "helpText": "The endpoint URL to send the request to", "required": true },
                "method":      { "type": "string", "uiType": "select", "label": "HTTP Method", "helpText": "HTTP method for the request", "options": ["GET", "POST", "PUT", "DELETE", "PATCH"], "default": "GET" },
                "headers":     { "type": "string", "uiType": "json", "label": "Headers", "helpText": "JSON object of HTTP headers (e.g. {\"Authorization\": \"Bearer ...\"})", "rows": 4 },
                "body":        { "type": "string", "uiType": "json", "label": "Request Body", "helpText": "Request body content (typically JSON)", "rows": 6 },
                "contentType": { "type": "string", "label": "Content Type", "helpText": "Content-Type header value", "default": "application/json" },
                "timeoutMs":   { "type": "number", "uiType": "number", "label": "Timeout (ms)", "helpText": "Request timeout in milliseconds", "min": 0, "max": 300000, "default": "30000" }
              },
              "required": ["url"]
            }
            """));

        Register(registry, "WebhookTriggerStep", "HTTP", "Waits for an incoming webhook request.",
            Schema("""
            {
              "properties": {
                "path":         { "type": "string", "label": "Webhook Path", "helpText": "URL path to listen on (e.g. /api/webhook/my-hook)", "required": true },
                "method":       { "type": "string", "uiType": "select", "label": "HTTP Method", "helpText": "Expected HTTP method", "options": ["GET", "POST", "PUT"], "default": "POST" },
                "responseBody": { "type": "string", "uiType": "json", "label": "Response Body", "helpText": "JSON response to return to the caller", "rows": 4 }
              },
              "required": ["path"]
            }
            """));

        // ── Events ────────────────────────────────────────────────────
        Register(registry, "PublishEventStep", "Events", "Publishes an event to the event bus.",
            Schema("""
            {
              "properties": {
                "eventType": { "type": "string", "label": "Event Type", "helpText": "Type name of the event to publish", "required": true },
                "payload":   { "type": "string", "uiType": "json", "label": "Event Payload", "helpText": "JSON payload data for the event", "rows": 6 }
              },
              "required": ["eventType"]
            }
            """));

        Register(registry, "WaitForEventStep", "Events", "Pauses execution until a matching event is received.",
            Schema("""
            {
              "properties": {
                "eventType": { "type": "string", "label": "Event Type", "helpText": "Type name of the event to wait for", "required": true },
                "timeoutMs": { "type": "number", "uiType": "number", "label": "Timeout (ms)", "helpText": "Maximum time to wait for the event (0 = wait indefinitely)", "min": 0, "max": 3600000 }
              },
              "required": ["eventType"]
            }
            """));

        // ── Human ─────────────────────────────────────────────────────
        Register(registry, "HumanTaskStep", "Human", "Creates a task for human completion.",
            Schema("""
            {
              "properties": {
                "assignee":    { "type": "string", "label": "Assignee", "helpText": "User or group to assign the task to", "required": true },
                "description": { "type": "string", "uiType": "textarea", "label": "Task Description", "helpText": "Detailed description of what needs to be done", "rows": 4 },
                "priority":    { "type": "string", "uiType": "select", "label": "Priority", "helpText": "Task priority level", "options": ["Low", "Medium", "High", "Critical"], "default": "Medium" },
                "dueDate":     { "type": "string", "label": "Due Date", "helpText": "Optional due date (ISO 8601 format)" }
              },
              "required": ["assignee"]
            }
            """));

        Register(registry, "ApprovalStep", "Human", "Pauses workflow pending human approval.",
            Schema("""
            {
              "properties": {
                "assignee":          { "type": "string", "label": "Approver", "helpText": "User or group who must approve", "required": true },
                "message":           { "type": "string", "uiType": "textarea", "label": "Approval Message", "helpText": "Message shown to the approver explaining what needs approval", "rows": 4 },
                "requiredApprovals": { "type": "number", "uiType": "number", "label": "Required Approvals", "helpText": "Number of approvals needed to proceed", "min": 1, "max": 100, "default": "1" }
              },
              "required": ["assignee"]
            }
            """));

        return registry;
    }

    private static void Register(StepTypeRegistry registry, string type, string category, string description, JsonElement? configSchema = null)
    {
        registry.Register(new StepTypeInfo
        {
            Type = type,
            Name = type,
            Category = category,
            Description = description,
            ConfigSchema = configSchema
        });
    }
}
