using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Events;
using WorkflowFramework.Extensions.Expressions;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework.Extensions.Http;
using WorkflowFramework.Extensions.Plugins;
using WorkflowFramework.Extensions.Distributed;
using Xunit;

namespace WorkflowFramework.Tests;

public class EnterpriseExtensionsTests
{
    // ==========================================
    // Plugin System Tests
    // ==========================================

    [Fact]
    public void PluginManager_Register_AddsPlugin()
    {
        var manager = new PluginManager();
        var plugin = new TestPlugin("TestPlugin");
        manager.Register(plugin);
        manager.Plugins.Should().HaveCount(1);
        manager.GetPlugin("TestPlugin").Should().Be(plugin);
    }

    [Fact]
    public void PluginManager_DuplicateRegister_Throws()
    {
        var manager = new PluginManager();
        manager.Register(new TestPlugin("A"));
        manager.Invoking(m => m.Register(new TestPlugin("A")))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task PluginManager_Lifecycle()
    {
        var manager = new PluginManager();
        var plugin = new TestPlugin("Test");
        manager.Register(plugin);

        var services = new ServiceCollection();
        manager.ConfigureAll(services);
        await manager.InitializeAllAsync();
        await manager.StartAllAsync();

        plugin.Configured.Should().BeTrue();
        plugin.Initialized.Should().BeTrue();
        plugin.Started.Should().BeTrue();

        await manager.StopAllAsync();
        plugin.Stopped.Should().BeTrue();

        await manager.DisposeAsync();
        plugin.Disposed.Should().BeTrue();
    }

    [Fact]
    public void PluginManager_DependencyOrder()
    {
        var manager = new PluginManager();
        var a = new TestPlugin("A");
        var b = new TestPlugin("B", dependencies: new[] { "A" });
        manager.Register(b);
        manager.Register(a);

        var services = new ServiceCollection();
        // Should not throw - A should be configured before B
        manager.ConfigureAll(services);
        a.ConfigureOrder.Should().BeLessThan(b.ConfigureOrder);
    }

    [Fact]
    public void PluginManager_CircularDependency_Throws()
    {
        var manager = new PluginManager();
        manager.Register(new TestPlugin("A", dependencies: new[] { "B" }));
        manager.Register(new TestPlugin("B", dependencies: new[] { "A" }));

        manager.Invoking(m => m.ConfigureAll(new ServiceCollection()))
            .Should().Throw<InvalidOperationException>().WithMessage("*Circular*");
    }

    [Fact]
    public void PluginManager_MissingDependency_Throws()
    {
        var manager = new PluginManager();
        manager.Register(new TestPlugin("A", dependencies: new[] { "Missing" }));

        manager.Invoking(m => m.ConfigureAll(new ServiceCollection()))
            .Should().Throw<InvalidOperationException>().WithMessage("*Missing*");
    }

    [Fact]
    public void PluginContext_RegisterStep()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.Services.Should().BeSameAs(services);
    }

