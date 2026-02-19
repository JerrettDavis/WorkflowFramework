using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Samples.TaskStream.Extensions;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Sources;
using WorkflowFramework.Samples.TaskStream.Store;
using WorkflowFramework.Samples.TaskStream.Workflows;

namespace WorkflowFramework.Tests.E2E;

/// <summary>
/// E2E tests for the TaskStream sample app using mock providers (no Ollama required).
/// </summary>
[Trait("Category", "E2E")]
public class TaskStreamE2ETests
{
    private static ServiceProvider BuildServiceProvider(List<SourceMessage> messages)
    {
        var services = new ServiceCollection();
        services.AddTaskStream(messages);

        // Remove MockEmailTaskSource to keep tests deterministic
        var mockSources = services.Where(d =>
            d.ServiceType == typeof(ITaskSource) &&
            d.ImplementationType == typeof(MockEmailTaskSource)).ToList();
        foreach (var d in mockSources) services.Remove(d);

        return services.BuildServiceProvider();
    }

    [Fact(Timeout = 30_000)]
    public async Task TaskStream_MockProviders_CompletesSuccessfully()
    {
        var messages = new List<SourceMessage>
        {
            new() { Source = "chat", RawContent = "Schedule standup for Monday, deploy the API to staging, and pick up groceries" }
        };

        using var sp = BuildServiceProvider(messages);
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact(Timeout = 30_000)]
    public async Task TaskStream_MockProviders_ExtractsTasks()
    {
        var messages = new List<SourceMessage>
        {
            new() { Source = "chat", RawContent = "Schedule standup for Monday, deploy the API to staging, and pick up groceries" }
        };

        using var sp = BuildServiceProvider(messages);
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        await orchestrator.ExecuteAsync();

        var store = sp.GetRequiredService<ITodoStore>();
        var todos = await store.GetAllAsync();
        todos.Should().NotBeEmpty("mock provider should extract tasks from the message");
    }

    [Fact(Timeout = 30_000)]
    public async Task TaskStream_MockProviders_TriagesAndExecutesTasks()
    {
        var messages = new List<SourceMessage>
        {
            new() { Source = "chat", RawContent = "Schedule standup for Monday, deploy the API to staging, and pick up groceries" }
        };

        using var sp = BuildServiceProvider(messages);
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);

        var store = sp.GetRequiredService<ITodoStore>();
        var todos = await store.GetAllAsync();

        // Verify tasks have been categorized (triaged)
        todos.Should().OnlyContain(t => Enum.IsDefined(t.Category),
            "all tasks should be triaged to a valid category");
    }

    [Fact(Timeout = 30_000)]
    public async Task TaskStream_MockProviders_GeneratesMarkdownReport()
    {
        var messages = new List<SourceMessage>
        {
            new() { Source = "chat", RawContent = "Schedule standup for Monday, deploy the API to staging" }
        };

        using var sp = BuildServiceProvider(messages);
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Context.Properties.Should().ContainKey("markdownReport");

        var report = result.Context.Properties["markdownReport"] as string;
        report.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(Timeout = 30_000)]
    public async Task TaskStream_MultipleMessages_ProcessesAll()
    {
        var messages = new List<SourceMessage>
        {
            new() { Source = "chat", RawContent = "Schedule team standup Monday 9am, pick up milk" },
            new() { Source = "chat", RawContent = "Review PR #87 for the auth module" },
            new() { Source = "chat", RawContent = "Buy birthday present for Sarah, update CI pipeline" }
        };

        using var sp = BuildServiceProvider(messages);
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
        var result = await orchestrator.ExecuteAsync();

        result.Status.Should().Be(WorkflowStatus.Completed);

        var store = sp.GetRequiredService<ITodoStore>();
        var todos = await store.GetAllAsync();
        todos.Count.Should().BeGreaterThanOrEqualTo(3,
            "multiple messages should produce multiple extracted tasks");
    }
}
