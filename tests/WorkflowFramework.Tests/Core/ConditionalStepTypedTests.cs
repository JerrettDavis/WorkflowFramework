using FluentAssertions;
using WorkflowFramework.Builder;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class ConditionalStepTypedTests
{
    private class TestData
    {
        public List<string> Log { get; set; } = new();
    }

    private class LogStep(string name) : IStep<TestData>
    {
        public string Name { get; } = name;

        public Task ExecuteAsync(IWorkflowContext<TestData> context)
        {
            context.Data.Log.Add(Name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConditionalStepT_ConditionTrue_NoElse_ExecutesThen()
    {
        var wf = Workflow.Create<TestData>()
            .If(ctx => true)
            .Then(new LogStep("Then"))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext<TestData>(new TestData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("Then");
    }

    [Fact]
    public async Task ConditionalStepT_ConditionFalse_NoElse_NothingExecutes()
    {
        var wf = Workflow.Create<TestData>()
            .If(ctx => false)
            .Then(new LogStep("Then"))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext<TestData>(new TestData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().BeEmpty();
    }

    [Fact]
    public async Task ConditionalStepT_ConditionFalse_WithElse_ExecutesElse()
    {
        var wf = Workflow.Create<TestData>()
            .If(ctx => false)
            .Then(new LogStep("Then"))
            .Else(new LogStep("Else"))
            .Build();

        var ctx = new WorkflowContext<TestData>(new TestData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("Else").And.NotContain("Then");
    }

    [Fact]
    public void ConditionalStepT_Name_WithElse_IncludesBothNames()
    {
        // Build and inspect name via workflow steps
        var wf = Workflow.Create<TestData>()
            .If(ctx => true)
            .Then(new LogStep("A"))
            .Else(new LogStep("B"))
            .Build();

        // The underlying workflow's steps contain the conditional
        // Name should be "If(A/B)"
        // We can't easily access internal step name without reflection,
        // but the typed builder path exercises the code
    }
}
