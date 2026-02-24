using Microsoft.AspNetCore.SignalR;
using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Hubs;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Plugins;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class WorkflowRunServiceTests
{
    private readonly InMemoryWorkflowDefinitionStore _store = new();
    private readonly WorkflowRunService _runService;

    public WorkflowRunServiceTests()
    {
        var settingsService = new DashboardSettingsService();
        var compiler = new WorkflowDefinitionCompiler(settingsService, new PluginRegistry());
        var notifier = new WorkflowExecutionNotifier(new FakeHubContext());
        _runService = new WorkflowRunService(_store, compiler, notifier);
    }

    private async Task<SavedWorkflowDefinition> CreateWorkflow()
    {
        return await _store.CreateAsync(new CreateWorkflowRequest
        {
            Description = "Test",
            Definition = new WorkflowDefinitionDto
            {
                Name = "TestWorkflow",
                Steps = [new StepDefinitionDto { Name = "S1", Type = "Action" }]
            }
        });
    }

    [Fact]
    public async Task StartRunAsync_ReturnsNullForMissingWorkflow()
    {
        var result = await _runService.StartRunAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task StartRunAsync_CreatesRun()
    {
        var wf = await CreateWorkflow();
        var run = await _runService.StartRunAsync(wf.Id);

        run.Should().NotBeNull();
        run!.RunId.Should().NotBeNullOrEmpty();
        run.WorkflowId.Should().Be(wf.Id);
        run.WorkflowName.Should().Be("TestWorkflow");
        run.Status.Should().BeOneOf("Running", "Completed");
    }

    [Fact]
    public async Task StartRunAsync_CompletesAsynchronously()
    {
        var wf = await CreateWorkflow();
        var run = await _runService.StartRunAsync(wf.Id);
        await Task.Delay(2000);

        var fetched = await _runService.GetRunAsync(run!.RunId);
        fetched.Should().NotBeNull();
        fetched!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetRunsAsync_ReturnsAllRuns()
    {
        var wf = await CreateWorkflow();
        await _runService.StartRunAsync(wf.Id);
        await _runService.StartRunAsync(wf.Id);

        var runs = await _runService.GetRunsAsync();
        runs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNullForMissing()
    {
        var result = await _runService.GetRunAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRunAsync_ReturnsExistingRun()
    {
        var wf = await CreateWorkflow();
        var created = await _runService.StartRunAsync(wf.Id);
        var fetched = await _runService.GetRunAsync(created!.RunId);

        fetched.Should().NotBeNull();
        fetched!.RunId.Should().Be(created.RunId);
    }

    [Fact]
    public async Task CancelRunAsync_ReturnsFalseForMissing()
    {
        var result = await _runService.CancelRunAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelRunAsync_ReturnsTrueForExisting()
    {
        var wf = await CreateWorkflow();
        var run = await _runService.StartRunAsync(wf.Id);
        var result = await _runService.CancelRunAsync(run!.RunId);
        result.Should().BeTrue();
    }

    // Minimal fakes for IHubContext
    private sealed class FakeHubContext : IHubContext<WorkflowExecutionHub, IWorkflowExecutionClient>
    {
        public IHubClients<IWorkflowExecutionClient> Clients { get; } = new FakeHubClients();
        public IGroupManager Groups => throw new NotImplementedException();
    }

    private sealed class FakeHubClients : IHubClients<IWorkflowExecutionClient>
    {
        private readonly IWorkflowExecutionClient _client = new FakeClient();
        public IWorkflowExecutionClient All => _client;
        public IWorkflowExecutionClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => _client;
        public IWorkflowExecutionClient Client(string connectionId) => _client;
        public IWorkflowExecutionClient Clients(IReadOnlyList<string> connectionIds) => _client;
        public IWorkflowExecutionClient Group(string groupName) => _client;
        public IWorkflowExecutionClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _client;
        public IWorkflowExecutionClient Groups(IReadOnlyList<string> groupNames) => _client;
        public IWorkflowExecutionClient User(string userId) => _client;
        public IWorkflowExecutionClient Users(IReadOnlyList<string> userIds) => _client;
    }

    private sealed class FakeClient : IWorkflowExecutionClient
    {
        public Task RunStarted(string runId, string workflowName) => Task.CompletedTask;
        public Task StepStarted(string runId, string stepName, int stepIndex) => Task.CompletedTask;
        public Task StepCompleted(string runId, string stepName, string status, long durationMs, string? output) => Task.CompletedTask;
        public Task StepFailed(string runId, string stepName, string error) => Task.CompletedTask;
        public Task RunCompleted(string runId, string status, long totalDurationMs) => Task.CompletedTask;
        public Task RunFailed(string runId, string error) => Task.CompletedTask;
        public Task LogMessage(string runId, string level, string message, DateTimeOffset timestamp) => Task.CompletedTask;
    }
}
