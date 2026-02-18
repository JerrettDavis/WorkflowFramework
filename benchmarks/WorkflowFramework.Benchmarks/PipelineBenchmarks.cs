using BenchmarkDotNet.Attributes;
using WorkflowFramework.Pipeline;

namespace WorkflowFramework.Benchmarks;

[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private Func<int, CancellationToken, Task<int>> _pipeline = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pipeline = WorkflowFramework.Pipeline.Pipeline.Create<int>()
            .Pipe<int>((x, ct) => Task.FromResult(x + 1))
            .Pipe<int>((x, ct) => Task.FromResult(x * 2))
            .Pipe<int>((x, ct) => Task.FromResult(x - 1))
            .Build();
    }

    [Benchmark]
    public Task<int> ThreeStepPipeline() => _pipeline(10, CancellationToken.None);
}
