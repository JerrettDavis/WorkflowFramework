using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Tests.E2E;

[Collection("Ollama")]
[Trait("Category", "E2E")]
public class AgentLoopOllamaE2ETests(OllamaFixture fixture, ITestOutputHelper output)
{
    private void SkipIfUnavailable()
    {
        if (!fixture.IsAvailable)
            Assert.Fail("SKIP: Ollama is not available at localhost:11434");
    }

    [Fact(Timeout = 120_000)]
    public async Task AgentLoop_CallsCalculatorTool_ReturnsCorrectAnswer()
    {
        SkipIfUnavailable();

        var registry = new ToolRegistry();
        registry.Register(new SimpleToolProvider(
            new ToolDefinition
            {
                Name = "calculate",
                Description = "Evaluates a math expression. Input: {\"expression\": \"2+2\"}. Returns the numeric result.",
                ParametersSchema = """{"type":"object","properties":{"expression":{"type":"string","description":"Math expression to evaluate"}},"required":["expression"]}"""
            },
            (name, argsJson) =>
            {
                var args = JsonDocument.Parse(argsJson).RootElement;
                var expr = args.GetProperty("expression").GetString()!;
                output.WriteLine($"Calculator called with: {expr}");
                // Simple eval for basic math
                var result = EvalSimpleMath(expr);
                return Task.FromResult(new ToolResult { Content = result.ToString() });
            }));

        var options = new AgentLoopOptions
        {
            StepName = "CalcAgent",
            SystemPrompt = "You are a helpful assistant. Use the calculate tool to answer math questions. After getting the result, state the final answer clearly.",
            MaxIterations = 5
        };

        var step = new AgentLoopStep(fixture.Provider, registry, options);
        var context = new WorkflowContext();
        context.Properties["task"] = "What is 137 multiplied by 42?";

        await step.ExecuteAsync(context);

        var response = context.Properties["CalcAgent.Response"] as string;
        var iterations = (int)context.Properties["CalcAgent.Iterations"]!;
        var toolResults = context.Properties["CalcAgent.ToolResults"] as List<ToolResult>;

        output.WriteLine($"Response: {response}");
        output.WriteLine($"Iterations: {iterations}");

        response.Should().NotBeNullOrWhiteSpace();
        response.Should().Contain("5754");
        toolResults.Should().NotBeEmpty("agent should have called the calculate tool");
        iterations.Should().BeGreaterThan(1, "should have at least one tool call iteration + final response");
    }

    [Fact(Timeout = 120_000)]
    public async Task AgentLoop_CallsMultipleTools_ProducesAnswer()
    {
        SkipIfUnavailable();

        var registry = new ToolRegistry();
        registry.Register(new SimpleToolProvider(
            [
                new ToolDefinition
                {
                    Name = "get_weather",
                    Description = "Gets current weather for a city. Input: {\"city\": \"London\"}. Returns weather description.",
                    ParametersSchema = """{"type":"object","properties":{"city":{"type":"string","description":"City name"}},"required":["city"]}"""
                },
                new ToolDefinition
                {
                    Name = "get_population",
                    Description = "Gets the population of a city. Input: {\"city\": \"London\"}. Returns population number.",
                    ParametersSchema = """{"type":"object","properties":{"city":{"type":"string","description":"City name"}},"required":["city"]}"""
                }
            ],
            (name, argsJson) =>
            {
                var args = JsonDocument.Parse(argsJson).RootElement;
                var city = args.GetProperty("city").GetString()!;
                output.WriteLine($"Tool '{name}' called for city: {city}");

                var content = name switch
                {
                    "get_weather" => $"The weather in {city} is sunny, 22°C with light winds.",
                    "get_population" => city.ToLower() switch
                    {
                        "tokyo" => "13,960,000",
                        "london" => "8,982,000",
                        "paris" => "2,161,000",
                        _ => "1,000,000"
                    },
                    _ => "Unknown tool"
                };
                return Task.FromResult(new ToolResult { Content = content });
            }));

        var options = new AgentLoopOptions
        {
            StepName = "CityAgent",
            SystemPrompt = "You are a helpful assistant. Use the available tools to answer questions. Call tools as needed, then give a final answer.",
            MaxIterations = 6
        };

        var step = new AgentLoopStep(fixture.Provider, registry, options);
        var context = new WorkflowContext();
        context.Properties["task"] = "What is the weather and population of Tokyo?";

        await step.ExecuteAsync(context);

        var response = context.Properties["CityAgent.Response"] as string;
        var toolResults = context.Properties["CityAgent.ToolResults"] as List<ToolResult>;

        output.WriteLine($"Response: {response}");
        output.WriteLine($"Tool calls: {toolResults?.Count}");

        response.Should().NotBeNullOrWhiteSpace();
        // Should mention Tokyo weather and population from tool results
        response!.Should().ContainAny("sunny", "22", "13,960,000", "13960000", "13.96");
        toolResults.Should().HaveCountGreaterThanOrEqualTo(2, "should call both weather and population tools");
    }

