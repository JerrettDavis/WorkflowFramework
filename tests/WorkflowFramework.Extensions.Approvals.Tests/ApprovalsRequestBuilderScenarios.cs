using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Approvals;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// TinyBDD scenarios for <see cref="ApprovalRequestBuilder"/>.
/// These complement the existing plain-xUnit tests in the project.
/// </summary>
[Feature("ApprovalRequestBuilder — fluent builder for approval requests")]
public class ApprovalsRequestBuilderScenarios : TinyBddXunitBase
{
    public ApprovalsRequestBuilderScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("Build with only title produces request with default values"), Fact]
    public async Task Build_WithTitle_ProducesDefaults()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("Deploy to prod")
            .Build();

        await Given("builder with only title set", () => request)
            .Then("request has the given title and required=1 and non-empty id", r =>
            {
                r.Title.Should().Be("Deploy to prod");
                r.RequiredApprovers.Should().Be(1);
                r.CorrelationId.Should().NotBeNullOrEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithTitle null throws ArgumentNullException"), Fact]
    public async Task WithTitle_Null_ThrowsArgumentNullException()
    {
        Exception? caught = null;
        try { new ApprovalRequestBuilder().WithTitle(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null title passed to WithTitle", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithTitle whitespace throws ArgumentException"), Fact]
    public async Task WithTitle_Whitespace_ThrowsArgumentException()
    {
        Exception? caught = null;
        try { new ApprovalRequestBuilder().WithTitle("   "); }
        catch (Exception ex) { caught = ex; }

        await Given("whitespace title", () => caught)
            .Then("ArgumentException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RequiringApprovers sets required approval count"), Fact]
    public async Task RequiringApprovers_SetsCount()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .RequiringApprovers(3)
            .Build();

        await Given("RequiringApprovers(3)", () => request.RequiredApprovers)
            .Then("RequiredApprovers is 3", count =>
            {
                count.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithTimeout sets the timeout on the request"), Fact]
    public async Task WithTimeout_SetsTimeout()
    {
        var timeout = TimeSpan.FromHours(2);
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .WithTimeout(timeout)
            .Build();

        await Given("WithTimeout(2h)", () => request.Timeout)
            .Then("Timeout is 2 hours", t =>
            {
                t.Should().Be(timeout);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AllowedFor sets allowed roles on the request"), Fact]
    public async Task AllowedFor_SetsRoles()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .AllowedFor("admin", "manager")
            .Build();

        await Given("AllowedFor('admin', 'manager')", () => request.AllowedRoles)
            .Then("AllowedRoles contains both roles", roles =>
            {
                roles.Should().Contain("admin").And.Contain("manager");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithContext stores key-value pair in request context"), Fact]
    public async Task WithContext_StoresKeyValue()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .WithContext("env", "production")
            .Build();

        await Given("WithContext('env', 'production')", () => request.Context)
            .Then("context contains env=production", ctx =>
            {
                ctx.Should().ContainKey("env");
                ctx["env"].Should().Be("production");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Builder can be reused after Build"), Fact]
    public async Task Builder_CanBeReusedAfterBuild()
    {
        var builder = new ApprovalRequestBuilder().WithTitle("First");
        var r1 = builder.Build();
        builder.WithTitle("Second");
        var r2 = builder.Build();

        await Given("builder used twice with different titles", () => (r1.Title, r2.Title))
            .Then("each build has its own title", t =>
            {
                t.Item1.Should().Be("First");
                t.Item2.Should().Be("Second");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithCorrelationId sets the correlation ID on the request"), Fact]
    public async Task WithCorrelationId_SetsId()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .WithCorrelationId("my-correlation-123")
            .Build();

        await Given("WithCorrelationId('my-correlation-123')", () => request.CorrelationId)
            .Then("CorrelationId is 'my-correlation-123'", id =>
            {
                id.Should().Be("my-correlation-123");
                return true;
            })
            .AssertPassed();
    }
}
