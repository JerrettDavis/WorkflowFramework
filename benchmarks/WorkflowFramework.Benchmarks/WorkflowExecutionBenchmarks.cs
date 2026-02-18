using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace WorkflowFramework.Benchmarks;

/// <summary>
/// Benchmarks for core workflow execution paths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class WorkflowExecutionBenchmarks
{
    private IWorkflow _simpleWorkflow = null!;
    private IWorkflow _middlewareWorkflow = null!;
    private IWorkflow _parallelWorkflow = null!;

    private sealed class NoOpStep : IStep
    {
        public string Name => "NoOp";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class NoOpMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    [GlobalSetup]
    public void Setup()
    {
        _simpleWorkflow = Workflow.Create()
            .Step(new NoOpStep())
            .Step(new NoOpStep())
            .Step(new NoOpStep())
            .Build();

        _middlewareWorkflow = Workflow.Create()
            .Use(new NoOpMiddleware())
            .Use(new NoOpMiddleware())
            .Step(new NoOpStep())
            .Step(new NoOpStep())
            .Step(new NoOpStep())
            .Build();

        _parallelWorkflow = Workflow.Create()
            .Parallel(p => p
                .Step(new NoOpStep())
                .Step(new NoOpStep())
                .Step(new NoOpStep()))
            .Build();
    }

    [Benchmark(Baseline = true)]
    public Task SimpleWorkflow() =>
        _simpleWorkflow.ExecuteAsync(new WorkflowContext());

    [Benchmark]
    public Task WorkflowWithMiddleware() =>
        _middlewareWorkflow.ExecuteAsync(new WorkflowContext());

    [Benchmark]
    public Task ParallelWorkflow() =>
        _parallelWorkflow.ExecuteAsync(new WorkflowContext());
}
