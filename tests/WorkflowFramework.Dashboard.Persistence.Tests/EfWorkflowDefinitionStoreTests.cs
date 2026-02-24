using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Persistence;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class EfWorkflowDefinitionStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;
    private readonly EfWorkflowDefinitionStore _store;

    public EfWorkflowDefinitionStoreTests()
    {
        _db = _factory.CreateSeeded();
        _store = new EfWorkflowDefinitionStore(_db, new AnonymousCurrentUserService());
    }

    [Fact]
    public async Task CreateAsync_ReturnsWorkflowWithId()
    {
        var request = new CreateWorkflowRequest
        {
            Description = "Test workflow",
            Tags = ["test"],
            Definition = new WorkflowDefinitionDto { Name = "Test" }
        };

        var result = await _store.CreateAsync(request);

        result.Id.Should().NotBeNullOrEmpty();
        result.Definition.Name.Should().Be("Test");
        result.Description.Should().Be("Test workflow");
        result.Tags.Should().Contain("test");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCreatedWorkflows()
    {
        await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "A" }
        });
        await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "B" }
        });

        var all = await _store.GetAllAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsWorkflow_WhenExists()
    {
        var created = await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Found" }
        });

        var result = await _store.GetByIdAsync(created.Id);
        result.Should().NotBeNull();
        result!.Definition.Name.Should().Be("Found");
    }

    [Fact]
    public async Task UpdateAsync_ModifiesWorkflow()
    {
        var created = await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Original" }
        });

        var updated = await _store.UpdateAsync(created.Id, new CreateWorkflowRequest
        {
            Description = "Updated desc",
            Definition = new WorkflowDefinitionDto { Name = "Updated" }
        });

        updated.Should().NotBeNull();
        updated!.Definition.Name.Should().Be("Updated");
        updated.Description.Should().Be("Updated desc");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.UpdateAsync("nonexistent", new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "X" }
        });
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var created = await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "ToDelete" }
        });

        var deleted = await _store.DeleteAsync(created.Id);
        deleted.Should().BeTrue();

        // Should not appear in GetAll (query filter)
        var all = await _store.GetAllAsync();
        all.Should().BeEmpty();

        // Should not appear in GetById (query filter)
        var found = await _store.GetByIdAsync(created.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var result = await _store.DeleteAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateAsync_CreatesNewWorkflowWithCopyName()
    {
        var created = await _store.CreateAsync(new CreateWorkflowRequest
        {
            Description = "Original desc",
            Tags = ["tag1"],
            Definition = new WorkflowDefinitionDto { Name = "Original" }
        });

        var duplicate = await _store.DuplicateAsync(created.Id);

        duplicate.Should().NotBeNull();
        duplicate!.Id.Should().NotBe(created.Id);
        duplicate.Definition.Name.Should().Be("Original (Copy)");
        duplicate.Description.Should().Be("Original desc");
    }

    [Fact]
    public async Task DuplicateAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.DuplicateAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SeedAsync_AddsWorkflowIfNotExists()
    {
        var workflow = new SavedWorkflowDefinition
        {
            Id = "seed-1",
            Definition = new WorkflowDefinitionDto { Name = "Seeded" },
            LastModified = DateTimeOffset.UtcNow
        };

        await _store.SeedAsync(workflow);
        var result = await _store.GetByIdAsync("seed-1");
        result.Should().NotBeNull();
        result!.Definition.Name.Should().Be("Seeded");
    }

    [Fact]
    public async Task SeedAsync_DoesNotOverwrite_WhenExists()
    {
        var workflow = new SavedWorkflowDefinition
        {
            Id = "seed-2",
            Definition = new WorkflowDefinitionDto { Name = "Original" },
            LastModified = DateTimeOffset.UtcNow
        };

        await _store.SeedAsync(workflow);

        var updated = new SavedWorkflowDefinition
        {
            Id = "seed-2",
            Definition = new WorkflowDefinitionDto { Name = "Changed" },
            LastModified = DateTimeOffset.UtcNow
        };
        await _store.SeedAsync(updated);

        var result = await _store.GetByIdAsync("seed-2");
        result!.Definition.Name.Should().Be("Original");
    }

    [Fact]
    public async Task GetAllAsync_OrdersByLastModifiedDesc()
    {
        await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "First" }
        });
        await Task.Delay(10); // Ensure different timestamps
        await _store.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Second" }
        });

        var all = await _store.GetAllAsync();
        all[0].Definition.Name.Should().Be("Second");
        all[1].Definition.Name.Should().Be("First");
    }

    [Fact]
    public async Task SampleWorkflowSeeder_SeedsAllSamples()
    {
        await Api.Services.SampleWorkflowSeeder.SeedAsync(_store);
        var all = await _store.GetAllAsync();
        all.Count.Should().BeGreaterThanOrEqualTo(10);
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }
}

