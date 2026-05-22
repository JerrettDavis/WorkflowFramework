using FluentAssertions;
using Xunit;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// Unit tests for <see cref="ApprovalResponse"/> static factory methods and record equality.
/// </summary>
public sealed class ApprovalResponseTests
{
    private static IReadOnlyList<ApprovalRecord> EmptyRecords() =>
        Array.Empty<ApprovalRecord>();

    private static ApprovalRecord SampleRecord() =>
        new(
            ApproverId: "user-42",
            ApproverDisplayName: "Alice Smith",
            Approved: true,
            Comment: "LGTM",
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "slack");

    // ------------------------------------------------------------------
    // ApprovedBy factory
    // ------------------------------------------------------------------

    [Fact]
    public void ApprovedBy_SetsApprovedTrue_OutcomeApproved_ReasonNull()
    {
        var records = new[] { SampleRecord() };

        var response = ApprovalResponse.ApprovedBy(records);

        response.Approved.Should().BeTrue();
        response.Outcome.Should().Be(ApprovalOutcome.Approved);
        response.Reason.Should().BeNull();
        response.Approvals.Should().BeEquivalentTo(records);
    }

    // ------------------------------------------------------------------
    // Rejected factory
    // ------------------------------------------------------------------

    [Fact]
    public void Rejected_SetsApprovedFalse_OutcomeRejected_ReasonPreserved()
    {
        const string reason = "Insufficient testing evidence";
        var records = EmptyRecords();

        var response = ApprovalResponse.Rejected(reason, records);

        response.Approved.Should().BeFalse();
        response.Outcome.Should().Be(ApprovalOutcome.Rejected);
        response.Reason.Should().Be(reason);
        response.Approvals.Should().BeEmpty();
    }

    [Fact]
    public void Rejected_WithNullReason_SetsReasonNull()
    {
        var response = ApprovalResponse.Rejected(null, EmptyRecords());

        response.Reason.Should().BeNull();
        response.Outcome.Should().Be(ApprovalOutcome.Rejected);
    }

    // ------------------------------------------------------------------
    // TimedOut factory
    // ------------------------------------------------------------------

    [Fact]
    public void TimedOut_SetsApprovedFalse_OutcomeTimedOut_ReasonTimedOut()
    {
        var partial = new[] { SampleRecord() };

        var response = ApprovalResponse.TimedOut(partial);

        response.Approved.Should().BeFalse();
        response.Outcome.Should().Be(ApprovalOutcome.TimedOut);
        response.Reason.Should().Be("Timed out");
        response.Approvals.Should().BeEquivalentTo(partial);
    }

    // ------------------------------------------------------------------
    // Escalated factory
    // ------------------------------------------------------------------

    [Fact]
    public void Escalated_SetsApprovedFalse_OutcomeEscalated_ReasonEscalated()
    {
        var partial = EmptyRecords();

        var response = ApprovalResponse.Escalated(partial);

        response.Approved.Should().BeFalse();
        response.Outcome.Should().Be(ApprovalOutcome.Escalated);
        response.Reason.Should().Be("Escalated");
        response.Approvals.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Record equality
    // ------------------------------------------------------------------

    [Fact]
    public void RecordEquality_TwoResponsesWithSameData_AreEqual()
    {
        var records = EmptyRecords();

        var response1 = ApprovalResponse.TimedOut(records);
        var response2 = ApprovalResponse.TimedOut(records);

        response1.Should().Be(response2);
    }

    [Fact]
    public void RecordEquality_DifferentOutcomes_AreNotEqual()
    {
        var records = EmptyRecords();

        var approved = ApprovalResponse.ApprovedBy(records);
        var rejected = ApprovalResponse.Rejected(null, records);

        approved.Should().NotBe(rejected);
    }
}
