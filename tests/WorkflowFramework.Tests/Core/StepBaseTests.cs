using FluentAssertions;
using WorkflowFramework.Attributes;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class StepBaseTests
{
    private class SimpleStep : StepBase
    {
        public override Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    [StepName("CustomName")]
    private class NamedStep : StepBase
    {
        public override Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    [Fact]
    public void Name_DefaultsToClassName()
    {
        new SimpleStep().Name.Should().Be("SimpleStep");
    }

    [Fact]
    public void Name_UsesStepNameAttribute()
    {
        new NamedStep().Name.Should().Be("CustomName");
    }

    [Fact]
    public async Task ExecuteAsync_CanBeOverridden()
    {
        var step = new SimpleStep();
        await step.ExecuteAsync(new WorkflowContext()); // should not throw
    }
}

public class StepBaseGenericTests
{
    private class MyData { public string Val { get; set; } = ""; }

    private class SimpleTypedStep : StepBase<MyData>
    {
        public override Task ExecuteAsync(IWorkflowContext<MyData> context)
        {
            context.Data.Val = "done";
            return Task.CompletedTask;
        }
    }

    [StepName("TypedCustom")]
    private class NamedTypedStep : StepBase<MyData>
    {
        public override Task ExecuteAsync(IWorkflowContext<MyData> context) => Task.CompletedTask;
    }

    [Fact]
    public void Name_DefaultsToClassName()
    {
        new SimpleTypedStep().Name.Should().Be("SimpleTypedStep");
    }

    [Fact]
    public void Name_UsesStepNameAttribute()
    {
        new NamedTypedStep().Name.Should().Be("TypedCustom");
    }

    [Fact]
    public async Task ExecuteAsync_ModifiesData()
    {
        var step = new SimpleTypedStep();
        var ctx = new WorkflowContext<MyData>(new MyData());
        await step.ExecuteAsync(ctx);
        ctx.Data.Val.Should().Be("done");
    }
}
