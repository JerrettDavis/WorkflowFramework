using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Persistence;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence.Entities;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class UserScopingTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;

    public UserScopingTests()
    {
        _db = _factory.CreateSeeded();
        // Create two test users
        _db.Users.Add(new DashboardUser { Id = "user-a", Username = "alice", DisplayName = "Alice" });
        _db.Users.Add(new DashboardUser { Id = "user-b", Username = "bob", DisplayName = "Bob" });
        _db.SaveChanges();
    }

    private EfWorkflowDefinitionStore CreateStore(ICurrentUserService currentUser) =>
        new(_db, currentUser);

    [Fact]
    public async Task AuthenticatedUser_OnlySeesOwnWorkflows()
    {
        var aliceStore = CreateStore(new FakeCurrentUser("user-a", "alice", true));
        var bobStore = CreateStore(new FakeCurrentUser("user-b", "bob", true));

        await aliceStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Alice's Workflow" }
        });
        await bobStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Bob's Workflow" }
        });

        var aliceWorkflows = await aliceStore.GetAllAsync();
        aliceWorkflows.Should().HaveCount(1);
        aliceWorkflows[0].Definition.Name.Should().Be("Alice's Workflow");

        var bobWorkflows = await bobStore.GetAllAsync();
        bobWorkflows.Should().HaveCount(1);
        bobWorkflows[0].Definition.Name.Should().Be("Bob's Workflow");
    }

    [Fact]
    public async Task AuthenticatedUser_CannotAccessOtherUsersWorkflow()
    {
        var aliceStore = CreateStore(new FakeCurrentUser("user-a", "alice", true));
        var bobStore = CreateStore(new FakeCurrentUser("user-b", "bob", true));

        var created = await aliceStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Alice's Workflow" }
        });

        var fromBob = await bobStore.GetByIdAsync(created.Id);
        fromBob.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticatedUser_CannotUpdateOtherUsersWorkflow()
    {
        var aliceStore = CreateStore(new FakeCurrentUser("user-a", "alice", true));
        var bobStore = CreateStore(new FakeCurrentUser("user-b", "bob", true));

        var created = await aliceStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Alice's Workflow" }
        });

        var updated = await bobStore.UpdateAsync(created.Id, new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Hacked" }
        });
        updated.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticatedUser_CannotDeleteOtherUsersWorkflow()
    {
        var aliceStore = CreateStore(new FakeCurrentUser("user-a", "alice", true));
        var bobStore = CreateStore(new FakeCurrentUser("user-b", "bob", true));

        var created = await aliceStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Alice's Workflow" }
        });

        var deleted = await bobStore.DeleteAsync(created.Id);
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task AnonymousUser_SeesAllWorkflows()
    {
        // First create workflows with specific owners
        var aliceStore = CreateStore(new FakeCurrentUser("user-a", "alice", true));
        var bobStore = CreateStore(new FakeCurrentUser("user-b", "bob", true));

        await aliceStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Alice's" }
        });
        await bobStore.CreateAsync(new CreateWorkflowRequest
        {
            Definition = new WorkflowDefinitionDto { Name = "Bob's" }
        });

        // Anonymous user sees all
        var anonStore = CreateStore(new AnonymousCurrentUserService());
        var all = await anonStore.GetAllAsync();
        all.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }

    private sealed class FakeCurrentUser(string userId, string username, bool isAuthenticated) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => username;
        public bool IsAuthenticated => isAuthenticated;
    }
}