    [Fact(Timeout = 120_000)]
    public async Task AgentLoop_WithCheckpointStore_SavesCheckpoints()
    {
        SkipIfUnavailable();

        var checkpointStore = new InMemoryCheckpointStore();

        var registry = new ToolRegistry();
        registry.Register(new SimpleToolProvider(
            new ToolDefinition
            {
                Name = "calculate",
                Description = "Evaluates a math expression. Input: {\"expression\": \"5+3\"}. Returns the numeric result.",
                ParametersSchema = """{"type":"object","properties":{"expression":{"type":"string","description":"Math expression"}},"required":["expression"]}"""
            },
            (name, argsJson) =>
            {
                var args = JsonDocument.Parse(argsJson).RootElement;
                var expr = args.GetProperty("expression").GetString()!;
                output.WriteLine($"Calculator: {expr}");
                return Task.FromResult(new ToolResult { Content = EvalSimpleMath(expr).ToString() });
            }));

        var options = new AgentLoopOptions
        {
            StepName = "CheckpointAgent",
            SystemPrompt = "You are a math assistant. Use the calculate tool to answer the question, then state the final answer.",
            MaxIterations = 5,
            CheckpointStore = checkpointStore,
            CheckpointInterval = 1
        };

        var step = new AgentLoopStep(fixture.Provider, registry, options);
        var context = new WorkflowContext();
        context.Properties["task"] = "What is 25 times 4?";

        await step.ExecuteAsync(context);

        var response = context.Properties["CheckpointAgent.Response"] as string;
        output.WriteLine($"Response: {response}");
        response.Should().Contain("100");

        // Verify checkpoints were saved
        var checkpoints = await checkpointStore.ListAsync(context.WorkflowId);
        output.WriteLine($"Checkpoints saved: {checkpoints.Count}");
        checkpoints.Should().NotBeEmpty("checkpoints should be saved during tool-calling iterations");
    }

    private static double EvalSimpleMath(string expr)
    {
        // Strip whitespace and handle basic operations
        expr = expr.Replace(" ", "").Replace(",", "");

        // Try to parse as simple binary operation
        foreach (var op in new[] { '+', '-', '*', '/' })
        {
            // Find operator (skip if first char is minus for negative numbers)
            var idx = expr.LastIndexOf(op);
            if (idx <= 0) continue;
            if (op is '+' or '-' && idx > 0 && "eE".Contains(expr[idx - 1])) continue;

            if (double.TryParse(expr[..idx], out var left) && double.TryParse(expr[(idx + 1)..], out var right))
            {
                return op switch
                {
                    '+' => left + right,
                    '-' => left - right,
                    '*' => left * right,
                    '/' => left / right,
                    _ => 0
                };
            }
        }

        // Fallback: try parsing as number
        return double.TryParse(expr, out var val) ? val : 0;
    }
}

