using FluentAssertions;
using WorkflowFramework.Registry;
using WorkflowFramework.Versioning;
using Xunit;

namespace WorkflowFramework.Tests;

public class RegistryTests
{
    [Fact]
    public void WorkflowRegistry_RegisterAndResolve()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test").Build());

        var workflow = registry.Resolve("test");
        workflow.Name.Should().Be("test");
    }

    [Fact]
    public void WorkflowRegistry_ResolveUnknown_Throws()
    {
        var registry = new WorkflowRegistry();

        var act = () => registry.Resolve("unknown");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void WorkflowRegistry_Names_ReturnsRegistered()
    {
        var registry = new WorkflowRegistry();
        registry.Register("a", () => Workflow.Create("a").Build());
        registry.Register("b", () => Workflow.Create("b").Build());

        registry.Names.Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public async Task WorkflowRunner_RunsByName()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test")
            .Step("Mark", ctx => { ctx.Properties["Ran"] = true; return Task.CompletedTask; })
            .Build());

        var runner = new WorkflowRunner(registry);
        IWorkflowContext context = new WorkflowContext();
        var result = await runner.RunAsync("test", context);

        result.IsSuccess.Should().BeTrue();
        ((bool)context.Properties["Ran"]!).Should().BeTrue();
    }

    [Fact]
    public void VersionedRegistry_ResolvesLatest()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("wf", 1, () => Workflow.Create("wf-v1").Build());
        registry.Register("wf", 2, () => Workflow.Create("wf-v2").Build());

        var workflow = registry.Resolve("wf");
        workflow.Name.Should().Be("wf-v2");
    }

    [Fact]
    public void VersionedRegistry_ResolvesSpecificVersion()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("wf", 1, () => Workflow.Create("wf-v1").Build());
        registry.Register("wf", 2, () => Workflow.Create("wf-v2").Build());

        var workflow = registry.Resolve("wf", 1);
        workflow.Name.Should().Be("wf-v1");
    }

    [Fact]
    public void VersionedRegistry_GetVersions()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("wf", 1, () => Workflow.Create("wf-v1").Build());
        registry.Register("wf", 3, () => Workflow.Create("wf-v3").Build());

        registry.GetVersions("wf").Should().BeEquivalentTo(new[] { 1, 3 });
    }
}
