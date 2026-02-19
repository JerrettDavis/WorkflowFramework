using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using WorkflowFramework.Dashboard.Api.Hubs;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api.Tests;

public sealed class WorkflowExecutionNotifierTests
{
    [Fact]
    public async Task OnWorkflowStarted_SendsRunStarted()
    {
        var (notifier, hub) = CreateNotifier();
        var context = new FakeWorkflowContext("run-1", "TestWorkflow");

        await notifier.OnWorkflowStartedAsync(context);

        hub.LastGroup.Should().Be("run-run-1");
        hub.SentMessages.Should().ContainKey("RunStarted");
    }

    [Fact]
    public async Task OnWorkflowCompleted_SendsRunCompleted()
    {
        var (notifier, hub) = CreateNotifier();
        var context = new FakeWorkflowContext("run-2", "TestWorkflow");

        await notifier.OnWorkflowStartedAsync(context);
        await notifier.OnWorkflowCompletedAsync(context);

        hub.SentMessages.Should().ContainKey("RunCompleted");
    }

    [Fact]
    public async Task OnStepStarted_SendsStepStarted()
    {
        var (notifier, hub) = CreateNotifier();
        var context = new FakeWorkflowContext("run-3", "TestWorkflow") { CurrentStepIndex = 0 };
        var step = new FakeStep("MyStep");

        await notifier.OnStepStartedAsync(context, step);

        hub.SentMessages.Should().ContainKey("StepStarted");
    }

    [Fact]
    public async Task OnStepFailed_SendsStepFailed()
    {
        var (notifier, hub) = CreateNotifier();
        var context = new FakeWorkflowContext("run-4", "TestWorkflow");
        var step = new FakeStep("FailStep");

        await notifier.OnStepStartedAsync(context, step);
        await notifier.OnStepFailedAsync(context, step, new InvalidOperationException("boom"));

        hub.SentMessages.Should().ContainKey("StepFailed");
    }

    [Fact]
    public async Task OnWorkflowFailed_SendsRunFailed()
    {
        var (notifier, hub) = CreateNotifier();
        var context = new FakeWorkflowContext("run-5", "TestWorkflow");

        await notifier.OnWorkflowStartedAsync(context);
        await notifier.OnWorkflowFailedAsync(context, new Exception("workflow boom"));

        hub.SentMessages.Should().ContainKey("RunFailed");
    }

    private static (WorkflowExecutionNotifier, FakeHubContext) CreateNotifier()
    {
        var hub = new FakeHubContext();
        var notifier = new WorkflowExecutionNotifier(hub);
        return (notifier, hub);
    }

    private sealed class FakeWorkflowContext : IWorkflowContext
    {
        public FakeWorkflowContext(string correlationId, string workflowName)
        {
            CorrelationId = correlationId;
            WorkflowId = "wf-1";
            Properties = new Dictionary<string, object?> { ["WorkflowName"] = workflowName };
        }

        public string WorkflowId { get; }
        public string CorrelationId { get; }
        public CancellationToken CancellationToken => CancellationToken.None;
        public IDictionary<string, object?> Properties { get; }
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }

    private sealed class FakeStep : IStep
    {
        public FakeStep(string name) => Name = name;
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class FakeHubContext : IHubContext<WorkflowExecutionHub, IWorkflowExecutionClient>
    {
        public string? LastGroup { get; private set; }
        public Dictionary<string, object?> SentMessages { get; } = new();

        public IHubClients<IWorkflowExecutionClient> Clients => new FakeHubClients(this);
        public IGroupManager Groups => throw new NotImplementedException();

        private sealed class FakeHubClients(FakeHubContext ctx) : IHubClients<IWorkflowExecutionClient>
        {
            public IWorkflowExecutionClient All => new FakeClient(ctx, null);
            public IWorkflowExecutionClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
            public IWorkflowExecutionClient Client(string connectionId) => All;
            public IWorkflowExecutionClient Clients(IReadOnlyList<string> connectionIds) => All;
            public IWorkflowExecutionClient Group(string groupName)
            {
                ctx.LastGroup = groupName;
                return new FakeClient(ctx, groupName);
            }
            public IWorkflowExecutionClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
            public IWorkflowExecutionClient Groups(IReadOnlyList<string> groupNames) => All;
            public IWorkflowExecutionClient User(string userId) => All;
            public IWorkflowExecutionClient Users(IReadOnlyList<string> userIds) => All;
        }

        private sealed class FakeClient(FakeHubContext ctx, string? group) : IWorkflowExecutionClient
        {
            public Task RunStarted(string runId, string workflowName) { ctx.SentMessages["RunStarted"] = (runId, workflowName); return Task.CompletedTask; }
            public Task StepStarted(string runId, string stepName, int stepIndex) { ctx.SentMessages["StepStarted"] = (runId, stepName, stepIndex); return Task.CompletedTask; }
            public Task StepCompleted(string runId, string stepName, string status, long durationMs, string? output) { ctx.SentMessages["StepCompleted"] = (runId, stepName); return Task.CompletedTask; }
            public Task StepFailed(string runId, string stepName, string error) { ctx.SentMessages["StepFailed"] = (runId, stepName, error); return Task.CompletedTask; }
            public Task RunCompleted(string runId, string status, long totalDurationMs) { ctx.SentMessages["RunCompleted"] = (runId, status); return Task.CompletedTask; }
            public Task RunFailed(string runId, string error) { ctx.SentMessages["RunFailed"] = (runId, error); return Task.CompletedTask; }
            public Task LogMessage(string runId, string level, string message, DateTimeOffset timestamp) { ctx.SentMessages["LogMessage"] = (runId, level, message); return Task.CompletedTask; }
        }
    }
}
