using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Registry;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Hosting.Tests.Hosting;

[Feature("WorkflowHealthCheck — health check for the workflow engine")]
public class WorkflowHealthCheckScenarios : TinyBddXunitBase
{
    public WorkflowHealthCheckScenarios(ITestOutputHelper output) : base(output) { }

    private static HealthCheckContext MakeContext() =>
        new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("workflow-engine", _ => null!, null, null)
        };

    [Scenario("Healthy when registry has registered workflows"), Fact]
    public async Task HealthCheck_WithRegisteredWorkflows_ReturnsHealthy()
    {
        var registry = Substitute.For<IWorkflowRegistry>();
        registry.Names.Returns(new List<string> { "wf1", "wf2" });
        var check = new WorkflowHealthCheck(registry);

        var result = await check.CheckHealthAsync(MakeContext());

        await Given("a registry with 2 workflows", () => result)
            .Then("status is Healthy and description contains count", r =>
            {
                r.Status.Should().Be(HealthStatus.Healthy);
                r.Description.Should().Contain("2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Healthy when registry is empty"), Fact]
    public async Task HealthCheck_EmptyRegistry_ReturnsHealthy()
    {
        var registry = Substitute.For<IWorkflowRegistry>();
        registry.Names.Returns(new List<string>());
        var check = new WorkflowHealthCheck(registry);

        var result = await check.CheckHealthAsync(MakeContext());

        await Given("a registry with zero workflows", () => result)
            .Then("status is Healthy with 0 count", r =>
            {
                r.Status.Should().Be(HealthStatus.Healthy);
                r.Description.Should().Contain("0");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Degraded when registry is null"), Fact]
    public async Task HealthCheck_NullRegistry_ReturnsDegraded()
    {
        var check = new WorkflowHealthCheck(null);

        var result = await check.CheckHealthAsync(MakeContext());

        await Given("a null registry passed to WorkflowHealthCheck", () => result)
            .Then("status is Degraded", r =>
            {
                r.Status.Should().Be(HealthStatus.Degraded);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Health check description contains workflow count"), Fact]
    public async Task HealthCheck_DescriptionContainsCount()
    {
        var registry = Substitute.For<IWorkflowRegistry>();
        registry.Names.Returns(new List<string> { "a", "b", "c" });
        var check = new WorkflowHealthCheck(registry);

        var result = await check.CheckHealthAsync(MakeContext());

        await Given("a registry with 3 workflows", () => result)
            .Then("description mentions 3 workflow(s)", r =>
            {
                r.Description.Should().Contain("3");
                return true;
            })
            .AssertPassed();
    }
}
