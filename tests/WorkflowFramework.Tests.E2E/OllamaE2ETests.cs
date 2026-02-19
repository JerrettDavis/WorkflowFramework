using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Samples.TaskStream.Extensions;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Workflows;

namespace WorkflowFramework.Tests.E2E;

[Collection("Ollama")]
[Trait("Category", "E2E")]
public class OllamaE2ETests
{
    private readonly OllamaFixture _fixture;

    public OllamaE2ETests(OllamaFixture fixture) => _fixture = fixture;

    private void SkipIfUnavailable()
    {
        if (!_fixture.IsAvailable)
            Assert.Fail("SKIP: Ollama is not available at localhost:11434");
    }

    [Fact(Timeout = 60_000)]
    public async Task LlmCallStep_WithOllama_ReturnsResponse()
    {
        SkipIfUnavailable();

        var step = new LlmCallStep(_fixture.Provider, new LlmCallOptions
        {
            StepName = "Math",
            PromptTemplate = "What is 2+2? Reply with just the number."
        });

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        var response = context.Properties["Math.Response"] as string;
        response.Should().NotBeNullOrWhiteSpace();
        response.Should().Contain("4");
    }

    [Fact(Timeout = 120_000)]
    public async Task AgentDecisionStep_WithOllama_PicksRoute()
    {
        SkipIfUnavailable();

        var step = new AgentDecisionStep(_fixture.Provider, new AgentDecisionOptions
        {
            StepName = "Route",
            Prompt = "A user submitted a simple request to change their display name. Should this be approved, rejected, or escalated?",
            Options = ["approve", "reject", "escalate"]
        });

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        var decision = context.Properties["Route.Decision"] as string;
        decision.Should().NotBeNullOrWhiteSpace();
        new[] { "approve", "reject", "escalate" }.Should().Contain(decision);
    }

    [Fact(Timeout = 120_000)]
    public async Task MultiStepWorkflow_WithOllama_E2E()
    {
        SkipIfUnavailable();

        var provider = _fixture.Provider;

        var extract = new LlmCallStep(provider, new LlmCallOptions
        {
            StepName = "Extract",
            PromptTemplate = "Extract the main topic from this text in one word. Text: \"The quarterly sales report shows a 15% increase in revenue.\""
        });

        var decide = new AgentDecisionStep(provider, new AgentDecisionOptions
        {
            StepName = "Categorize",
            Prompt = "Based on the extracted topic, categorize this as: finance, engineering, or marketing.",
            Options = ["finance", "engineering", "marketing"]
        });

        var format = new LlmCallStep(provider, new LlmCallOptions
        {
            StepName = "Format",
            PromptTemplate = "Write a one-sentence summary. Keep it under 20 words."
        });

        var workflow = Workflow.Create("MultiStep")
            .Step(extract)
            .Step(decide)
            .Step(format)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        (context.Properties["Extract.Response"] as string).Should().NotBeNullOrWhiteSpace();
        var category = context.Properties["Categorize.Decision"] as string;
        new[] { "finance", "engineering", "marketing" }.Should().Contain(category);
        (context.Properties["Format.Response"] as string).Should().NotBeNullOrWhiteSpace();
    }

    [Fact(Timeout = 600_000)]
    public async Task TaskStream_WithOllama_E2E()
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
        services.AddTaskStream(messages);

        // Replace the agent provider with Ollama — remove existing registrations first
        var existing = services.Where(d => d.ServiceType == typeof(IAgentProvider)).ToList();
        foreach (var d in existing) services.Remove(d);
        services.AddSingleton<IAgentProvider>(_fixture.Provider);

        // Remove MockEmailTaskSource to keep the test fast (only our 1 message)
        var mockSources = services.Where(d =>
            d.ServiceType == typeof(Samples.TaskStream.Sources.ITaskSource) &&
            d.ImplementationType == typeof(Samples.TaskStream.Sources.MockEmailTaskSource)).ToList();
        foreach (var d in mockSources) services.Remove(d);

        using var sp = services.BuildServiceProvider();
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);

        // Verify tasks were extracted
        var store = sp.GetRequiredService<Samples.TaskStream.Store.ITodoStore>();
        var todos = await store.GetAllAsync();
        todos.Should().NotBeEmpty("Ollama should extract at least one task");
        todos.Count.Should().BeGreaterThanOrEqualTo(2, "the message contains at least 3 distinct tasks");
    }

    [Fact(Timeout = 60_000)]
    public async Task AgentPlanStep_WithOllama_GeneratesPlan()
    {
        SkipIfUnavailable();

        var step = new AgentPlanStep(_fixture.Provider, "Planner");

        var context = new WorkflowContext();
        context.Properties["goal"] = "Deploy a new microservice to production";
        context.Properties["constraints"] = "Must pass all tests, needs security review";

        await step.ExecuteAsync(context);

        var plan = context.Properties["Planner.Plan"] as string;
        plan.Should().NotBeNullOrWhiteSpace();
        plan!.Length.Should().BeGreaterThan(20, "plan should contain meaningful content");
    }
}