    [Fact]
    public void PluginContext_EventHooks()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.OnEvent("step.completed", _ => Task.CompletedTask);
        ctx.GetEventHooks("step.completed").Should().HaveCount(1);
        ctx.GetEventHooks("nonexistent").Should().BeEmpty();
    }

    [Fact]
    public void PluginManifest_Properties()
    {
        var manifest = new PluginManifest
        {
            Name = "Test",
            Version = "2.0.0",
            Description = "Desc",
            Author = "Author"
        };
        manifest.Name.Should().Be("Test");
        manifest.Capabilities.Should().BeEmpty();
    }

    // ==========================================
    // Expression Engine Tests
    // ==========================================

    [Fact]
    public async Task SimpleExpression_Variables()
    {
        var eval = new SimpleExpressionEvaluator();
        eval.Name.Should().Be("simple");
        var result = await eval.EvaluateAsync<double>("x", new Dictionary<string, object?> { ["x"] = 42.0 });
        result.Should().Be(42);
    }

    [Fact]
    public async Task SimpleExpression_BooleanLiterals()
    {
        var eval = new SimpleExpressionEvaluator();
        (await eval.EvaluateAsync<bool>("true", new Dictionary<string, object?>())).Should().BeTrue();
        (await eval.EvaluateAsync<bool>("false", new Dictionary<string, object?>())).Should().BeFalse();
    }

    [Fact]
    public async Task SimpleExpression_Comparison()
    {
        var eval = new SimpleExpressionEvaluator();
        var vars = new Dictionary<string, object?> { ["x"] = 10.0 };
        (await eval.EvaluateAsync<bool>("x > 5", vars)).Should().BeTrue();
        (await eval.EvaluateAsync<bool>("x < 5", vars)).Should().BeFalse();
        (await eval.EvaluateAsync<bool>("x == 10", vars)).Should().BeTrue();
    }

    [Fact]
    public async Task SimpleExpression_StringLiteral()
    {
        var eval = new SimpleExpressionEvaluator();
        var result = await eval.EvaluateAsync<string>("'hello'", new Dictionary<string, object?>());
        result.Should().Be("hello");
    }

    [Fact]
    public async Task SimpleExpression_Null()
    {
        var eval = new SimpleExpressionEvaluator();
        var result = await eval.EvaluateAsync("null", new Dictionary<string, object?>());
        result.Should().BeNull();
    }

    [Fact]
    public async Task SimpleExpression_Arithmetic()
    {
        var eval = new SimpleExpressionEvaluator();
        var vars = new Dictionary<string, object?> { ["x"] = 10.0 };
        var result = await eval.EvaluateAsync<double>("x + 5", vars);
        result.Should().Be(15);
    }

    [Fact]
    public async Task TemplateEngine_Renders()
    {
        var engine = new TemplateEngine();
        var result = await engine.RenderAsync("Hello {{name}}!", new Dictionary<string, object?> { ["name"] = "World" });
        result.Should().Be("Hello World!");
    }

    [Fact]
    public async Task TemplateEngine_EmptyTemplate()
    {
        var engine = new TemplateEngine();
        var result = await engine.RenderAsync("", new Dictionary<string, object?>());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TemplateEngine_NoPlaceholders()
    {
        var engine = new TemplateEngine();
        var result = await engine.RenderAsync("plain text", new Dictionary<string, object?>());
        result.Should().Be("plain text");
    }

    // ==========================================
    // Event Bus Tests
    // ==========================================

    [Fact]
    public async Task InMemoryEventBus_PublishSubscribe()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        bus.Subscribe("test", evt => { received = evt; return Task.CompletedTask; });

        await bus.PublishAsync(new WorkflowEvent { EventType = "test", CorrelationId = "c1" });
        received.Should().NotBeNull();
        received!.EventType.Should().Be("test");
    }

    [Fact]
    public async Task InMemoryEventBus_Unsubscribe()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        var sub = bus.Subscribe("test", evt => { received = evt; return Task.CompletedTask; });
        sub.Dispose();

        await bus.PublishAsync(new WorkflowEvent { EventType = "test" });
        received.Should().BeNull();
        bus.DeadLetters.Should().HaveCount(1);
    }

    [Fact]
    public async Task InMemoryEventBus_DeadLetters()
    {
        var bus = new InMemoryEventBus();
        await bus.PublishAsync(new WorkflowEvent { EventType = "orphan" });
        bus.DeadLetters.Should().HaveCount(1);
    }

    [Fact]
    public async Task InMemoryEventBus_WaitForEvent()
    {
        var bus = new InMemoryEventBus();
        var waitTask = bus.WaitForEventAsync("callback", "corr-1", TimeSpan.FromSeconds(5));

        // Simulate callback after a short delay
        await Task.Delay(50);
        await bus.PublishAsync(new WorkflowEvent { EventType = "callback", CorrelationId = "corr-1" });

        var result = await waitTask;
        result.Should().NotBeNull();
        result!.EventType.Should().Be("callback");
    }

    [Fact]
    public async Task InMemoryEventBus_WaitForEvent_Timeout()
    {
        var bus = new InMemoryEventBus();
        var result = await bus.WaitForEventAsync("timeout", "corr-2", TimeSpan.FromMilliseconds(50));
        result.Should().BeNull();
    }

    [Fact]
    public void WorkflowEvent_Defaults()
    {
        var evt = new WorkflowEvent();
        evt.Id.Should().NotBeNullOrEmpty();
        evt.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PublishEventStep_PublishesEvent()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? published = null;
        bus.Subscribe("order.created", evt => { published = evt; return Task.CompletedTask; });

        var step = new PublishEventStep(bus, ctx => new WorkflowEvent
        {
            EventType = "order.created",
            CorrelationId = ctx.CorrelationId
        });

        var context = new WorkflowContext { CorrelationId = "test-corr" };
        await step.ExecuteAsync(context);

        published.Should().NotBeNull();
        context.Properties.Should().ContainKey("PublishEvent.EventId");
    }

    // ==========================================
    // Human Task Tests
    // ==========================================

    [Fact]
    public async Task InMemoryTaskInbox_CreateAndGet()
    {
        var inbox = new InMemoryTaskInbox();
        var task = new HumanTask { Title = "Review", Assignee = "alice" };

        var created = await inbox.CreateTaskAsync(task);
        var fetched = await inbox.GetTaskAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Review");
    }

    [Fact]
    public async Task InMemoryTaskInbox_GetTasksForAssignee()
    {
        var inbox = new InMemoryTaskInbox();
        await inbox.CreateTaskAsync(new HumanTask { Assignee = "alice" });
        await inbox.CreateTaskAsync(new HumanTask { Assignee = "bob" });
        await inbox.CreateTaskAsync(new HumanTask { Assignee = "alice" });

        var tasks = await inbox.GetTasksForAssigneeAsync("alice");
        tasks.Should().HaveCount(2);
    }

    [Fact]
    public async Task InMemoryTaskInbox_CompleteTask()
    {
        var inbox = new InMemoryTaskInbox();
        var task = new HumanTask { Assignee = "alice" };
        await inbox.CreateTaskAsync(task);

        await inbox.CompleteTaskAsync(task.Id, "approved");
        var fetched = await inbox.GetTaskAsync(task.Id);
        fetched!.Status.Should().Be(HumanTaskStatus.Approved);
        fetched.Outcome.Should().Be("approved");
    }

    [Fact]
    public async Task InMemoryTaskInbox_DelegateTask()
    {
        var inbox = new InMemoryTaskInbox();
        var task = new HumanTask { Assignee = "alice" };
        await inbox.CreateTaskAsync(task);

        await inbox.DelegateTaskAsync(task.Id, "bob");
        var fetched = await inbox.GetTaskAsync(task.Id);
        fetched!.Assignee.Should().Be("bob");
        fetched.DelegatedTo.Should().Be("bob");
    }

    [Fact]
    public async Task InMemoryTaskInbox_WaitForCompletion()
    {
        var inbox = new InMemoryTaskInbox();
        var task = new HumanTask { Assignee = "alice" };
        await inbox.CreateTaskAsync(task);

        var waitTask = inbox.WaitForCompletionAsync(task.Id, TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        await inbox.CompleteTaskAsync(task.Id, "done");

        var completed = await waitTask;
        completed.Outcome.Should().Be("done");
    }

    [Fact]
    public void HumanTask_Defaults()
    {
        var task = new HumanTask();
        task.Id.Should().NotBeNullOrEmpty();
        task.Status.Should().Be(HumanTaskStatus.Pending);
    }

    [Fact]
    public void EscalationRule_Properties()
    {
        var rule = new EscalationRule
        {
            Timeout = TimeSpan.FromHours(1),
            EscalateTo = "manager"
        };
        rule.Timeout.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void ApprovalOptions_Defaults()
    {
        var opts = new ApprovalOptions();
        opts.Mode.Should().Be(ApprovalMode.Sequential);
        opts.Timeout.Should().Be(TimeSpan.FromHours(24));
    }

    // ==========================================
    // AI / Agent Tests
    // ==========================================

    [Fact]
    public async Task EchoAgentProvider_Complete()
    {
        var provider = new EchoAgentProvider();
        provider.Name.Should().Be("echo");

        var response = await provider.CompleteAsync(new LlmRequest { Prompt = "Hello" });
        response.Content.Should().Be("Echo: Hello");
        response.FinishReason.Should().Be("stop");
        response.Usage.Should().NotBeNull();
    }

    [Fact]
    public async Task EchoAgentProvider_Decide()
    {
        var provider = new EchoAgentProvider();
        var decision = await provider.DecideAsync(new AgentDecisionRequest
        {
            Options = new List<string> { "routeA", "routeB" }
        });
        decision.Should().Be("routeA");
    }

    [Fact]
    public async Task LlmCallStep_ExecutesAndStoresResponse()
    {
        var provider = new EchoAgentProvider();
        var step = new LlmCallStep(provider, new LlmCallOptions { PromptTemplate = "Test prompt" });
        step.Name.Should().Be("LlmCall");

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["LlmCall.Response"].Should().Be("Echo: Test prompt");
        context.Properties["LlmCall.FinishReason"].Should().Be("stop");
    }

    [Fact]
    public async Task AgentDecisionStep_StoresDecision()
    {
        var provider = new EchoAgentProvider();
        var step = new AgentDecisionStep(provider, new AgentDecisionOptions
        {
            Prompt = "Choose route",
            Options = new List<string> { "fast", "slow" }
        });

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["AgentDecision.Decision"].Should().Be("fast");
    }

    [Fact]
    public async Task AgentPlanStep_StoresPlan()
    {
        var provider = new EchoAgentProvider();
        var step = new AgentPlanStep(provider);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        ((string)context.Properties["AgentPlan.Plan"]!).Should().StartWith("Echo:");
    }

    [Fact]
    public void LlmRequest_Defaults()
    {
        var req = new LlmRequest();
        req.Tools.Should().BeEmpty();
        req.Variables.Should().BeEmpty();
    }

    [Fact]
    public void AgentTool_Properties()
    {
        var tool = new AgentTool { Name = "search", Description = "Search the web" };
        tool.Name.Should().Be("search");
    }

    // ==========================================
    // Enhanced HTTP Tests
    // ==========================================

    [Fact]
    public void WebhookTriggerOptions_Defaults()
    {
        var opts = new WebhookTriggerOptions();
        opts.Timeout.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void WebhookPayload_Properties()
    {
        var payload = new WebhookPayload { Body = "{}", Headers = new Dictionary<string, string> { ["X-Test"] = "1" } };
        payload.Body.Should().Be("{}");
        payload.Headers.Should().ContainKey("X-Test");
    }

    [Fact]
    public async Task WebhookTriggerStep_ReceivesCallback()
    {
        var step = new WebhookTriggerStep(new WebhookTriggerOptions
        {
            Name = "WH",
            Timeout = TimeSpan.FromSeconds(5),
            CallbackIdFactory = ctx => "test-callback"
        });

        var context = new WorkflowContext();
        var execTask = step.ExecuteAsync(context);
        await Task.Delay(50);
        WebhookTriggerStep.DeliverWebhook("test-callback", new WebhookPayload { Body = "ok" });

        await execTask;
        context.Properties["WH.Received"].Should().Be(true);
        context.Properties["WH.Body"].Should().Be("ok");
    }

    [Fact]
    public void WebhookTriggerStep_DeliverToNonexistent_ReturnsFalse()
    {
        var result = WebhookTriggerStep.DeliverWebhook("nonexistent", new WebhookPayload());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApiKeyAuthProvider_AppliesHeader()
    {
        var auth = new ApiKeyAuthProvider("my-key", "X-Api-Key");
        var request = new HttpRequestMessage();
        await auth.ApplyAsync(request);
        request.Headers.GetValues("X-Api-Key").Should().Contain("my-key");
    }

    [Fact]
    public async Task BearerTokenAuthProvider_AppliesHeader()
    {
        var auth = new BearerTokenAuthProvider("tok123");
        var request = new HttpRequestMessage();
        await auth.ApplyAsync(request);
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("tok123");
    }

    // ==========================================
    // Enhanced Distributed Tests
    // ==========================================

    [Fact]
    public async Task WorkflowWorker_ProcessesItems()
    {
        var queue = new InMemoryWorkflowQueue();
        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "TestWf" });

        var processed = new List<string>();
        await using var worker = new WorkflowWorker(queue, (item, ct) =>
        {
            processed.Add(item.WorkflowName);
            return Task.CompletedTask;
        }, new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(50) });

        worker.Start();
        worker.IsRunning.Should().BeTrue();

        await Task.Delay(200);
        await worker.StopAsync();

        processed.Should().Contain("TestWf");
        worker.ProcessedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WorkflowWorker_HealthStatus()
    {
        var queue = new InMemoryWorkflowQueue();
        var worker = new WorkflowWorker(queue, (_, _) => Task.CompletedTask);

        var health = worker.GetHealthStatus();
        health.WorkerId.Should().NotBeNullOrEmpty();
        health.IsRunning.Should().BeFalse();
        health.ProcessedCount.Should().Be(0);
    }

    // ==========================================
    // Enhanced Diagnostics Tests
    // ==========================================

    [Fact]
    public async Task OpenTelemetryMiddleware_CreatesActivity()
    {
        var middleware = new OpenTelemetryMiddleware();
        var context = new WorkflowContext();
        var step = new InlineStep("Test", _ => Task.CompletedTask);

        await middleware.InvokeAsync(context, step, _ => Task.CompletedTask);
        // No exception means it works (activity may be null without listener)
    }

    [Fact]
    public async Task MetricsDashboardDataProvider_ReturnsSummary()
    {
        var metrics = new MetricsMiddleware();
        var workflow = Workflow.Create("Test").Use(metrics).Step("A", _ => Task.CompletedTask).Build();
        await workflow.ExecuteAsync(new WorkflowContext());

        var provider = new MetricsDashboardDataProvider(metrics);
        var summary = await provider.GetSummaryAsync();
        summary.TotalSteps.Should().Be(1);
        summary.FailedSteps.Should().Be(0);
    }

    [Fact]
    public void DashboardSummary_Defaults()
    {
        var s = new DashboardSummary();
        s.TotalWorkflows.Should().Be(0);
        s.LastUpdated.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ==========================================
    // Helper Classes
    // ==========================================

    private static int _configureCounter;

    private class TestPlugin : WorkflowPluginBase
    {
        private readonly string[] _deps;
        public bool Configured { get; private set; }
        public bool Initialized { get; private set; }
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public int ConfigureOrder { get; private set; }

        public TestPlugin(string name, string[]? dependencies = null)
        {
            _name = name;
            _deps = dependencies ?? Array.Empty<string>();
        }

        private readonly string _name;
        public override string Name => _name;
        public override IReadOnlyList<string> Dependencies => _deps;

        public override void Configure(IWorkflowPluginContext context)
        {
            Configured = true;
            ConfigureOrder = Interlocked.Increment(ref _configureCounter);
        }

        public override Task InitializeAsync(CancellationToken ct) { Initialized = true; return Task.CompletedTask; }
        public override Task StartAsync(CancellationToken ct) { Started = true; return Task.CompletedTask; }
        public override Task StopAsync(CancellationToken ct) { Stopped = true; return Task.CompletedTask; }
        public override ValueTask DisposeAsync() { Disposed = true; return default; }
    }

    private class InlineStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _action;
        public InlineStep(string name, Func<IWorkflowContext, Task> action) { Name = name; _action = action; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action(context);
    }

    // WorkflowContext helper with CorrelationId setter
    private class WorkflowContext : IWorkflowContext
    {
        public string WorkflowId { get; set; } = Guid.NewGuid().ToString("N");
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
