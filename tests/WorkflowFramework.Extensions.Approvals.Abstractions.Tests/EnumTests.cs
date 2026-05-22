using FluentAssertions;
using Xunit;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// Structural tests that lock down the exact member sets of
/// <see cref="ApprovalOutcome"/> and <see cref="OnTimeoutAction"/>.
/// These tests intentionally fail if a member is added or removed without updating the test,
/// making accidental API surface changes visible in CI.
/// </summary>
public sealed class EnumTests
{
    // ------------------------------------------------------------------
    // ApprovalOutcome
    // ------------------------------------------------------------------

    public static TheoryData<ApprovalOutcome> ExpectedApprovalOutcomes => new()
    {
        ApprovalOutcome.Pending,
        ApprovalOutcome.Approved,
        ApprovalOutcome.Rejected,
        ApprovalOutcome.TimedOut,
        ApprovalOutcome.Escalated
    };

    [Theory]
    [MemberData(nameof(ExpectedApprovalOutcomes))]
    public void ApprovalOutcome_ContainsExpectedMember(ApprovalOutcome expected)
    {
        Enum.IsDefined(typeof(ApprovalOutcome), expected).Should().BeTrue(
            because: $"{expected} should be a defined member of ApprovalOutcome");
    }

    [Fact]
    public void ApprovalOutcome_HasExactlyFiveMembers()
    {
        var values = Enum.GetValues(typeof(ApprovalOutcome));

        values.Length.Should().Be(5,
            because: "ApprovalOutcome must define exactly: Pending, Approved, Rejected, TimedOut, Escalated");
    }

    // ------------------------------------------------------------------
    // OnTimeoutAction
    // ------------------------------------------------------------------

    public static TheoryData<OnTimeoutAction> ExpectedOnTimeoutActions => new()
    {
        OnTimeoutAction.AutoReject,
        OnTimeoutAction.AutoApprove,
        OnTimeoutAction.Escalate
    };

    [Theory]
    [MemberData(nameof(ExpectedOnTimeoutActions))]
    public void OnTimeoutAction_ContainsExpectedMember(OnTimeoutAction expected)
    {
        Enum.IsDefined(typeof(OnTimeoutAction), expected).Should().BeTrue(
            because: $"{expected} should be a defined member of OnTimeoutAction");
    }

    [Fact]
    public void OnTimeoutAction_HasExactlyThreeMembers()
    {
        var values = Enum.GetValues(typeof(OnTimeoutAction));

        values.Length.Should().Be(3,
            because: "OnTimeoutAction must define exactly: AutoReject, AutoApprove, Escalate");
    }
}
