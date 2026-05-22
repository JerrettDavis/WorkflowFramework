using FluentAssertions;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class QuorumApprovalAggregatorTests
{
    private static ApprovalRecord Vote(bool approved, string id = "approver") =>
        new(id, id, approved, null, DateTimeOffset.UtcNow, "test");

    // ------------------------------------------------------------------
    // Theory table
    // ------------------------------------------------------------------

    public static TheoryData<IReadOnlyList<ApprovalRecord>, int, int, ApprovalOutcome> Cases =>
        new()
        {
            // 2-of-3 with 1 approval → Pending (2 remaining could approve)
            { new[] { Vote(true, "a1") }, 2, 3, ApprovalOutcome.Pending },

            // 2-of-3 with 2 approvals → Approved
            { new[] { Vote(true, "a1"), Vote(true, "a2") }, 2, 3, ApprovalOutcome.Approved },

            // 2-of-3 with 1 approval + 1 rejection → Pending (1 remaining could push to 2)
            { new[] { Vote(true, "a1"), Vote(false, "a2") }, 2, 3, ApprovalOutcome.Pending },

            // 2-of-3 with 1 approval + 2 rejections → Rejected (0 remaining, only 1 approval)
            { new[] { Vote(true, "a1"), Vote(false, "a2"), Vote(false, "a3") }, 2, 3, ApprovalOutcome.Rejected },

            // 2-of-2 with 1 rejection → Rejected (1 remaining, max reachable = 1 < 2)
            { new[] { Vote(false, "a1") }, 2, 2, ApprovalOutcome.Rejected },

            // 1-of-3 first approval → Approved
            { new[] { Vote(true, "a1") }, 1, 3, ApprovalOutcome.Approved },

            // 3-of-3 all approved → Approved
            { new[] { Vote(true, "a1"), Vote(true, "a2"), Vote(true, "a3") }, 3, 3, ApprovalOutcome.Approved },

            // 3-of-3 with 1 rejection → Rejected
            { new[] { Vote(false, "a1"), Vote(true, "a2"), Vote(true, "a3") }, 3, 3, ApprovalOutcome.Rejected },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate_ReturnsExpectedOutcome(
        IReadOnlyList<ApprovalRecord> votes,
        int required,
        int total,
        ApprovalOutcome expected)
    {
        var outcome = QuorumApprovalAggregator.Evaluate(votes, required, total);
        outcome.Should().Be(expected);
    }

    // ------------------------------------------------------------------
    // Boundary / guard tests
    // ------------------------------------------------------------------

    [Fact]
    public void Evaluate_RequiredApproversZero_Throws()
    {
        var act = () => QuorumApprovalAggregator.Evaluate(Array.Empty<ApprovalRecord>(), 0, 1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("requiredApprovers");
    }

    [Fact]
    public void Evaluate_TotalLessThanRequired_Throws()
    {
        var act = () => QuorumApprovalAggregator.Evaluate(Array.Empty<ApprovalRecord>(), 3, 2);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("totalAddressableApprovers");
    }

    [Fact]
    public void Evaluate_VotesExceedTotal_Throws()
    {
        var votes = new[] { Vote(true, "a1"), Vote(true, "a2") };
        var act = () => QuorumApprovalAggregator.Evaluate(votes, 1, 1);
        act.Should().Throw<ArgumentException>().WithParameterName("votes");
    }

    [Fact]
    public void Evaluate_NullVotes_Throws()
    {
        var act = () => QuorumApprovalAggregator.Evaluate(null!, 1, 1);
        act.Should().Throw<ArgumentNullException>().WithParameterName("votes");
    }
}
