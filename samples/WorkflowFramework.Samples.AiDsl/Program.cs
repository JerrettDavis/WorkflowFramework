using System.Text.Json;
using WorkflowFramework;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Serialization;

// ─────────────────────────────────────────────────────────────────────────────
// DSL-Emitter Pattern Demo
//
// This sample demonstrates how an LLM can dynamically emit workflow steps as
// JSON, which are then reviewed by a human and executed at runtime.
//
// Flow:
//   1. AgentProviderSelectorStep  – picks echo or ollama from the registry and
//                                   stores the resolved IAgentProvider in context
//   2. DslEmitterStep             – reads the provider from context (set by step 1)
//                                   and iteratively asks the LLM for step definitions
//   3. HumanApprovalGate          – prints emitted steps and asks for approval
//   4. BridgeStep                 – copies Emitter.EmittedSteps → Executor.Steps
//   5. WorkflowDslExecutorStep    – materialises and runs each emitted step
// ─────────────────────────────────────────────────────────────────────────────

// ── 1. Parse arguments ───────────────────────────────────────────────────────

var providerKey = "echo";
foreach (var arg in args)
{
    if (arg.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
        providerKey = arg["--provider=".Length..].Trim().ToLowerInvariant();
}

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║          WorkflowFramework  –  DSL-Emitter Demo          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  Provider : {providerKey}");
Console.WriteLine();

// ── 2. Build provider registry ───────────────────────────────────────────────

// Echo provider – two pre-queued responses:
//   • First response  : the DSL steps JSON array
//   • Second response : [] (done signal, ends the iteration loop)
const string dslStepsJson =
    """[{"name":"AnalyseInput","type":"action","config":{"message":"Analysing input data..."}},{"name":"GenerateSummary","type":"action","config":{"message":"Generating summary report..."}}]""";

var echoProvider = new EchoAgentProvider(new[] { dslStepsJson, "[]" });

var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                   ?? "http://localhost:11434";
var ollamaProvider = new OllamaAgentProvider(new OllamaOptions
{
    BaseUrl = ollamaBaseUrl,
    DefaultModel = "qwen2.5:1.5b",
    DisableThinking = true
});

var registry = new Dictionary<string, IAgentProvider>(StringComparer.OrdinalIgnoreCase)
{
    ["echo"]   = echoProvider,
    ["ollama"] = ollamaProvider
};

if (!registry.TryGetValue(providerKey, out var selectedProvider))
{
    Console.Error.WriteLine($"Unknown provider '{providerKey}'. Use --provider=echo or --provider=ollama.");
    return 1;
}

// ── 3. Build the DSL-emitter step ────────────────────────────────────────────

const string systemPrompt =
    """
    You are a workflow planning assistant.
    Respond ONLY with a valid JSON array of step definitions – no markdown, no prose.
    Each step must have "name" (string) and "type" (string, e.g. "action") fields.
    You may include an optional "config" object with arbitrary string key/value pairs.

    Example:
    [
      {"name":"FetchData","type":"action","config":{"source":"api"}},
      {"name":"Transform","type":"action","config":{"mode":"map"}}
    ]

    When you have emitted all desired steps, respond with exactly: []
    """;

var emitterStep = new DslEmitterStep(selectedProvider, new DslEmitterOptions
{
    StepName            = "Emitter",
    MaxIterations       = 5,
    SystemPromptTemplate = systemPrompt,
    IncludeSchemaInPrompt = false,
    DoneSignal          = "[]"
});

// ── 4. Build the workflow ─────────────────────────────────────────────────────

var workflow = Workflow.Create("DslEmitterDemo")
    // Step 1 – resolve the chosen provider and store it in context so DslEmitterStep picks it up
    .Step(new AgentProviderSelectorStep(registry, providerKey))

    // Step 2 – ask the LLM to emit DSL step definitions
    .Step(emitterStep)

    // Step 3 – human-in-the-loop approval gate
    .Step("HumanApprovalGate", ctx =>
    {
        Console.WriteLine("──────────────────────────────────────────────────────────");
        Console.WriteLine("  HUMAN APPROVAL GATE");
        Console.WriteLine("──────────────────────────────────────────────────────────");

        if (!ctx.Properties.TryGetValue("Emitter.EmittedSteps", out var stepsObj)
            || stepsObj is not List<StepDefinitionDto> emitted
            || emitted.Count == 0)
        {
            Console.WriteLine("  No steps were emitted. Nothing to approve.");
            ctx.Properties["HumanApproval.Approved"] = false;
            ctx.IsAborted = true;
            return Task.CompletedTask;
        }

        Console.WriteLine($"  The LLM emitted {emitted.Count} step(s):");
        Console.WriteLine();
        for (var i = 0; i < emitted.Count; i++)
        {
            var s = emitted[i];
            Console.Write($"  [{i + 1}] {s.Name}  (type={s.Type})");
            if (s.Config?.Count > 0)
            {
                var cfg = string.Join(", ", s.Config.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.Write($"  config={{ {cfg} }}");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.Write("  Approve and execute these steps? [Y/n]: ");

        var answer = Console.ReadLine()?.Trim() ?? string.Empty;
        var approved = answer.Length == 0
                    || answer.Equals("y", StringComparison.OrdinalIgnoreCase)
                    || answer.Equals("yes", StringComparison.OrdinalIgnoreCase);

        ctx.Properties["HumanApproval.Approved"] = approved;

        if (!approved)
        {
            Console.WriteLine();
            Console.WriteLine("  ✗ Execution aborted by user.");
            ctx.IsAborted = true;
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("  ✓ Approved – proceeding to execution.");
        }

        Console.WriteLine("──────────────────────────────────────────────────────────");
        Console.WriteLine();
        return Task.CompletedTask;
    })

    // Step 4 – bridge: copy Emitter.EmittedSteps → WorkflowDslExecutor.Steps
    .Step("BridgeStep", ctx =>
    {
        if (ctx.Properties.TryGetValue("Emitter.EmittedSteps", out var steps))
            ctx.Properties["WorkflowDslExecutor.Steps"] = steps;
        return Task.CompletedTask;
    })

    // Step 5 – execute the emitted steps
    .Step(new WorkflowDslExecutorStep())

    .Build();

// ── 5. Seed the context and run ───────────────────────────────────────────────

var context = new WorkflowContext();
context.Properties["provider"]             = providerKey;
context.Properties["DslEmitter.UserMessage"] =
    "Generate a simple two-step data processing workflow.";

Console.WriteLine("  Running workflow…");
Console.WriteLine();

WorkflowResult result;
try
{
    result = await workflow.ExecuteAsync(context);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Workflow threw an exception: {ex.Message}");
    return 1;
}

// ── 6. Print results ──────────────────────────────────────────────────────────

Console.WriteLine("══════════════════════════════════════════════════════════");
Console.WriteLine("  RESULTS");
Console.WriteLine("══════════════════════════════════════════════════════════");
Console.WriteLine($"  Workflow status  : {result.Status}");

if (context.Properties.TryGetValue("Emitter.Iterations", out var iterObj))
    Console.WriteLine($"  Emitter iterations: {iterObj}");

if (context.Properties.TryGetValue("WorkflowDslExecutor.ExecutedCount", out var countObj))
    Console.WriteLine($"  Executed steps   : {countObj}");

if (context.Properties.TryGetValue("WorkflowDslExecutor.Results", out var resultsObj)
    && resultsObj is string resultsStr && !string.IsNullOrEmpty(resultsStr))
    Console.WriteLine($"  Execution order  : {resultsStr}");

if (context.Properties.TryGetValue("HumanApproval.Approved", out var approvedObj))
    Console.WriteLine($"  Human approved   : {approvedObj}");

if (result.Errors.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("  Errors:");
    foreach (var err in result.Errors)
        Console.WriteLine($"    • {err.StepName}: {err.Exception.Message}");
}

Console.WriteLine("══════════════════════════════════════════════════════════");
Console.WriteLine();

return 0;
