namespace WorkflowFramework.Dashboard.Web.Models;

public sealed class StepDefinition
{
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Category { get; init; } = "";
    public string Color { get; init; } = "";
    public List<StepProperty> Properties { get; init; } = [];
}

public sealed class StepProperty
{
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Type { get; init; } = "string"; // string, number, bool, select
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public List<string>? Options { get; init; }
}

public sealed class WorkflowNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string StepType { get; set; } = "";
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object?> Config { get; set; } = new();
    public string? RunStatus { get; set; }
}

public sealed class WorkflowEdge
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Label { get; set; }
}

public static class StepCatalog
{
    public static readonly Dictionary<string, string> CategoryColors = new()
    {
        ["Core"] = "#3b82f6",
        ["Integration"] = "#22c55e",
        ["AI/Agents"] = "#a855f7",
        ["Data"] = "#f97316",
        ["HTTP"] = "#14b8a6",
        ["Events"] = "#eab308",
        ["Human"] = "#ec4899",
    };

    public static List<StepDefinition> GetAll() =>
    [
        // Core
        new() { Type = "Action", Name = "Action", Icon = "⬡", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "expression", Label = "Expression" }] },
        new() { Type = "Conditional", Name = "Conditional", Icon = "◇", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "expression", Label = "Expression", Required = true }, new() { Name = "thenStep", Label = "Then Step" }, new() { Name = "elseStep", Label = "Else Step" }] },
        new() { Type = "Parallel", Name = "Parallel", Icon = "⫘", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "maxConcurrency", Label = "Max Concurrency", Type = "number" }] },
        new() { Type = "ForEach", Name = "ForEach", Icon = "↻", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "collectionExpression", Label = "Collection Expression", Required = true }, new() { Name = "itemVariable", Label = "Item Variable" }] },
        new() { Type = "While", Name = "While", Icon = "↻", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "expression", Label = "Expression", Required = true }] },
        new() { Type = "DoWhile", Name = "DoWhile", Icon = "↻", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "expression", Label = "Expression", Required = true }] },
        new() { Type = "Retry", Name = "Retry", Icon = "🔄", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "maxAttempts", Label = "Max Attempts", Type = "number", DefaultValue = "3" }, new() { Name = "delayMs", Label = "Delay (ms)", Type = "number", DefaultValue = "1000" }, new() { Name = "backoffMultiplier", Label = "Backoff Multiplier", Type = "number" }] },
        new() { Type = "Timeout", Name = "Timeout", Icon = "⏱", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "durationMs", Label = "Duration (ms)", Type = "number", Required = true }] },
        new() { Type = "Delay", Name = "Delay", Icon = "⏱", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "durationMs", Label = "Duration (ms)", Type = "number", Required = true }] },
        new() { Type = "TryCatch", Name = "TryCatch", Icon = "🛡", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "catchTypes", Label = "Catch Types (comma-separated)" }] },
        new() { Type = "SubWorkflow", Name = "SubWorkflow", Icon = "📦", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "workflowName", Label = "Workflow Name", Required = true }] },
        new() { Type = "Saga", Name = "Saga", Icon = "📜", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "compensateOnFailure", Label = "Compensate On Failure", Type = "bool", DefaultValue = "true" }] },
        // Integration
        new() { Type = "ContentBasedRouter", Name = "Content Router", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "routingExpression", Label = "Routing Expression", Required = true }] },
        new() { Type = "MessageFilter", Name = "Message Filter", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "filterExpression", Label = "Filter Expression", Required = true }] },
        new() { Type = "RecipientList", Name = "Recipient List", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "recipientExpression", Label = "Recipient Expression", Required = true }] },
        new() { Type = "Splitter", Name = "Splitter", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "splitExpression", Label = "Split Expression", Required = true }, new() { Name = "aggregateResults", Label = "Aggregate Results", Type = "bool" }] },
        new() { Type = "Aggregator", Name = "Aggregator", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "correlationExpression", Label = "Correlation Expression", Required = true }, new() { Name = "completionSize", Label = "Completion Size", Type = "number" }, new() { Name = "completionTimeout", Label = "Completion Timeout", Type = "number" }] },
        new() { Type = "ScatterGather", Name = "Scatter Gather", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "timeout", Label = "Timeout", Type = "number" }, new() { Name = "aggregationStrategy", Label = "Aggregation Strategy" }] },
        new() { Type = "WireTap", Name = "Wire Tap", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "destinationChannel", Label = "Destination Channel", Required = true }] },
        new() { Type = "DeadLetter", Name = "Dead Letter", Icon = "🔀", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "channelName", Label = "Channel Name", Required = true }, new() { Name = "maxRetries", Label = "Max Retries", Type = "number" }] },
        // AI/Agents
        new() { Type = "AgentLoopStep", Name = "Agent Loop", Icon = "🧠", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "provider", Label = "Provider", Required = true }, new() { Name = "model", Label = "Model", Required = true }, new() { Name = "systemPrompt", Label = "System Prompt" }, new() { Name = "maxIterations", Label = "Max Iterations", Type = "number", DefaultValue = "10" }, new() { Name = "tools", Label = "Tools (comma-separated)" }] },
        new() { Type = "AgentDecisionStep", Name = "Agent Decision", Icon = "🤖", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "provider", Label = "Provider", Required = true }, new() { Name = "model", Label = "Model" }, new() { Name = "prompt", Label = "Prompt", Required = true }, new() { Name = "options", Label = "Options (comma-separated)" }] },
        new() { Type = "AgentPlanStep", Name = "Agent Plan", Icon = "🤖", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "provider", Label = "Provider", Required = true }, new() { Name = "model", Label = "Model" }, new() { Name = "objective", Label = "Objective", Required = true }, new() { Name = "maxSteps", Label = "Max Steps", Type = "number" }] },
        new() { Type = "LlmCallStep", Name = "LLM Call", Icon = "🤖", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "provider", Label = "Provider", Required = true }, new() { Name = "model", Label = "Model", Required = true }, new() { Name = "prompt", Label = "Prompt", Required = true }, new() { Name = "temperature", Label = "Temperature", Type = "number" }, new() { Name = "maxTokens", Label = "Max Tokens", Type = "number" }] },
        new() { Type = "ToolCallStep", Name = "Tool Call", Icon = "🔧", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "toolName", Label = "Tool Name", Required = true }, new() { Name = "parameters", Label = "Parameters (JSON)" }] },
        // Data
        new() { Type = "DataMapStep", Name = "Data Map", Icon = "🗺", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "mappings", Label = "Mappings (JSON)", Required = true }] },
        new() { Type = "FormatConvertStep", Name = "Format Convert", Icon = "🔄", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "sourceFormat", Label = "Source Format", Type = "select", Required = true, Options = ["JSON", "XML", "CSV", "YAML"] }, new() { Name = "targetFormat", Label = "Target Format", Type = "select", Required = true, Options = ["JSON", "XML", "CSV", "YAML"] }] },
        new() { Type = "SchemaValidateStep", Name = "Schema Validate", Icon = "📊", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "schema", Label = "Schema (JSON)", Required = true }] },
        new() { Type = "BatchProcessStep", Name = "Batch Process", Icon = "📊", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "batchSize", Label = "Batch Size", Type = "number", Required = true, DefaultValue = "100" }] },
        // HTTP
        new() { Type = "HttpStep", Name = "HTTP Request", Icon = "🌐", Category = "HTTP", Color = CategoryColors["HTTP"],
            Properties = [new() { Name = "url", Label = "URL", Required = true }, new() { Name = "method", Label = "Method", Type = "select", DefaultValue = "GET", Options = ["GET", "POST", "PUT", "DELETE", "PATCH"] }, new() { Name = "headers", Label = "Headers (JSON)" }, new() { Name = "body", Label = "Body" }, new() { Name = "contentType", Label = "Content Type" }, new() { Name = "timeoutMs", Label = "Timeout (ms)", Type = "number" }] },
        new() { Type = "WebhookTriggerStep", Name = "Webhook Trigger", Icon = "📡", Category = "HTTP", Color = CategoryColors["HTTP"],
            Properties = [new() { Name = "path", Label = "Path", Required = true }, new() { Name = "method", Label = "Method", Type = "select", DefaultValue = "POST", Options = ["GET", "POST", "PUT"] }, new() { Name = "responseBody", Label = "Response Body" }] },
        // Events
        new() { Type = "PublishEventStep", Name = "Publish Event", Icon = "📣", Category = "Events", Color = CategoryColors["Events"],
            Properties = [new() { Name = "eventType", Label = "Event Type", Required = true }, new() { Name = "payload", Label = "Payload" }] },
        new() { Type = "WaitForEventStep", Name = "Wait For Event", Icon = "⚡", Category = "Events", Color = CategoryColors["Events"],
            Properties = [new() { Name = "eventType", Label = "Event Type", Required = true }, new() { Name = "timeoutMs", Label = "Timeout (ms)", Type = "number" }] },
        // Human
        new() { Type = "HumanTaskStep", Name = "Human Task", Icon = "👤", Category = "Human", Color = CategoryColors["Human"],
            Properties = [new() { Name = "assignee", Label = "Assignee", Required = true }, new() { Name = "description", Label = "Description" }, new() { Name = "priority", Label = "Priority", Type = "select", Options = ["Low", "Medium", "High", "Critical"] }, new() { Name = "dueDate", Label = "Due Date" }] },
        new() { Type = "ApprovalStep", Name = "Approval", Icon = "✅", Category = "Human", Color = CategoryColors["Human"],
            Properties = [new() { Name = "assignee", Label = "Assignee", Required = true }, new() { Name = "message", Label = "Message" }, new() { Name = "requiredApprovals", Label = "Required Approvals", Type = "number", DefaultValue = "1" }] },
    ];
}
