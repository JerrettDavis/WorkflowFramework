using System.Text.Json.Nodes;
using FluentAssertions;
using WorkflowFramework.Extensions.Approvals.Slack.Blocks;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackBlockKitBuilderTests
{
    private static ApprovalRequest MakeRequest(
        string title = "Deploy to Production",
        string? description = "Please approve the deployment.",
        int requiredApprovers = 1,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, object?>? context = null) =>
        new(
            Title: title,
            Description: description,
            Context: context ?? new Dictionary<string, object?>(),
            RequiredApprovers: requiredApprovers,
            Timeout: timeout ?? TimeSpan.FromHours(1),
            AllowedRoles: null)
        {
            CorrelationId = "testcorrelation123"
        };

    [Fact]
    public void BuildApprovalMessage_ContainsTitleInHeader()
    {
        var request = MakeRequest(title: "My Approval Title");
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var blocks = result["blocks"]!.AsArray();
        var header = blocks.FirstOrDefault(b => b?["type"]?.GetValue<string>() == "header");
        header.Should().NotBeNull();
        header!["text"]!["text"]!.GetValue<string>().Should().Be("My Approval Title");
    }

    [Fact]
    public void BuildApprovalMessage_ContainsDescriptionInSection()
    {
        var request = MakeRequest(description: "Please review this carefully.");
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var blocks = result["blocks"]!.AsArray();
        var sections = blocks.Where(b => b?["type"]?.GetValue<string>() == "section").ToList();
        var descSection = sections.FirstOrDefault(s =>
            s?["text"]?["type"]?.GetValue<string>() == "mrkdwn" &&
            s["text"]!["text"]!.GetValue<string>().Contains("Please review"));

        descSection.Should().NotBeNull();
    }

    [Fact]
    public void BuildApprovalMessage_RequiredApproversInFields()
    {
        var request = MakeRequest(requiredApprovers: 3);
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var json = result.ToJsonString();
        json.Should().Contain("Required approvers");
        json.Should().Contain("3");
    }

    [Fact]
    public void BuildApprovalMessage_TimeoutInFields()
    {
        var request = MakeRequest(timeout: TimeSpan.FromHours(2));
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var json = result.ToJsonString();
        json.Should().Contain("Timeout");
        json.Should().Contain("hour");
    }

    [Fact]
    public void BuildApprovalMessage_CorrelationIdInFields()
    {
        var request = MakeRequest();
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var json = result.ToJsonString();
        json.Should().Contain("testcorrelation123");
    }

    [Fact]
    public void BuildApprovalMessage_TwoActionButtonsWithCorrectActionIds()
    {
        var request = MakeRequest();
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var blocks = result["blocks"]!.AsArray();
        var actionsBlock = blocks.FirstOrDefault(b => b?["type"]?.GetValue<string>() == "actions");
        actionsBlock.Should().NotBeNull();

        var elements = actionsBlock!["elements"]!.AsArray();
        elements.Should().HaveCount(2);

        var approveBtn = elements.FirstOrDefault(e => e?["action_id"]?.GetValue<string>() == "approve:testcorrelation123");
        var rejectBtn = elements.FirstOrDefault(e => e?["action_id"]?.GetValue<string>() == "reject:testcorrelation123");

        approveBtn.Should().NotBeNull();
        rejectBtn.Should().NotBeNull();

        approveBtn!["style"]!.GetValue<string>().Should().Be("primary");
        rejectBtn!["style"]!.GetValue<string>().Should().Be("danger");
    }

    [Fact]
    public void BuildApprovalMessage_ContextDictionaryEntriesSurfaceAsFields()
    {
        var context = new Dictionary<string, object?>
        {
            ["environment"] = "production",
            ["commit"] = "abc1234"
        };
        var request = MakeRequest(context: context);
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var json = result.ToJsonString();
        json.Should().Contain("environment");
        json.Should().Contain("production");
        json.Should().Contain("commit");
        json.Should().Contain("abc1234");
    }

    [Fact]
    public void BuildApprovalMessage_EmptyContextProducesValidJson()
    {
        var request = MakeRequest(context: new Dictionary<string, object?>());
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        result.Should().NotBeNull();
        result["blocks"].Should().NotBeNull();
    }

    [Fact]
    public void BuildApprovalMessage_NullDescriptionProducesValidJson()
    {
        var request = MakeRequest(description: null);
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        result.Should().NotBeNull();

        // Should not throw and should still have header and actions
        var blocks = result["blocks"]!.AsArray();
        blocks.Any(b => b?["type"]?.GetValue<string>() == "header").Should().BeTrue();
        blocks.Any(b => b?["type"]?.GetValue<string>() == "actions").Should().BeTrue();
    }

    [Fact]
    public void BuildApprovalMessage_ChannelSetOnTopLevel()
    {
        var request = MakeRequest();
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C99999", request);

        result["channel"]!.GetValue<string>().Should().Be("C99999");
    }

    [Fact]
    public void BuildApprovalMessage_LongTitlesTruncatedTo150Chars()
    {
        var longTitle = new string('A', 200);
        var request = MakeRequest(title: longTitle);
        var result = SlackBlockKitBuilder.BuildApprovalMessage("C123", request);

        var blocks = result["blocks"]!.AsArray();
        var header = blocks.FirstOrDefault(b => b?["type"]?.GetValue<string>() == "header");
        var headerText = header!["text"]!["text"]!.GetValue<string>();

        // Header text is capped at 150 characters
        (headerText.Length <= 150).Should().BeTrue("header text must not exceed Slack's 150 character limit");
        headerText.Should().Be(new string('A', 150));
    }
}
