using System.Text.Json.Nodes;
using FluentAssertions;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Teams.Cards;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class AdaptiveCardBuilderTests
{
    private static ApprovalRequest MakeRequest(
        string title = "Deploy to Production",
        string? description = "Please approve the deployment.",
        Dictionary<string, object?>? context = null,
        int requiredApprovers = 1,
        TimeSpan? timeout = null)
    {
        return new ApprovalRequest(
            Title: title,
            Description: description,
            Context: context ?? new Dictionary<string, object?> { ["env"] = "prod", ["sha"] = "abc123" },
            RequiredApprovers: requiredApprovers,
            Timeout: timeout ?? TimeSpan.FromHours(1),
            AllowedRoles: null)
        {
            CorrelationId = "corr-test-001"
        };
    }

    // ------------------------------------------------------------------
    // Card structure
    // ------------------------------------------------------------------

    [Fact]
    public void BuildApprovalCard_HasCorrectTypeVersionAndSchema()
    {
        var request = MakeRequest();
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "approve-tok", "reject-tok");

        card["type"]!.GetValue<string>().Should().Be("AdaptiveCard");
        card["version"]!.GetValue<string>().Should().Be("1.5");
        card["$schema"]!.GetValue<string>().Should().Be("http://adaptivecards.io/schemas/adaptive-card.json");
    }

    [Fact]
    public void BuildApprovalCard_BodyContainsTitle()
    {
        var request = MakeRequest(title: "My Title");
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");

        var body = card["body"]!.AsArray();
        var titleBlock = body.First(n => n!["type"]!.GetValue<string>() == "TextBlock" &&
                                         n["text"]!.GetValue<string>() == "My Title");
        titleBlock.Should().NotBeNull();
        titleBlock!["size"]!.GetValue<string>().Should().Be("Large");
        titleBlock["weight"]!.GetValue<string>().Should().Be("Bolder");
    }

    [Fact]
    public void BuildApprovalCard_BodyContainsDescription()
    {
        var request = MakeRequest(description: "My Description");
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");

        var body = card["body"]!.AsArray();
        var descBlock = body.FirstOrDefault(n =>
            n!["type"]?.GetValue<string>() == "TextBlock" &&
            n["text"]?.GetValue<string>() == "My Description");
        descBlock.Should().NotBeNull();
    }

    [Fact]
    public void BuildApprovalCard_FactSetContainsRequiredApproversTimeoutAndCorrelationId()
    {
        var request = MakeRequest(requiredApprovers: 3, timeout: TimeSpan.FromMinutes(30));
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");

        var body = card["body"]!.AsArray();
        var factSet = body.First(n => n!["type"]!.GetValue<string>() == "FactSet");
        var facts = factSet!["facts"]!.AsArray();

        var factTitles = facts.Select(f => f!["title"]!.GetValue<string>()).ToList();
        factTitles.Should().Contain("Required Approvers");
        factTitles.Should().Contain("Timeout");
        factTitles.Should().Contain("Correlation ID");
    }

    [Fact]
    public void BuildApprovalCard_FactSetContainsContextEntries()
    {
        var context = new Dictionary<string, object?> { ["env"] = "staging", ["commit"] = "deadbeef" };
        var request = MakeRequest(context: context);
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");

        var body = card["body"]!.AsArray();
        var factSet = body.First(n => n!["type"]!.GetValue<string>() == "FactSet");
        var facts = factSet!["facts"]!.AsArray();

        var factTitles = facts.Select(f => f!["title"]!.GetValue<string>()).ToList();
        factTitles.Should().Contain("env");
        factTitles.Should().Contain("commit");
    }

    [Fact]
    public void BuildApprovalCard_EmptyContext_ProducesNoExtraFacts()
    {
        var request = MakeRequest(context: new Dictionary<string, object?>());
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");

        var body = card["body"]!.AsArray();
        var factSet = body.First(n => n!["type"]!.GetValue<string>() == "FactSet");
        var facts = factSet!["facts"]!.AsArray();

        // Should only contain the three metadata facts.
        var factTitles = facts.Select(f => f!["title"]!.GetValue<string>()).ToList();
        factTitles.Should().BeEquivalentTo(["Required Approvers", "Timeout", "Correlation ID"]);
    }

    // ------------------------------------------------------------------
    // Actions
    // ------------------------------------------------------------------

    [Fact]
    public void BuildApprovalCard_HasTwoActionSubmit()
    {
        var request = MakeRequest();
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "approve-tok", "reject-tok");

        var actions = card["actions"]!.AsArray();
        actions.Should().HaveCount(2);
        actions.All(a => a!["type"]!.GetValue<string>() == "Action.Submit").Should().BeTrue();
    }

    [Fact]
    public void BuildApprovalCard_ApproveActionContainsApproveToken()
    {
        var request = MakeRequest();
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "approve-tok", "reject-tok");

        var actions = card["actions"]!.AsArray();
        var approveAction = actions.First(a => a!["title"]!.GetValue<string>() == "Approve");
        approveAction!["data"]!["token"]!.GetValue<string>().Should().Be("approve-tok");
        approveAction["data"]!["decision"]!.GetValue<string>().Should().Be("approve");
        approveAction["data"]!["correlationId"]!.GetValue<string>().Should().Be("corr-test-001");
    }

    [Fact]
    public void BuildApprovalCard_RejectActionContainsRejectToken()
    {
        var request = MakeRequest();
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "approve-tok", "reject-tok");

        var actions = card["actions"]!.AsArray();
        var rejectAction = actions.First(a => a!["title"]!.GetValue<string>() == "Reject");
        rejectAction!["data"]!["token"]!.GetValue<string>().Should().Be("reject-tok");
        rejectAction["data"]!["decision"]!.GetValue<string>().Should().Be("reject");
        rejectAction["data"]!["correlationId"]!.GetValue<string>().Should().Be("corr-test-001");
    }

    // ------------------------------------------------------------------
    // Envelope
    // ------------------------------------------------------------------

    [Fact]
    public void BuildMessageEnvelope_WrapsCardInAttachments()
    {
        var request = MakeRequest();
        var card = AdaptiveCardBuilder.BuildApprovalCard(request, "a", "r");
        var envelope = AdaptiveCardBuilder.BuildMessageEnvelope(card);

        envelope["type"]!.GetValue<string>().Should().Be("message");
        var attachments = envelope["attachments"]!.AsArray();
        attachments.Should().HaveCount(1);
        attachments[0]!["contentType"]!.GetValue<string>()
            .Should().Be("application/vnd.microsoft.card.adaptive");
        attachments[0]!["content"].Should().NotBeNull();
    }
}
