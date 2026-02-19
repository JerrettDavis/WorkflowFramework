using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class InMemoryWorkflowDefinitionStoreTests
{
    private readonly InMemoryWorkflowDefinitionStore _store = new();

    private static CreateWorkflowRequest MakeRequest(string name = "Test Workflow") => new()
    {
        Description = "A test workflow",
        Definition = new WorkflowDefinitionDto
        {
            Name = name,
            Steps = [new StepDefinitionDto { Name = "Step1", Type = "Action" }]
        }
    };

    [Fact]
    public async Task CreateAsync_ReturnsNewWorkflowWithId()
    {
        var result = await _store.CreateAsync(MakeRequest());

        result.Id.Should().NotBeNullOrEmpty();
        result.Definition.Name.Should().Be("Test Workflow");
        result.Definition.Steps.Should().HaveCount(1);
        result.Description.Should().Be("A test workflow");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllWorkflows()
    {
        await _store.CreateAsync(MakeRequest("W1"));
        await _store.CreateAsync(MakeRequest("W2"));

        var all = await _store.GetAllAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        var result = await _store.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCreatedWorkflow()
    {
        var created = await _store.CreateAsync(MakeRequest());
        var fetched = await _store.GetByIdAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Definition.Name.Should().Be("Test Workflow");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForMissing()
    {
        var result = await _store.UpdateAsync("nonexistent", MakeRequest());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingWorkflow()
    {
        var created = await _store.CreateAsync(MakeRequest("Original"));
        var updated = await _store.UpdateAsync(created.Id, MakeRequest("Updated"));

        updated.Should().NotBeNull();
        updated!.Definition.Name.Should().Be("Updated");
        updated.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissing()
    {
        var result = await _store.DeleteAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesWorkflow()
    {
        var created = await _store.CreateAsync(MakeRequest());
        var deleted = await _store.DeleteAsync(created.Id);

        deleted.Should().BeTrue();
        (await _store.GetByIdAsync(created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DuplicateAsync_ReturnsNullForMissing()
    {
        var result = await _store.DuplicateAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateAsync_CreatesCloneWithNewId()
    {
        var created = await _store.CreateAsync(MakeRequest("Original"));
        var dup = await _store.DuplicateAsync(created.Id);

        dup.Should().NotBeNull();
        dup!.Id.Should().NotBe(created.Id);
        dup.Definition.Name.Should().Be("Original (Copy)");
        dup.Definition.Steps.Should().HaveCount(1);
    }
}
