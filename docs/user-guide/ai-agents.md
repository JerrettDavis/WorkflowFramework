# AI & Agent Integration

WorkflowFramework ships an `Extensions.AI` package that lets you embed LLM calls, AI-driven routing decisions, and autonomous planning directly into workflows.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.AI
```

## Key Types

| Type | Purpose |
|------|---------|
| `IAgentProvider` | Abstraction over any LLM backend |
| `LlmCallStep` | Call an LLM with a prompt template |
| `AgentDecisionStep` | AI picks a route from a set of options |
| `AgentPlanStep` | AI suggests next workflow steps |
| `OllamaAgentProvider` | Local LLM via Ollama |
| `SemanticKernelAgentProvider` | Microsoft Semantic Kernel interop |

## IAgentProvider

```csharp
using WorkflowFramework.Extensions.AI;

public interface IAgentProvider
{
    string Name { get; }
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);
    Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken ct = default);
}
```

## LlmCallStep — Prompt-Based Step

Sends a prompt to the configured provider and stores the response in context properties.

```csharp
var provider = new OllamaAgentProvider(new OllamaOptions
{
    BaseUrl = "http://localhost:11434",
    DefaultModel = "qwen3:30b-instruct"
});

var workflow = new WorkflowBuilder()
    .Step(new LlmCallStep(provider, new LlmCallOptions
    {
        StepName = "Summarize",
        PromptTemplate = "Summarize the following text: {{InputText}}",
        Temperature = 0.3,
        MaxTokens = 500
    }))
    .Build();

await workflow.RunAsync(context);

var summary = context.Properties["Summarize.Response"];  // LLM output
var tokens  = context.Properties["Summarize.TotalTokens"]; // usage info
```

## AgentDecisionStep — AI Routing

The agent picks one of the given options based on context, enabling dynamic branching:

```csharp
var workflow = new WorkflowBuilder()
    .Step(new AgentDecisionStep(provider, new AgentDecisionOptions
    {
        Prompt = "Should this support ticket be escalated or auto-replied?",
        Options = { "escalate", "auto-reply" }
    }))
    .If(ctx => ctx.Properties["AgentDecision.Decision"]?.ToString() == "escalate")
        .Step(escalateStep)
    .Else()
        .Step(autoReplyStep)
    .EndIf()
    .Build();
```

## AgentPlanStep — Autonomous Planning

Asks the LLM to suggest next steps based on the current workflow state:

```csharp
var workflow = new WorkflowBuilder()
    .Step(new AgentPlanStep(provider, "Planner"))
    .Build();

await workflow.RunAsync(context);
var plan = context.Properties["Planner.Plan"]; // LLM's suggested plan
```

## Tool Calling

`LlmCallStep` supports tool/function definitions. When the LLM requests tool calls, they are stored in context:

```csharp
var step = new LlmCallStep(provider, new LlmCallOptions
{
    PromptTemplate = "Look up the customer's order status.",
    Tools =
    {
        new AgentTool
        {
            Name = "get_order",
            Description = "Retrieves order details by ID",
            ParametersSchema = """{"type":"object","properties":{"orderId":{"type":"string"}}}"""
        }
    }
});
```

After execution, check `context.Properties["LlmCall.ToolCalls"]` for any `ToolCall` objects the model returned.

## OllamaAgentProvider

Connects to a local [Ollama](https://ollama.com) instance:

```csharp
var provider = new OllamaAgentProvider(new OllamaOptions
{
    BaseUrl = "http://localhost:11434",
    DefaultModel = "qwen3:30b-instruct",
    Timeout = TimeSpan.FromSeconds(120),
    DisableThinking = true  // appends /no_think for supported models
});
```

## SemanticKernelAgentProvider

Wraps a Microsoft Semantic Kernel `Kernel` for access to any SK-supported backend (OpenAI, Azure OpenAI, Hugging Face, etc.):

> [!NOTE]
> Available on .NET 8+ only.

```csharp
using Microsoft.SemanticKernel;
using WorkflowFramework.Extensions.AI;

var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o", apiKey)
    .Build();

var provider = new SemanticKernelAgentProvider(kernel);

// Use it exactly like OllamaAgentProvider
var step = new LlmCallStep(provider, new LlmCallOptions
{
    PromptTemplate = "Classify this email: {{EmailBody}}"
});
```

If the kernel has plugins registered, `FunctionChoiceBehavior.Auto()` is enabled automatically for tool calling.