/// <summary>
/// Simple tool provider for testing — wraps tool definitions and a callback.
/// </summary>
file sealed class SimpleToolProvider : IToolProvider
{
    private readonly IReadOnlyList<ToolDefinition> _tools;
    private readonly Func<string, string, Task<ToolResult>> _handler;

    public SimpleToolProvider(ToolDefinition tool, Func<string, string, Task<ToolResult>> handler)
        : this([tool], handler) { }

    public SimpleToolProvider(IReadOnlyList<ToolDefinition> tools, Func<string, string, Task<ToolResult>> handler)
    {
        _tools = tools;
        _handler = handler;
    }

    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
        => Task.FromResult(_tools);

    public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
        => _handler(toolName, argumentsJson);
}

// ─── Goals 2-6 (E2E with real Ollama) ─────────────────────────────────────

/// <summary>
/// End-to-end tests for the DSL-emitter pipeline against a live local Ollama instance.
/// These tests are automatically skipped when Ollama is not reachable (via <see cref="OllamaFactAttribute"/>).
/// <para>
/// Prerequisite: <c>ollama pull qwen2.5:1.5b</c>
/// </para>
/// Goals covered: 2 (agent emits DSL), 3 (framework executes DSL), 5 (E2E with Ollama),
/// 6 (realistic "diagnose compilation" prompt → workflow steps, agent never calls tools).
/// </summary>
[Collection("Ollama")]
[Trait("Category", "E2E")]
public class DslEmitterOllamaTests(OllamaFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// Goal 5 + 6: a realistic "diagnose compilation errors" task should cause the model to
    /// emit at least one Workflow Framework DSL step describing an inspection/build action.
    /// The agent must NEVER call tools; it only emits DSL pipeline instructions.
    /// </summary>
    [OllamaFact(Timeout = 180_000)]   // ← OllamaFact – skips automatically if Ollama absent
    public async Task DslEmitter_EmitsAtLeastOneDslStep_ForCodebaseDiagnosisTask_AgenticDemo()
    {
        // Arrange – use the shared Ollama provider from the fixture
        var options = new DslEmitterOptions
        {
            StepName = "Diagnoser",
            MaxIterations = 3
        };
        var step = new DslEmitterStep(fixture.Provider, options);
        var context = new WorkflowContext();
        context.Properties["task"] = "Diagnose the errors in this codebase compilation. " +
            "Do NOT run anything yourself — emit only a JSON array of WorkflowFramework DSL steps.";

        // Act
        await step.ExecuteAsync(context);

        // Assert – the real model must emit at least one DSL step (goal 6)
        var emittedSteps = context.Properties["Diagnoser.EmittedSteps"] as System.Collections.IList;
        output.WriteLine($"Emitted step count: {emittedSteps?.Count ?? 0}");
        emittedSteps.Should().NotBeNull("provider should emit at least one DSL step");
        emittedSteps!.Count.Should().BeGreaterThan(0,
            "a diagnosis task should produce at least one inspection step");
    }

    /// <summary>
    /// Goal 6 (agent never calls tools directly): the emitter response must never contain
    /// raw tool invocations — only DSL instructions for the framework.
    /// </summary>
    [OllamaFact(Timeout = 180_000)]   // ← OllamaFact – skips automatically if Ollama absent
    public async Task DslEmitter_NeverSurfacesToolCalls_AgentOnlyEmitsDsl_AgenticDemo()
    {
        // Arrange
        var options = new DslEmitterOptions
        {
            StepName = "CodeDiagAgent",
            MaxIterations = 2
        };
        var step = new DslEmitterStep(fixture.Provider, options);
        var context = new WorkflowContext();
        context.Properties["task"] = "List the workflow steps needed to detect and report " +
            "build errors in a .NET project. Respond ONLY with a JSON array of DSL steps.";

        // Act
        await step.ExecuteAsync(context);

        // Assert – requirement 6: the framework must never proxy raw tool calls through the emitter
        context.Properties.Should().NotContainKey("CodeDiagAgent.ToolCalls",
            "the DslEmitterStep must absorb/ignore tool calls — only DSL pipeline steps are allowed");

        context.Properties.TryGetValue("CodeDiagAgent.Iterations", out var iterations);
        output.WriteLine($"Iterations: {iterations}");
        iterations.Should().NotBeNull("iteration count must be recorded");
    }
}
