using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Configuration;

namespace WorkflowFramework.Extensions.Configuration.Tests.Configuration;

[Feature("StepRegistry — runtime step type resolution by name")]
public class StepRegistryScenarios : TinyBddXunitBase
{
    public StepRegistryScenarios(ITestOutputHelper output) : base(output) { }

    private sealed class PingStep : IStep
    {
        public string Name => "ping";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class PongStep : IStep
    {
        public string Name => "pong";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    [Scenario("generic Register<T> uses type name as default key"), Fact]
    public async Task GenericRegisterUsesTypeName()
    {
        var registry = new StepRegistry();
        registry.Register<PingStep>();

        await Given("a registry with PingStep registered via generic overload", () => registry)
            .Then("Resolve('PingStep') returns a PingStep", r =>
            {
                var step = r.Resolve("PingStep");
                step.Should().BeOfType<PingStep>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("generic Register<T> with explicit name uses that name"), Fact]
    public async Task GenericRegisterWithExplicitName()
    {
        var registry = new StepRegistry();
        registry.Register<PingStep>("my-ping");

        await Given("a registry with PingStep registered as 'my-ping'", () => registry)
            .Then("Resolve('my-ping') returns a PingStep", r =>
            {
                var step = r.Resolve("my-ping");
                step.Should().BeOfType<PingStep>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("factory-based Register delegates to provided factory"), Fact]
    public async Task FactoryRegisterDelegates()
    {
        var registry = new StepRegistry();
        registry.Register("custom", () => new PongStep());

        await Given("a registry with factory-based 'custom' registration", () => registry)
            .Then("Resolve('custom') returns a PongStep", r =>
            {
                var step = r.Resolve("custom");
                step.Should().BeOfType<PongStep>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resolve is case-insensitive"), Fact]
    public async Task ResolveIsCaseInsensitive()
    {
        var registry = new StepRegistry();
        registry.Register<PingStep>("Ping");

        await Given("a registry with step registered as 'Ping' (mixed case)", () => registry)
            .Then("Resolve('ping') succeeds despite case difference", r =>
            {
                var step = r.Resolve("ping");
                step.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resolve throws KeyNotFoundException for unknown name"), Fact]
    public async Task ResolveThrowsForUnknown()
    {
        var registry = new StepRegistry();

        await Given("an empty registry", () => registry)
            .Then("Resolve('ghost') throws KeyNotFoundException", r =>
            {
                var act = () => r.Resolve("ghost");
                act.Should().Throw<KeyNotFoundException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Names contains all registered step names"), Fact]
    public async Task NamesContainsAllRegistered()
    {
        var registry = new StepRegistry();
        registry.Register<PingStep>("ping");
        registry.Register<PongStep>("pong");

        await Given("a registry with two steps registered", () => registry.Names)
            .Then("Names contains both 'ping' and 'pong'", names =>
            {
                names.Should().Contain("ping").And.Contain("pong");
                return true;
            })
            .AssertPassed();
    }
}
