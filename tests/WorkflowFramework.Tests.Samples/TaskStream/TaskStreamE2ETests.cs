using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Samples.TaskStream.Extensions;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Workflows;

namespace WorkflowFramework.Tests.Samples.TaskStream;

[Trait("Category", "SampleE2E")]
public sealed class TaskStreamE2ETests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var sampleMessages = new List<SourceMessage>
        {
            new()
            {
                Source = "chat",
                RawContent = "Schedule team standup Monday 9am, pick up milk on the way home, deploy v2.1 hotfix to staging"
            },
            new()
            {
                Source = "chat",
                RawContent = "Review PR #87 for the auth module, send quarterly report to finance team"
            },
            new()
            {
                Source = "chat",
                RawContent = "Buy birthday present for Sarah, update the CI pipeline to use .NET 10"
            }
        };

        var services = new ServiceCollection();
        services.AddTaskStream(sampleMessages);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TaskStream_WithMockProvider_RunsFullPipeline()
    {
        using var sp = BuildServiceProvider();
        var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();

        var result = await orchestrator.ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Context.Errors.Should().BeEmpty();

        // Verify all major pipeline outputs exist
        result.Context.Properties.Should().ContainKey("sourceMessages");
        result.Context.Properties.Should().ContainKey("normalizedMessages");
        result.Context.Properties.Should().ContainKey("extractedTodos");
        result.Context.Properties.Should().ContainKey("validatedTodos");
        result.Context.Properties.Should().ContainKey("markdownReport");

        var report = (string)result.Context.Properties["markdownReport"]!;
        report.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TaskStream_Extraction_ProducesStructuredOutput()
    {
        using var sp = BuildServiceProvider();

        var collectStep = sp.GetRequiredService<WorkflowFramework.Samples.TaskStream.Steps.CollectSourcesStep>();
        var normalizeStep = sp.GetRequiredService<WorkflowFramework.Samples.TaskStream.Steps.NormalizeInputStep>();
        var extractStep = sp.GetRequiredService<WorkflowFramework.Samples.TaskStream.Steps.ExtractTodosStep>();
        var validateStep = sp.GetRequiredService<WorkflowFramework.Samples.TaskStream.Steps.ValidateAndDeduplicateStep>();
        var persistStep = sp.GetRequiredService<WorkflowFramework.Samples.TaskStream.Steps.PersistTodosStep>();

        var extraction = ExtractionWorkflow.Build(collectStep, normalizeStep, extractStep, validateStep, persistStep);

        var context = new WorkflowContext();
        var result = await extraction.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties.Should().ContainKey("extractedTodos");

        var todos = context.Properties["extractedTodos"] as List<TodoItem>;
        todos.Should().NotBeNull();
        todos!.Should().HaveCountGreaterThan(0);

        // Verify todos have categorized content
        foreach (var todo in todos)
        {
            todo.Title.Should().NotBeNullOrEmpty();
            todo.Category.Should().NotBe(default(TaskCategory));
        }
    }
}
