using FluentAssertions;
using NSubstitute;
using Xunit;
using WorkflowFramework.Dashboard.Services;
using WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Dashboard.Tests;

public class WorkflowDashboardServiceTests
{
    private readonly IWorkflowRegistry _registry = Substitute.For<IWorkflowRegistry>();
    private readonly IExecutionHistoryStore _historyStore = Substitute.For<IExecutionHistoryStore>();
    private readonly WorkflowDashboardService _sut;

    public WorkflowDashboardServiceTests()
    {
        _sut = new WorkflowDashboardService(_registry, _historyStore);
    }

    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        var act = () => new WorkflowDashboardService(null!, _historyStore);
        act.Should().Throw<ArgumentNullException>().WithParameterName("registry");
    }

    [Fact]
    public void Constructor_NullHistoryStore_Throws()
    {
        var act = () => new WorkflowDashboardService(_registry, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("historyStore");
    }

    [Fact]
    public async Task GetWorkflowsAsync_ReturnsAllRegistered()
    {
        var workflow = CreateMockWorkflow("TestWorkflow", 3);
        _registry.Names.Returns(new[] { "TestWorkflow" });
        _registry.Resolve("TestWorkflow").Returns(workflow);
        _historyStore.GetRunsAsync(Arg.Any<ExecutionHistoryFilter?>(), Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowRunRecord>());

        var result = await _sut.GetWorkflowsAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TestWorkflow");
        result[0].StepCount.Should().Be(3);
        result[0].LastRunStatus.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkflowsAsync_IncludesLastRunStatus()
    {
        var workflow = CreateMockWorkflow("Wf1", 2);
        _registry.Names.Returns(new[] { "Wf1" });
        _registry.Resolve("Wf1").Returns(workflow);

        var lastRun = new WorkflowRunRecord
        {
            RunId = "r1",
            WorkflowName = "Wf1",
            Status = WorkflowStatus.Completed,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        _historyStore.GetRunsAsync(Arg.Any<ExecutionHistoryFilter?>(), Arg.Any<CancellationToken>())
            .Returns(new List<WorkflowRunRecord> { lastRun });

        var result = await _sut.GetWorkflowsAsync();

        result[0].LastRunStatus.Should().Be(WorkflowStatus.Completed);
        result[0].LastRunAt.Should().Be(lastRun.StartedAt);
    }

    [Fact]
    public void GetWorkflowDetail_ReturnsStepNames()
    {
        var workflow = CreateMockWorkflow("DetailWf", 2);
        _registry.Resolve("DetailWf").Returns(workflow);

        var detail = _sut.GetWorkflowDetail("DetailWf");

        detail.Name.Should().Be("DetailWf");
        detail.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void GetWorkflowDetail_NullName_Throws()
    {
        var act = () => _sut.GetWorkflowDetail(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("workflowName");
    }

    [Fact]
    public async Task GetRunsAsync_DelegatesToStore()
    {
        var runs = new List<WorkflowRunRecord>
        {
            new() { RunId = "r1", WorkflowName = "Wf1", Status = WorkflowStatus.Completed }
        };
        _historyStore.GetRunsAsync(Arg.Any<ExecutionHistoryFilter?>(), Arg.Any<CancellationToken>())
            .Returns(runs);

        var result = await _sut.GetRunsAsync("Wf1", 10);

        result.Should().HaveCount(1);
        result[0].RunId.Should().Be("r1");
        await _historyStore.Received(1).GetRunsAsync(
            Arg.Is<ExecutionHistoryFilter?>(f => f!.WorkflowName == "Wf1" && f.MaxResults == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRunAsync_ReturnsRun()
    {
        var run = new WorkflowRunRecord { RunId = "r1", WorkflowName = "Wf1" };
        _historyStore.GetRunAsync("r1", Arg.Any<CancellationToken>()).Returns(run);

        var result = await _sut.GetRunAsync("r1");

        result.Should().NotBeNull();
        result!.RunId.Should().Be("r1");
    }

    [Fact]
    public async Task GetRunAsync_NullId_Throws()
    {
        var act = async () => await _sut.GetRunAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("runId");
    }

    [Fact]
    public async Task TriggerRunAsync_ExecutesWorkflow()
    {
        var context = new WorkflowContext();
        var expectedResult = new WorkflowResult(WorkflowStatus.Completed, context);
        var workflow = Substitute.For<IWorkflow>();
        workflow.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(expectedResult);
        _registry.Resolve("Wf1").Returns(workflow);

        var result = await _sut.TriggerRunAsync("Wf1");

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerRunAsync_NullName_Throws()
    {
        var act = async () => await _sut.TriggerRunAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("workflowName");
    }

    private static IWorkflow CreateMockWorkflow(string name, int stepCount)
    {
        var workflow = Substitute.For<IWorkflow>();
        workflow.Name.Returns(name);
        var steps = Enumerable.Range(0, stepCount)
            .Select(i =>
            {
                var step = Substitute.For<IStep>();
                step.Name.Returns($"Step{i + 1}");
                return step;
            })
            .ToList();
        workflow.Steps.Returns(steps);
        return workflow;
    }
}
