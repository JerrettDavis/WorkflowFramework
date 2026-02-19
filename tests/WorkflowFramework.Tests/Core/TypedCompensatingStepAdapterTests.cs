using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class TypedCompensatingStepAdapterTests
{
    private class TestData
    {
        public string Status { get; set; } = "";
        public List<string> Log { get; set; } = new();
    }

    private class TestCompensatingStep : ICompensatingStep<TestData>
    {
        public string Name => "TestComp";
        public Task ExecuteAsync(IWorkflowContext<TestData> context)
        {
            context.Data.Log.Add("executed");
            return Task.CompletedTask;
        }
        public Task CompensateAsync(IWorkflowContext<TestData> context)
        {
            context.Data.Log.Add("compensated");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        var type = typeof(WorkflowContext).Assembly
            .GetType("WorkflowFramework.Internal.TypedCompensatingStepAdapter`1")!
            .MakeGenericType(typeof(TestData));
        var act = () => Activator.CreateInstance(type, new object?[] { null });
        act.Should().Throw<Exception>(); // TargetInvocationException wrapping ArgumentNullException
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToInner()
    {
        var adapter = CreateAdapter(new TestCompensatingStep());
        var data = new TestData();
        var ctx = new WorkflowContext<TestData>(data);
        await adapter.ExecuteAsync(ctx);
        data.Log.Should().Contain("executed");
    }

    [Fact]
    public async Task CompensateAsync_DelegatesToInner()
    {
        var adapter = CreateAdapter(new TestCompensatingStep());
        var data = new TestData();
        var ctx = new WorkflowContext<TestData>(data);
        await ((ICompensatingStep)adapter).CompensateAsync(ctx);
        data.Log.Should().Contain("compensated");
    }

    [Fact]
    public void Name_DelegatesToInner()
    {
        var adapter = CreateAdapter(new TestCompensatingStep());
        adapter.Name.Should().Be("TestComp");
    }

    private static ICompensatingStep CreateAdapter(ICompensatingStep<TestData> inner)
    {
        var type = typeof(WorkflowContext).Assembly
            .GetType("WorkflowFramework.Internal.TypedCompensatingStepAdapter`1")!
            .MakeGenericType(typeof(TestData));
        return (ICompensatingStep)Activator.CreateInstance(type, inner)!;
    }
}
