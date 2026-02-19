using FluentAssertions;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Routing;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class RoutingPatternTests
{
    #region ContentBasedRouter

    [Fact]
    public async Task ContentBasedRouter_NullRoutes_Throws()
    {
        var act = () => new ContentBasedRouterStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ContentBasedRouter_NoMatch_NoDefault_DoesNothing()
    {
        var router = new ContentBasedRouterStep(new (Func<IWorkflowContext, bool>, IStep)[]
        {
            (ctx => false, new TestStep("A"))
        });
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        context.Properties.Should().BeEmpty();
    }

    [Fact]
    public async Task ContentBasedRouter_NoMatch_WithDefault_ExecutesDefault()
    {
        var defaultStep = new TestStep("Default", ctx => { ctx.Properties["ran"] = "default"; return Task.CompletedTask; });
        var router = new ContentBasedRouterStep(
            new (Func<IWorkflowContext, bool>, IStep)[] { (ctx => false, new TestStep("A")) },
            defaultStep);
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        context.Properties["ran"].Should().Be("default");
    }

    [Fact]
    public async Task ContentBasedRouter_MultipleRoutes_OnlyFirstMatchExecutes()
    {
        var log = new List<string>();
        var router = new ContentBasedRouterStep(new (Func<IWorkflowContext, bool>, IStep)[]
        {
            (ctx => true, new TestStep("A", ctx => { log.Add("A"); return Task.CompletedTask; })),
            (ctx => true, new TestStep("B", ctx => { log.Add("B"); return Task.CompletedTask; })),
        });
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        log.Should().Equal("A");
    }

    [Fact]
    public async Task ContentBasedRouter_SecondRouteMatches()
    {
        var executed = "";
        var router = new ContentBasedRouterStep(new (Func<IWorkflowContext, bool>, IStep)[]
        {
            (ctx => false, new TestStep("A")),
            (ctx => true, new TestStep("B", ctx => { executed = "B"; return Task.CompletedTask; })),
        });
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        executed.Should().Be("B");
    }

    [Fact]
    public void ContentBasedRouter_Name_IsContentBasedRouter()
    {
        var router = new ContentBasedRouterStep(Array.Empty<(Func<IWorkflowContext, bool>, IStep)>());
        router.Name.Should().Be("ContentBasedRouter");
    }

    #endregion

    #region MessageFilter

    [Fact]
    public void MessageFilter_NullPredicate_Throws()
    {
        var act = () => new MessageFilterStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MessageFilter_PredicateTrue_DoesNotAbort()
    {
        var filter = new MessageFilterStep(ctx => true);
        var context = new WorkflowContext();
        await filter.ExecuteAsync(context);
        context.IsAborted.Should().BeFalse();
    }

    [Fact]
    public async Task MessageFilter_PredicateFalse_Aborts()
    {
        var filter = new MessageFilterStep(ctx => false);
        var context = new WorkflowContext();
        await filter.ExecuteAsync(context);
        context.IsAborted.Should().BeTrue();
    }

    [Fact]
    public void MessageFilter_Name_IsMessageFilter()
    {
        new MessageFilterStep(ctx => true).Name.Should().Be("MessageFilter");
    }

    #endregion

    #region DynamicRouter

    [Fact]
    public void DynamicRouter_NullFunction_Throws()
    {
        var act = () => new DynamicRouterStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DynamicRouter_ReturnsNull_Immediately_DoesNothing()
    {
        var router = new DynamicRouterStep(ctx => null);
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        context.Properties.Should().BeEmpty();
    }

    [Fact]
    public async Task DynamicRouter_EvolvingRoutes_ExecutesMultipleTimes()
    {
        var count = 0;
        var step = new TestStep("Inc", ctx => { count++; ctx.Properties["c"] = count; return Task.CompletedTask; });
        var router = new DynamicRouterStep(ctx =>
        {
            var c = ctx.Properties.TryGetValue("c", out var v) ? (int)v! : 0;
            return c < 5 ? step : null;
        });
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        count.Should().Be(5);
    }

    [Fact]
    public async Task DynamicRouter_StopsOnAbort()
    {
        var count = 0;
        var step = new TestStep("Abort", ctx =>
        {
            count++;
            if (count == 2) ctx.IsAborted = true;
            return Task.CompletedTask;
        });
        var router = new DynamicRouterStep(ctx => step);
        var context = new WorkflowContext();
        await router.ExecuteAsync(context);
        count.Should().Be(2);
    }

    [Fact]
    public void DynamicRouter_Name_IsDynamicRouter()
    {
        new DynamicRouterStep(ctx => null).Name.Should().Be("DynamicRouter");
    }

    #endregion

    #region RecipientList

    [Fact]
    public void RecipientList_NullSelector_Throws()
    {
        var act = () => new RecipientListStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RecipientList_EmptyList_DoesNothing()
    {
        var step = new RecipientListStep(ctx => Enumerable.Empty<IStep>());
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
    }

    [Fact]
    public async Task RecipientList_Sequential_ExecutesAll()
    {
        var log = new List<string>();
        var step = new RecipientListStep(ctx => new IStep[]
        {
            new TestStep("A", c => { log.Add("A"); return Task.CompletedTask; }),
            new TestStep("B", c => { log.Add("B"); return Task.CompletedTask; }),
        });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("A", "B");
    }

    [Fact]
    public async Task RecipientList_Parallel_ExecutesAll()
    {
        var count = 0;
        var step = new RecipientListStep(ctx => new IStep[]
        {
            new TestStep("A", c => { Interlocked.Increment(ref count); return Task.CompletedTask; }),
            new TestStep("B", c => { Interlocked.Increment(ref count); return Task.CompletedTask; }),
        }, parallel: true);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        count.Should().Be(2);
    }

    [Fact]
    public async Task RecipientList_Sequential_StopsOnAbort()
    {
        var log = new List<string>();
        var step = new RecipientListStep(ctx => new IStep[]
        {
            new TestStep("A", c => { log.Add("A"); c.IsAborted = true; return Task.CompletedTask; }),
            new TestStep("B", c => { log.Add("B"); return Task.CompletedTask; }),
        });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("A");
    }

    [Fact]
    public void RecipientList_Name_IsRecipientList()
    {
        new RecipientListStep(ctx => Array.Empty<IStep>()).Name.Should().Be("RecipientList");
    }

    #endregion

    #region RoutingSlip

    [Fact]
    public void RoutingSlip_NullItinerary_Throws()
    {
        var act = () => new RoutingSlip(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoutingSlip_Advance_BeyondEnd_ReturnsFalse()
    {
        var slip = new RoutingSlip(new[] { "a" });
        slip.CurrentStep.Should().Be("a");
        slip.Advance().Should().BeFalse();
        slip.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void RoutingSlip_EmptyItinerary_CurrentStepIsNull()
    {
        var slip = new RoutingSlip(Array.Empty<string>());
        slip.CurrentStep.Should().BeNull();
        slip.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void RoutingSlip_Traversal()
    {
        var slip = new RoutingSlip(new[] { "a", "b", "c" });
        slip.CurrentStep.Should().Be("a");
        slip.Advance().Should().BeTrue();
        slip.CurrentStep.Should().Be("b");
        slip.Advance().Should().BeTrue();
        slip.CurrentStep.Should().Be("c");
        slip.Advance().Should().BeFalse();
        slip.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void RoutingSlipStep_NullSlipSelector_Throws()
    {
        var act = () => new RoutingSlipStep(null!, new Dictionary<string, IStep>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoutingSlipStep_NullStepRegistry_Throws()
    {
        var act = () => new RoutingSlipStep(ctx => new RoutingSlip(Array.Empty<string>()), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RoutingSlipStep_ExecutesAllStepsInOrder()
    {
        var log = new List<string>();
        var registry = new Dictionary<string, IStep>
        {
            ["a"] = new TestStep("a", ctx => { log.Add("a"); return Task.CompletedTask; }),
            ["b"] = new TestStep("b", ctx => { log.Add("b"); return Task.CompletedTask; }),
        };
        var step = new RoutingSlipStep(ctx => new RoutingSlip(new[] { "a", "b" }), registry);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("a", "b");
        context.Properties.Should().ContainKey(RoutingSlipStep.RoutingSlipKey);
    }

    [Fact]
    public async Task RoutingSlipStep_MissingStep_Throws()
    {
        var step = new RoutingSlipStep(
            ctx => new RoutingSlip(new[] { "missing" }),
            new Dictionary<string, IStep>());
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public async Task RoutingSlipStep_StopsOnAbort()
    {
        var log = new List<string>();
        var registry = new Dictionary<string, IStep>
        {
            ["a"] = new TestStep("a", ctx => { log.Add("a"); ctx.IsAborted = true; return Task.CompletedTask; }),
            ["b"] = new TestStep("b", ctx => { log.Add("b"); return Task.CompletedTask; }),
        };
        var step = new RoutingSlipStep(ctx => new RoutingSlip(new[] { "a", "b" }), registry);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("a");
    }

    [Fact]
    public void RoutingSlipStep_Name_IsRoutingSlip()
    {
        var step = new RoutingSlipStep(ctx => new RoutingSlip(Array.Empty<string>()), new Dictionary<string, IStep>());
        step.Name.Should().Be("RoutingSlip");
    }

    #endregion

    #region Helpers

    private sealed class TestStep : IStep
    {
        private readonly Func<IWorkflowContext, Task>? _action;
        public TestStep(string name, Func<IWorkflowContext, Task>? action = null)
        {
            Name = name;
            _action = action;
        }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action?.Invoke(context) ?? Task.CompletedTask;
    }

    #endregion
}
