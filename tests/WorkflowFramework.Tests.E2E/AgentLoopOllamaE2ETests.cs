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
