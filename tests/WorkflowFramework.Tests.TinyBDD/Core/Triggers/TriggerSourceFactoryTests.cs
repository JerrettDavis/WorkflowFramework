using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("TriggerSourceFactory registration and creation")]
public class TriggerSourceFactoryTests : TinyBddTestBase
{
    public TriggerSourceFactoryTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Create returns a ScheduleTriggerSource for the 'schedule' type"), Fact]
    public async Task CreateReturnsScheduleSource() =>
        await Given("a default factory", () => new TriggerSourceFactory())
            .When("Create is called with type 'schedule'", factory =>
            {
                var def = new TriggerDefinition { Type = "schedule" };
                return factory.Create(def);
            })
            .Then("the returned source is a ScheduleTriggerSource", src =>
            {
                src.Should().BeOfType<ScheduleTriggerSource>();
                return true;
            })
            .AssertPassed();

    [Scenario("Create returns a ManualTriggerSource for the 'manual' type"), Fact]
    public async Task CreateReturnsManualSource() =>
        await Given("a default factory", () => new TriggerSourceFactory())
            .When("Create is called with type 'manual'", factory =>
            {
                var def = new TriggerDefinition { Type = "manual" };
                return factory.Create(def);
            })
            .Then("the returned source is a ManualTriggerSource", src =>
            {
                src.Should().BeOfType<ManualTriggerSource>();
                return true;
            })
            .AssertPassed();

    [Scenario("Create throws InvalidOperationException for an unregistered type"), Fact]
    public async Task CreateThrowsForUnknownType() =>
        await Given("a default factory", () => new TriggerSourceFactory())
            .When("Create is called with an unknown type", factory =>
            {
                Exception? thrown = null;
                try { factory.Create(new TriggerDefinition { Type = "does-not-exist" }); }
                catch (InvalidOperationException ex) { thrown = ex; }
                return thrown;
            })
            .Then("an InvalidOperationException is thrown with the unknown type in the message", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Contain("does-not-exist");
                return true;
            })
            .AssertPassed();

    [Scenario("Register adds a custom trigger type that Create can resolve"), Fact]
    public async Task RegisterCustomType() =>
        await Given("a factory with a custom type registered", () =>
            {
                var factory = new TriggerSourceFactory();
                factory.Register("custom", def => new ManualTriggerSource(def));
                return factory;
            })
            .When("Create is called for the custom type", factory =>
            {
                var def = new TriggerDefinition { Type = "custom" };
                return factory.Create(def);
            })
            .Then("the registered factory produces the source without error", src =>
            {
                src.Should().NotBeNull();
                src.Type.Should().Be("manual");
                return true;
            })
            .AssertPassed();

    [Scenario("GetAvailableTypes returns info for all registered built-in types"), Fact]
    public async Task GetAvailableTypesReturnsBuiltIns() =>
        await Given("a default factory", () => new TriggerSourceFactory())
            .When("GetAvailableTypes is called", factory => factory.GetAvailableTypes())
            .Then("at least the built-in types are present", types =>
            {
                types.Should().NotBeEmpty();
                types.Select(t => t.Type).Should().Contain(new[] { "schedule", "manual" });
                return true;
            })
            .AssertPassed();
}
