using FluentAssertions;
using WorkflowFramework.Extensions.Http;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Http;

public class WebhookTriggerStepTests
{
    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new WebhookTriggerStep(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions());
        step.Name.Should().Be("WebhookTrigger");
    }

    [Fact]
    public void Name_Custom()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions { Name = "WH" });
        step.Name.Should().Be("WH");
    }

    [Fact]
    public async Task ExecuteAsync_ReceivesCallback()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions
        {
            Name = "WH",
            Timeout = TimeSpan.FromSeconds(5),
            CallbackIdFactory = ctx => "cb-123"
        });
        var ctx = CreateCtx();
        var execTask = step.ExecuteAsync(ctx);
        await Task.Delay(30);
        WebhookTriggerStep.DeliverWebhook("cb-123", new WebhookPayload
        {
            Body = "ok",
            Headers = new Dictionary<string, string> { ["X-H"] = "v" }
        }).Should().BeTrue();
        await execTask;
        ctx.Properties["WH.Received"].Should().Be(true);
        ctx.Properties["WH.Body"].Should().Be("ok");
        ctx.Properties["WH.Header.X-H"].Should().Be("v");
        ctx.Properties["WH.CallbackId"].Should().Be("cb-123");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_SetsReceivedFalse()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions
        {
            Name = "WH",
            Timeout = TimeSpan.FromMilliseconds(30)
        });
        var ctx = CreateCtx();
        await step.ExecuteAsync(ctx);
        ctx.Properties["WH.Received"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCorrelationIdWhenNoFactory()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions
        {
            Timeout = TimeSpan.FromSeconds(5)
        });
        var ctx = CreateCtx();
        ctx.CorrelationId = "my-corr";
        var execTask = step.ExecuteAsync(ctx);
        await Task.Delay(30);
        WebhookTriggerStep.DeliverWebhook("my-corr", new WebhookPayload { Body = "hi" });
        await execTask;
        ctx.Properties["WebhookTrigger.Received"].Should().Be(true);
    }

    [Fact]
    public void DeliverWebhook_NonExistent_ReturnsFalse()
    {
        WebhookTriggerStep.DeliverWebhook("nonexistent", new WebhookPayload()).Should().BeFalse();
    }

    [Fact]
    public void WebhookTriggerOptions_Defaults()
    {
        var o = new WebhookTriggerOptions();
        o.Name.Should().BeNull();
        o.Timeout.Should().Be(TimeSpan.FromMinutes(30));
        o.CallbackIdFactory.Should().BeNull();
    }

    [Fact]
    public void WebhookPayload_Defaults()
    {
        var p = new WebhookPayload();
        p.Body.Should().BeEmpty();
        p.Headers.Should().BeEmpty();
    }

    private static TestCtx CreateCtx() => new();
    private class TestCtx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w";
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
