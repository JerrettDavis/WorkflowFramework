using FluentAssertions;
using WorkflowFramework.Registry;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowRegistryTests
{
    [Fact]
    public void Register_NullName_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Register(null!, () => Workflow.Create().Build());
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Register_NullFactory_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Register("test", (Func<IWorkflow>)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("factory");
    }

    [Fact]
    public void Resolve_Registered_ReturnsWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test").Step(new TrackingStep()).Build());
        var wf = registry.Resolve("test");
        wf.Name.Should().Be("test");
    }

    [Fact]
    public void Resolve_NullName_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Resolve(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Resolve_NotRegistered_ThrowsKeyNotFound()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Resolve("missing");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Resolve_CaseInsensitive()
    {
        var registry = new WorkflowRegistry();
        registry.Register("Test", () => Workflow.Create("Test").Build());
        registry.Resolve("test").Should().NotBeNull();
        registry.Resolve("TEST").Should().NotBeNull();
    }

    [Fact]
    public void Names_ReturnsRegisteredNames()
    {
        var registry = new WorkflowRegistry();
        registry.Register("A", () => Workflow.Create().Build());
        registry.Register("B", () => Workflow.Create().Build());
        registry.Names.Should().HaveCount(2);
    }

    [Fact]
    public void Register_Duplicate_OverwritesFactory()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("v1").Build());
        registry.Register("test", () => Workflow.Create("v2").Build());
        registry.Resolve("test").Name.Should().Be("v2");
    }

    // Typed registry tests
    private class OrderData { public string Id { get; set; } = ""; }

    [Fact]
    public void RegisterTyped_NullName_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Register<OrderData>(null!, () => Workflow.Create<OrderData>().Build());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterTyped_NullFactory_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Register<OrderData>("test", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveTyped_Works()
    {
        var registry = new WorkflowRegistry();
        registry.Register<OrderData>("order", () => Workflow.Create<OrderData>("order").Build());
        var wf = registry.Resolve<OrderData>("order");
        wf.Name.Should().Be("order");
    }

    [Fact]
    public void ResolveTyped_NullName_Throws()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Resolve<OrderData>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveTyped_NotRegistered_ThrowsKeyNotFound()
    {
        var registry = new WorkflowRegistry();
        var act = () => registry.Resolve<OrderData>("missing");
        act.Should().Throw<KeyNotFoundException>();
    }
}

public class WorkflowRunnerTests
{
    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        var act = () => new WorkflowRunner(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("registry");
    }

    [Fact]
    public async Task RunAsync_Untyped_ExecutesWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test").Step(new TrackingStep("A")).Build());
        var runner = new WorkflowRunner(registry);
        IWorkflowContext ctx = new WorkflowContext();
        var result = await runner.RunAsync("test", ctx);
        result.IsSuccess.Should().BeTrue();
        TrackingStep.GetLog(ctx).Should().Contain("A");
    }

    [Fact]
    public async Task RunAsync_Typed_ExecutesWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register<TestData>("test", () =>
            Workflow.Create<TestData>("test")
                .Step("set", ctx => { ctx.Data.Value = 42; return Task.CompletedTask; })
                .Build());
        var runner = new WorkflowRunner(registry);
        var result = await runner.RunAsync("test", new TestData());
        result.IsSuccess.Should().BeTrue();
        result.Data.Value.Should().Be(42);
    }

    private class TestData { public int Value { get; set; } }
}
