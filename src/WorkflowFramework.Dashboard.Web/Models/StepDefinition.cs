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
        ["Human Tasks"] = "#ec4899",
    };

    public static List<StepDefinition> GetAll() =>
    [
        // Core
        new() { Type = "start", Name = "Start", Icon = "▶", Category = "Core", Color = CategoryColors["Core"],
            Properties = [] },
        new() { Type = "end", Name = "End", Icon = "⏹", Category = "Core", Color = CategoryColors["Core"],
            Properties = [] },
        new() { Type = "condition", Name = "Condition", Icon = "◇", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "expression", Label = "Expression", Required = true }] },
        new() { Type = "delay", Name = "Delay", Icon = "⏱", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "duration", Label = "Duration (ms)", Type = "number", DefaultValue = "1000" }] },
        new() { Type = "parallel", Name = "Parallel", Icon = "⫘", Category = "Core", Color = CategoryColors["Core"],
            Properties = [] },
        new() { Type = "loop", Name = "Loop", Icon = "↻", Category = "Core", Color = CategoryColors["Core"],
            Properties = [new() { Name = "count", Label = "Iterations", Type = "number" }, new() { Name = "expression", Label = "While Expression" }] },
        // Integration
        new() { Type = "database", Name = "Database Query", Icon = "🗄", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "connectionString", Label = "Connection String", Required = true }, new() { Name = "query", Label = "SQL Query", Required = true }] },
        new() { Type = "grpc", Name = "gRPC Call", Icon = "⚡", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "endpoint", Label = "Endpoint", Required = true }, new() { Name = "method", Label = "Method", Required = true }] },
        new() { Type = "message_publish", Name = "Publish Message", Icon = "📤", Category = "Integration", Color = CategoryColors["Integration"],
            Properties = [new() { Name = "topic", Label = "Topic", Required = true }, new() { Name = "payload", Label = "Payload" }] },
        // AI/Agents
        new() { Type = "llm", Name = "LLM Prompt", Icon = "🤖", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "model", Label = "Model", DefaultValue = "gpt-4" }, new() { Name = "prompt", Label = "Prompt", Required = true }] },
        new() { Type = "agent", Name = "Agent Task", Icon = "🧠", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "agentId", Label = "Agent ID", Required = true }, new() { Name = "task", Label = "Task Description", Required = true }] },
        new() { Type = "mcp_tool", Name = "MCP Tool", Icon = "🔧", Category = "AI/Agents", Color = CategoryColors["AI/Agents"],
            Properties = [new() { Name = "toolName", Label = "Tool Name", Required = true }, new() { Name = "parameters", Label = "Parameters (JSON)" }] },
        // Data
        new() { Type = "transform", Name = "Transform", Icon = "🔄", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "expression", Label = "Transform Expression", Required = true }] },
        new() { Type = "map", Name = "Data Map", Icon = "🗺", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "mappings", Label = "Field Mappings (JSON)" }] },
        new() { Type = "filter", Name = "Filter", Icon = "🔍", Category = "Data", Color = CategoryColors["Data"],
            Properties = [new() { Name = "expression", Label = "Filter Expression", Required = true }] },
        // HTTP
        new() { Type = "http_request", Name = "HTTP Request", Icon = "🌐", Category = "HTTP", Color = CategoryColors["HTTP"],
            Properties = [new() { Name = "url", Label = "URL", Required = true }, new() { Name = "method", Label = "Method", Type = "select", DefaultValue = "GET", Options = ["GET", "POST", "PUT", "DELETE", "PATCH"] }, new() { Name = "headers", Label = "Headers (JSON)" }, new() { Name = "body", Label = "Body" }] },
        new() { Type = "webhook", Name = "Webhook Trigger", Icon = "📡", Category = "HTTP", Color = CategoryColors["HTTP"],
            Properties = [new() { Name = "path", Label = "Path", Required = true }, new() { Name = "method", Label = "Method", Type = "select", DefaultValue = "POST", Options = ["GET", "POST", "PUT"] }] },
        // Events
        new() { Type = "event_trigger", Name = "Event Trigger", Icon = "⚡", Category = "Events", Color = CategoryColors["Events"],
            Properties = [new() { Name = "eventType", Label = "Event Type", Required = true }] },
        new() { Type = "event_emit", Name = "Emit Event", Icon = "📣", Category = "Events", Color = CategoryColors["Events"],
            Properties = [new() { Name = "eventType", Label = "Event Type", Required = true }, new() { Name = "payload", Label = "Payload" }] },
        // Human Tasks
        new() { Type = "approval", Name = "Approval", Icon = "✅", Category = "Human Tasks", Color = CategoryColors["Human Tasks"],
            Properties = [new() { Name = "assignee", Label = "Assignee", Required = true }, new() { Name = "message", Label = "Message" }] },
        new() { Type = "form", Name = "User Form", Icon = "📋", Category = "Human Tasks", Color = CategoryColors["Human Tasks"],
            Properties = [new() { Name = "formSchema", Label = "Form Schema (JSON)", Required = true }] },
    ];
}
