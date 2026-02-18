using BenchmarkDotNet.Attributes;

namespace WorkflowFramework.Benchmarks;

[MemoryDiagnoser]
public class MiddlewarePipelineBenchmarks
{
    private IWorkflow _noMiddleware = null!;
    private IWorkflow _oneMiddleware = null!;
    private IWorkflow _fiveMiddleware = null!;

    [GlobalSetup]
    public void Setup()
    {
        _noMiddleware = Workflow.Create("NoMw")
            .Step("Step1", _ => Task.CompletedTask)
            .Build();

        var b1 = Workflow.Create("OneMw").Use(new NoOpMiddleware()).Step("Step1", _ => Task.CompletedTask);
        _oneMiddleware = b1.Build();

        var b5 = Workflow.Create("FiveMw");
        for (var i = 0; i < 5; i++) b5.Use(new NoOpMiddleware());
        b5.Step("Step1", _ => Task.CompletedTask);
        _fiveMiddleware = b5.Build();
    }

    [Benchmark(Baseline = true)] public Task NoMiddleware() => _noMiddleware.ExecuteAsync(new WorkflowContext());
    [Benchmark] public Task OneMiddleware() => _oneMiddleware.ExecuteAsync(new WorkflowContext());
    [Benchmark] public Task FiveMiddleware() => _fiveMiddleware.ExecuteAsync(new WorkflowContext());

    private sealed class NoOpMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }
}
