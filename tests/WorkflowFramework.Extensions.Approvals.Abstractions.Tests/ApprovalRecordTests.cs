using FluentAssertions;
using Xunit;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// Unit tests for <see cref="ApprovalRecord"/> construction, field preservation, and record equality.
/// </summary>
public sealed class ApprovalRecordTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(2025, 6, 15, 14, 30, 0, TimeSpan.FromHours(5));

    // ------------------------------------------------------------------
    // Construction and field preservation
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_AllFields_PreservesAllValues()
    {
        var record = new ApprovalRecord(
            ApproverId: "user-99",
            ApproverDisplayName: "Bob Jones",
            Approved: true,
            Comment: "Approved after review",
            Timestamp: TestTimestamp,
            Channel: "teams");

        record.ApproverId.Should().Be("user-99");
        record.ApproverDisplayName.Should().Be("Bob Jones");
        record.Approved.Should().BeTrue();
        record.Comment.Should().Be("Approved after review");
        record.Timestamp.Should().Be(TestTimestamp);
        record.Channel.Should().Be("teams");
    }

    [Fact]
    public void Constructor_NullableFields_AcceptsNullValues()
    {
        var record = new ApprovalRecord(
            ApproverId: "svc-account",
            ApproverDisplayName: null,
            Approved: false,
            Comment: null,
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "email");

        record.ApproverDisplayName.Should().BeNull();
        record.Comment.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // DateTimeOffset preservation (including offset)
    // ------------------------------------------------------------------

    [Fact]
    public void Timestamp_WithNonUtcOffset_PreservesOffset()
    {
        var offsetTimestamp = new DateTimeOffset(2025, 1, 10, 9, 0, 0, TimeSpan.FromHours(-7));

        var record = new ApprovalRecord(
            ApproverId: "u1",
            ApproverDisplayName: "Dev User",
            Approved: true,
            Comment: null,
            Timestamp: offsetTimestamp,
            Channel: "cli");

        record.Timestamp.Offset.Should().Be(TimeSpan.FromHours(-7));
        record.Timestamp.Should().Be(offsetTimestamp);
    }

    // ------------------------------------------------------------------
    // Record equality
    // ------------------------------------------------------------------

    [Fact]
    public void RecordEquality_TwoIdenticalRecords_AreEqual()
    {
        var r1 = new ApprovalRecord("u1", "Alice", true, "ok", TestTimestamp, "slack");
        var r2 = new ApprovalRecord("u1", "Alice", true, "ok", TestTimestamp, "slack");

        r1.Should().Be(r2);
    }

    [Fact]
    public void RecordEquality_DifferentApproverIds_AreNotEqual()
    {
        var r1 = new ApprovalRecord("u1", "Alice", true, null, TestTimestamp, "slack");
        var r2 = new ApprovalRecord("u2", "Alice", true, null, TestTimestamp, "slack");

        r1.Should().NotBe(r2);
    }

    [Fact]
    public void RecordEquality_DifferentApprovedValue_AreNotEqual()
    {
        var r1 = new ApprovalRecord("u1", "Alice", true, null, TestTimestamp, "slack");
        var r2 = new ApprovalRecord("u1", "Alice", false, null, TestTimestamp, "slack");

        r1.Should().NotBe(r2);
    }
}
