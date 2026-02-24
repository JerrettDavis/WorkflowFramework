using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Persistence;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class EfSettingsStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;
    private readonly EfSettingsStore _store;

    public EfSettingsStoreTests()
    {
        _db = _factory.CreateSeeded();
        _store = new EfSettingsStore(_db);
    }

    [Fact]
    public void Get_ReturnsDefaults_WhenNoSettings()
    {
        var settings = _store.Get();
        settings.OllamaUrl.Should().Be("http://localhost:11434");
        settings.MaxConcurrentRuns.Should().Be(5);
    }

    [Fact]
    public void Update_PersistsSettings()
    {
        _store.Update(new DashboardSettings
        {
            OllamaUrl = "http://custom:11434",
            DefaultModel = "gpt-4",
            MaxConcurrentRuns = 10
        });

        var settings = _store.Get();
        settings.OllamaUrl.Should().Be("http://custom:11434");
        settings.DefaultModel.Should().Be("gpt-4");
        settings.MaxConcurrentRuns.Should().Be(10);
    }

    [Fact]
    public void Update_OverwritesExistingSettings()
    {
        _store.Update(new DashboardSettings { DefaultModel = "gpt-3.5" });
        _store.Update(new DashboardSettings { DefaultModel = "gpt-4" });

        var settings = _store.Get();
        settings.DefaultModel.Should().Be("gpt-4");
    }

    [Fact]
    public void Get_SupportsPerUserSettings()
    {
        // Create users first to satisfy FK constraints
        _db.Users.Add(new WorkflowFramework.Dashboard.Persistence.Entities.DashboardUser
        {
            Id = "user1", Username = "user1", DisplayName = "User 1"
        });
        _db.Users.Add(new WorkflowFramework.Dashboard.Persistence.Entities.DashboardUser
        {
            Id = "user2", Username = "user2", DisplayName = "User 2"
        });
        _db.SaveChanges();

        _store.Update(new DashboardSettings { DefaultModel = "model-a" }, "user1");
        _store.Update(new DashboardSettings { DefaultModel = "model-b" }, "user2");

        _store.Get("user1").DefaultModel.Should().Be("model-a");
        _store.Get("user2").DefaultModel.Should().Be("model-b");
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }
}

