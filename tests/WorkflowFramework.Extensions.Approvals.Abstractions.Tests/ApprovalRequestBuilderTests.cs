using FluentAssertions;
using Xunit;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// Unit tests for <see cref="ApprovalRequestBuilder"/> covering validation, default values,
/// fluent chaining, and immutability of produced <see cref="ApprovalRequest"/> instances.
/// </summary>
public sealed class ApprovalRequestBuilderTests
{
    // ------------------------------------------------------------------
    // WithTitle validation
    // ------------------------------------------------------------------

    [Fact]
    public void WithTitle_EmptyString_ThrowsArgumentException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.WithTitle(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void WithTitle_WhitespaceString_ThrowsArgumentException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.WithTitle("   ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void WithTitle_NullString_ThrowsArgumentNullException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.WithTitle(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("title");
    }

    // ------------------------------------------------------------------
    // RequiringApprovers validation
    // ------------------------------------------------------------------

    [Fact]
    public void RequiringApprovers_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.RequiringApprovers(0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("n");
    }

    [Fact]
    public void RequiringApprovers_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.RequiringApprovers(-1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("n");
    }

    // ------------------------------------------------------------------
    // WithTimeout validation
    // ------------------------------------------------------------------

    [Fact]
    public void WithTimeout_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.WithTimeout(TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    [Fact]
    public void WithTimeout_NegativeTimeSpan_ThrowsArgumentOutOfRangeException()
    {
        var builder = new ApprovalRequestBuilder();

        var act = () => builder.WithTimeout(TimeSpan.FromMinutes(-1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    // ------------------------------------------------------------------
    // Default Build() state
    // ------------------------------------------------------------------

    [Fact]
    public void Build_Defaults_ProducesExpectedRequest()
    {
        var request = new ApprovalRequestBuilder().Build();

        request.Title.Should().Be("Approval");
        request.RequiredApprovers.Should().Be(1);
        request.Timeout.Should().Be(TimeSpan.FromHours(24));
        request.Context.Should().BeEmpty();
        request.AllowedRoles.Should().BeNull();
        request.Description.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Full fluent chain
    // ------------------------------------------------------------------

    [Fact]
    public void Build_FullFluentChain_ProducesMatchingRecord()
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle("Deploy")
            .WithDescription("desc")
            .RequiringApprovers(3)
            .WithTimeout(TimeSpan.FromHours(2))
            .AllowedFor("sre", "lead")
            .WithContext("commit", "abc")
            .WithCorrelationId("corr-1")
            .Build();

        request.Title.Should().Be("Deploy");
        request.Description.Should().Be("desc");
        request.RequiredApprovers.Should().Be(3);
        request.Timeout.Should().Be(TimeSpan.FromHours(2));
        request.AllowedRoles.Should().BeEquivalentTo(new[] { "sre", "lead" });
        request.Context.Should().ContainKey("commit").WhoseValue.Should().Be("abc");
        request.CorrelationId.Should().Be("corr-1");
    }

    // ------------------------------------------------------------------
    // WithContext accumulation
    // ------------------------------------------------------------------

    [Fact]
    public void WithContext_MultipleEntries_AccumulatesAllEntries()
    {
        var request = new ApprovalRequestBuilder()
            .WithContext("env", "prod")
            .WithContext("commit", "deadbeef")
            .WithContext("requestedBy", "alice")
            .Build();

        request.Context.Should().HaveCount(3)
            .And.ContainKey("env").WhoseValue.Should().Be("prod");
        request.Context.Should().ContainKey("commit").WhoseValue.Should().Be("deadbeef");
        request.Context.Should().ContainKey("requestedBy").WhoseValue.Should().Be("alice");
    }

    // ------------------------------------------------------------------
    // AllowedFor with empty array
    // ------------------------------------------------------------------

    [Fact]
    public void AllowedFor_EmptyArray_SetsEmptyListNotNull()
    {
        var request = new ApprovalRequestBuilder()
            .AllowedFor()
            .Build();

        request.AllowedRoles.Should().NotBeNull()
            .And.BeEmpty();
    }

    // ------------------------------------------------------------------
    // WithCorrelationId
    // ------------------------------------------------------------------

    [Fact]
    public void WithCorrelationId_SetsTheProperty()
    {
        const string expectedId = "my-correlation-id-123";

        var request = new ApprovalRequestBuilder()
            .WithCorrelationId(expectedId)
            .Build();

        request.CorrelationId.Should().Be(expectedId);
    }

    [Fact]
    public void Build_WithoutCorrelationId_AutoGeneratesNonEmptyGuid()
    {
        var request = new ApprovalRequestBuilder().Build();

        request.CorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(request.CorrelationId, out _).Should().BeTrue(
            because: "the default correlation ID should be a valid GUID string");
    }

    // ------------------------------------------------------------------
    // Two builders produce distinct CorrelationIds
    // ------------------------------------------------------------------

    [Fact]
    public void TwoSeparateBuilders_ProduceDistinctCorrelationIds()
    {
        var request1 = new ApprovalRequestBuilder().Build();
        var request2 = new ApprovalRequestBuilder().Build();

        request1.CorrelationId.Should().NotBe(request2.CorrelationId);
    }
}
