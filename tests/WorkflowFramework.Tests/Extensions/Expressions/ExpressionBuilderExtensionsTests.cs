using FluentAssertions;
using WorkflowFramework.Extensions.Expressions;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Expressions;

public class ExpressionBuilderExtensionsTests
{
    [Fact]
    public async Task IfExpression_True_ExecutesThenBranch()
    {
        var executed = false;
        var workflow = Workflow.Create("test")
            .IfExpression("x == 1")
                .Then(new ActionStep("Then", () => executed = true))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext();
        ctx.Properties["x"] = 1;
        await workflow.ExecuteAsync(ctx);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task IfExpression_False_SkipsThenBranch()
    {
        var executed = false;
        var workflow = Workflow.Create("test")
            .IfExpression("x == 1")
                .Then(new ActionStep("Then", () => executed = true))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext();
        ctx.Properties["x"] = 2;
        await workflow.ExecuteAsync(ctx);

        executed.Should().BeFalse();
    }

    [Fact]
    public async Task IfExpression_WithCustomEvaluator_UsesIt()
    {
        var evaluator = new SimpleExpressionEvaluator();
        var executed = false;
        var workflow = Workflow.Create("test")
            .IfExpression("val > 10", evaluator)
                .Then(new ActionStep("Then", () => executed = true))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext();
        ctx.Properties["val"] = 20;
        await workflow.ExecuteAsync(ctx);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task IfExpression_DefaultEvaluator_Works()
    {
        var executed = false;
        var workflow = Workflow.Create("test")
            .IfExpression("a == 5")
                .Then(new ActionStep("Then", () => executed = true))
            .EndIf()
            .Build();

        var ctx = new WorkflowContext();
        ctx.Properties["a"] = 5;
        await workflow.ExecuteAsync(ctx);

        executed.Should().BeTrue();
    }
}

file class ActionStep : IStep
{
    private readonly Action _action;
    public string Name { get; }
    public ActionStep(string name, Action action) { Name = name; _action = action; }
    public Task ExecuteAsync(IWorkflowContext context) { _action(); return Task.CompletedTask; }
}
