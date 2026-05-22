using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("TriggerSourceFactory — extended registration and type-info scenarios")]
public class TriggerSourceFactoryAdditionalScenarios : TinyBddTestBase
{
    public TriggerSourceFactoryAdditionalScenarios(ITestOutputHelper output) : base(output) { }

    // ── null guards ───────────────────────────────────────────────────────────

    [Scenario("Register throws ArgumentNullException for null type"), Fact]
    public async Task RegisterNullTypeThrows()
    {
        var factory = new TriggerSourceFactory();
        Exception? caught = null;
        try { factory.Register(null!, _ => new ManualTriggerSource(new TriggerDefinition { Type = "manual" })); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null type passed to Register()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register throws ArgumentNullException for null factory delegate"), Fact]
    public async Task RegisterNullFactoryThrows()
    {
        var factory = new TriggerSourceFactory();
        Exception? caught = null;
        try { factory.Register("custom-type", (Func<TriggerDefinition, ITriggerSource>)null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null factory delegate passed to Register()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Create throws ArgumentNullException for null definition"), Fact]
    public async Task CreateNullDefinitionThrows()
    {
        var factory = new TriggerSourceFactory();
        Exception? caught = null;
        try { factory.Create(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null definition passed to Create()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── built-in types ────────────────────────────────────────────────────────

    [Scenario("Default factory registers 'filewatch' built-in type"), Fact]
    public async Task DefaultFactoryRegistersFileWatchType()
    {
        var factory = new TriggerSourceFactory();
        var src = factory.Create(new TriggerDefinition { Type = "filewatch" });

        await Given("a default factory", () => src)
            .Then("'filewatch' type resolves to a FileWatchTriggerSource", s =>
            {
                s.Should().BeOfType<FileWatchTriggerSource>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Default factory registers 'audio' built-in type"), Fact]
    public async Task DefaultFactoryRegistersAudioType()
    {
        var factory = new TriggerSourceFactory();
        var src = factory.Create(new TriggerDefinition { Type = "audio" });

        await Given("a default factory", () => src)
            .Then("'audio' type resolves to an AudioInputTriggerSource", s =>
            {
                s.Should().BeOfType<AudioInputTriggerSource>();
                return true;
            })
            .AssertPassed();
    }

    // ── TriggerTypeInfo registration ──────────────────────────────────────────

    [Scenario("Register with TriggerTypeInfo — GetAvailableTypes returns the info"), Fact]
    public async Task RegisterWithTypeInfoAppearsInGetAvailableTypes()
    {
        var factory = new TriggerSourceFactory();
        var info = new TriggerTypeInfo
        {
            Type = "my-custom",
            DisplayName = "My Custom Trigger",
            Description = "A custom trigger for testing.",
            Category = "Test",
            Icon = "test-icon"
        };
        factory.Register("my-custom", _ => new ManualTriggerSource(new TriggerDefinition { Type = "manual" }), info);

        var types = factory.GetAvailableTypes();

        await Given("a factory with a custom type registered with TriggerTypeInfo", () => types)
            .Then("GetAvailableTypes contains the custom type info", t =>
            {
                t.Should().Contain(ti => ti.Type == "my-custom" && ti.DisplayName == "My Custom Trigger");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register without TriggerTypeInfo — type is not present in GetAvailableTypes"), Fact]
    public async Task RegisterWithoutTypeInfoNotInGetAvailableTypes()
    {
        var factory = new TriggerSourceFactory();
        factory.Register("no-info-type", _ => new ManualTriggerSource(new TriggerDefinition { Type = "manual" }));

        var types = factory.GetAvailableTypes();

        await Given("a factory with type registered without info", () => types)
            .Then("GetAvailableTypes does not contain the no-info type", t =>
            {
                t.Should().NotContain(ti => ti.Type == "no-info-type");
                return true;
            })
            .AssertPassed();
    }

    // ── overwrite registration ────────────────────────────────────────────────

    [Scenario("Re-registering an existing type overwrites the factory"), Fact]
    public async Task ReRegisteringTypeOverwritesFactory()
    {
        var factory = new TriggerSourceFactory();
        factory.Register("swap", _ => new ManualTriggerSource(new TriggerDefinition { Type = "manual" }));
        factory.Register("swap", _ => new ScheduleTriggerSource(new TriggerDefinition { Type = "schedule" }));

        var src = factory.Create(new TriggerDefinition { Type = "swap" });

        await Given("type 'swap' re-registered as ScheduleTriggerSource", () => src)
            .Then("resolved source is ScheduleTriggerSource", s =>
            {
                s.Should().BeOfType<ScheduleTriggerSource>();
                return true;
            })
            .AssertPassed();
    }

    // ── type name in error message ────────────────────────────────────────────

    [Scenario("InvalidOperationException message lists registered types"), Fact]
    public async Task UnknownTypeErrorListsRegisteredTypes()
    {
        var factory = new TriggerSourceFactory();
        // schedule, filewatch, manual, audio are registered by default
        Exception? caught = null;
        try { factory.Create(new TriggerDefinition { Type = "not-there" }); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("Create called with 'not-there' type", () => caught?.Message)
            .Then("error message lists known types", msg =>
            {
                msg.Should().NotBeNullOrEmpty();
                msg.Should().Contain("schedule"); // one of the built-in types
                return true;
            })
            .AssertPassed();
    }
}
