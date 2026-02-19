using FluentAssertions;
using WorkflowFramework.Tests.Common;
using WorkflowFramework.Versioning;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class VersionedWorkflowRegistryTests
{
    [Fact]
    public void Register_And_Resolve_SpecificVersion()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("order", 1, () => Workflow.Create("order-v1").Step(new TrackingStep()).Build());
        registry.Register("order", 2, () => Workflow.Create("order-v2").Step(new TrackingStep()).Build());
        var wf = registry.Resolve("order", 1);
        wf.Name.Should().Be("order-v1");
    }

    [Fact]
    public void Resolve_NoVersion_ReturnsLatest()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("order", 1, () => Workflow.Create("v1").Build());
        registry.Register("order", 3, () => Workflow.Create("v3").Build());
        registry.Register("order", 2, () => Workflow.Create("v2").Build());
        registry.Resolve("order").Name.Should().Be("v3");
    }

    [Fact]
    public void Resolve_NotRegistered_ThrowsKeyNotFound()
    {
        var registry = new VersionedWorkflowRegistry();
        var act = () => registry.Resolve("missing");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Resolve_WrongVersion_ThrowsKeyNotFound()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("order", 1, () => Workflow.Create("v1").Build());
        var act = () => registry.Resolve("order", 99);
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetVersions_ReturnsRegistered()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("order", 1, () => Workflow.Create().Build());
        registry.Register("order", 3, () => Workflow.Create().Build());
        registry.GetVersions("order").Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void GetVersions_NotRegistered_ReturnsEmpty()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.GetVersions("missing").Should().BeEmpty();
    }

    [Fact]
    public void Register_OverwritesSameVersion()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("order", 1, () => Workflow.Create("old").Build());
        registry.Register("order", 1, () => Workflow.Create("new").Build());
        registry.Resolve("order", 1).Name.Should().Be("new");
    }

    [Fact]
    public void Resolve_CaseInsensitive()
    {
        var registry = new VersionedWorkflowRegistry();
        registry.Register("Order", 1, () => Workflow.Create("v1").Build());
        registry.Resolve("order", 1).Should().NotBeNull();
    }
}
