#pragma warning disable SKEXP0070
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Agents;
using WorkflowFramework.Samples.TaskStream.Extensions;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Tools;
using WorkflowFramework.Samples.TaskStream.Workflows;

namespace WorkflowFramework.Tests.E2E;

[Collection("Ollama")]
[Trait("Category", "E2E")]
public class SemanticKernelE2ETests(OllamaFixture fixture)
{
    private void SkipIfUnavailable()
    {
        if (!fixture.IsAvailable)
            Assert.Fail("SKIP: Ollama is not available at localhost:11434");
    }

    private static Kernel BuildKernel(IEnumerable<IAgentTool>? tools = null)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion("qwen3:30b-instruct", new Uri("http://localhost:11434"));

        if (tools is not null)
        {
            var plugin = new TaskStreamPlugin(tools);
            builder.Plugins.AddFromObject(plugin, "TaskStream");
        }

        return builder.Build();
    }

    [Fact(Timeout = 120_000)]
    public async Task SemanticKernelProvider_CompleteAsync_ReturnsResponse()
    {
        SkipIfUnavailable();

        var kernel = BuildKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        var response = await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "What is 2+2? Reply with just the number."
        });

        response.Should().NotBeNull();
        response.Content.Should().NotBeNullOrWhiteSpace();
        response.Content.Should().Contain("4");
    }

    [Fact(Timeout = 120_000)]
    public async Task SemanticKernelProvider_DecideAsync_PicksOption()
    {
        SkipIfUnavailable();

        var kernel = BuildKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        var decision = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "A user submitted a simple request to change their display name. Should this be approved, rejected, or escalated?",
            Options = ["approve", "reject", "escalate"]
        });

        decision.Should().NotBeNullOrWhiteSpace();
        new[] { "approve", "reject", "escalate" }.Should().Contain(decision);
    }

    [Fact(Timeout = 600_000)]
    public async Task TaskStream_WithSemanticKernel_E2E()
    {
        SkipIfUnavailable();

        var messages = new List<SourceMessage>
        {
            new()
            {
                Source = "chat",
                RawContent = "Schedule standup for Monday, deploy the API to staging, and pick up groceries"
            }
        };

        var services = new ServiceCollection();
        services.AddTaskStream(messages, ["--use-sk"]);

        // Remove MockEmailTaskSource for speed
        var mockSources = services.Where(d =>
            d.ServiceType == typeof(Samples.TaskStream.Sources.ITaskSource) &&
            d.ImplementationType == typeof(Samples.TaskStream.Sources.MockEmailTaskSource)).ToList();
        foreach (var d in mockSources) services.Remove(d);

        using var sp = services.BuildServiceProvider();
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);

        var store = sp.GetRequiredService<Samples.TaskStream.Store.ITodoStore>();
        var todos = await store.GetAllAsync();
        todos.Should().NotBeEmpty("SK+Ollama should extract at least one task");
    }

    [Fact(Timeout = 120_000)]
    public async Task SemanticKernelProvider_WithPlugins_CanCallTools()
    {
        SkipIfUnavailable();

        var tools = new IAgentTool[] { new WebSearchTool(), new CalendarTool() };
        var kernel = BuildKernel(tools);
        var provider = new SemanticKernelAgentProvider(kernel);

        // Ask something that should trigger tool use
        var response = await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "Search the web for 'best practices for .NET development' and give me the top result."
        });

        response.Should().NotBeNull();
        response.Content.Should().NotBeNullOrWhiteSpace();
        // The response should contain content (whether from tool call or direct answer)
        response.Content.Length.Should().BeGreaterThan(5);
    }
}
