using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class WebhookTriggerTests
{
    [Fact]
    public void WebhookTriggerResponse_HasExpectedProperties()
    {
        var response = new WebhookTriggerResponse
        {
            RunId = "run123",
            Status = "Running",
            WebhookId = "wh_abc"
        };

        response.RunId.Should().Be("run123");
        response.Status.Should().Be("Running");
        response.WebhookId.Should().StartWith("wh_");
    }

    [Fact]
    public void SetScheduleRequest_HasDefaults()
    {
        var request = new SetScheduleRequest();

        request.CronExpression.Should().BeEmpty();
        request.Enabled.Should().BeTrue();
    }

    [Fact]
    public void WebhookTriggerResponse_WebhookIdFormat()
    {
        var id = $"wh_{Guid.NewGuid():N}"[..12];
        id.Should().StartWith("wh_");
        id.Length.Should().Be(12);
    }
}
