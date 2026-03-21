using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public sealed class SampleWorkflowSeederTests
{
    [Fact]
    public async Task SeedAsync_IncludesRunnableLocalOllamaSmokeWorkflow()
    {
        var store = new InMemoryWorkflowDefinitionStore();

        await SampleWorkflowSeeder.SeedAsync(store);

        var workflow = (await store.GetAllAsync())
            .SingleOrDefault(item => item.Definition.Name == "Local Ollama Smoke Test");

        workflow.Should().NotBeNull();
        workflow!.Tags.Should().Contain(["ollama", "smoke", "local-first"]);
        workflow.Definition.Steps.Should().HaveCount(3);

        var llmStep = workflow.Definition.Steps.Single(step => step.Name == "GenerateLocalReply");
        llmStep.Type.Should().Be("LlmCallStep");
        llmStep.Config.Should().ContainKey("prompt");
        llmStep.Config!["prompt"].Should().Contain("{{PrepareContext.Expression}}");
        llmStep.Config.Should().ContainKey("provider");
        llmStep.Config["provider"].Should().Be("ollama");
        llmStep.Config.Should().ContainKey("model");
        llmStep.Config["model"].Should().Be("qwen3:30b-instruct");
    }
}
