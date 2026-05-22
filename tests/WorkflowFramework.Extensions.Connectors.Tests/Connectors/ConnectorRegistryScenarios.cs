using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Tests.Connectors;

[Feature("ConnectorRegistry — registers and retrieves connectors by name")]
public class ConnectorRegistryScenarios : TinyBddXunitBase
{
    public ConnectorRegistryScenarios(ITestOutputHelper output) : base(output) { }

    private static IConnector MakeConnector(string name, string type = "Test")
    {
        var c = Substitute.For<IConnector>();
        c.Name.Returns(name);
        c.Type.Returns(type);
        c.TestConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        return c;
    }

    [Scenario("Get returns null for unregistered connector"), Fact]
    public async Task Get_Unknown_ReturnsNull()
    {
        var registry = new ConnectorRegistry();

        await Given("an empty registry", () => registry.Get("anything"))
            .Then("null is returned", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register and Get retrieves the connector"), Fact]
    public async Task Register_ThenGet_ReturnsConnector()
    {
        var registry = new ConnectorRegistry();
        var connector = MakeConnector("my-connector");
        registry.Register(connector);

        await Given("a connector registered as 'my-connector'", () => registry.Get("my-connector"))
            .Then("the same connector is returned", c =>
            {
                c.Should().BeSameAs(connector);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Get is case-insensitive"), Fact]
    public async Task Get_CaseInsensitive()
    {
        var registry = new ConnectorRegistry();
        registry.Register(MakeConnector("MyConnector"));

        await Given("connector registered as 'MyConnector'", () => registry.Get("myconnector"))
            .Then("lowercase lookup returns the connector", c =>
            {
                c.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Registering duplicate name throws InvalidOperationException"), Fact]
    public async Task Register_Duplicate_Throws()
    {
        var registry = new ConnectorRegistry();
        registry.Register(MakeConnector("dup"));
        Exception? caught = null;
        try { registry.Register(MakeConnector("dup")); }
        catch (Exception ex) { caught = ex; }

        await Given("two connectors with the same name registered", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register with null connector throws ArgumentNullException"), Fact]
    public async Task Register_Null_Throws()
    {
        var registry = new ConnectorRegistry();
        Exception? caught = null;
        try { registry.Register(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null passed to Register", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Names reflects all registered connector names"), Fact]
    public async Task Names_ReflectsAll()
    {
        var registry = new ConnectorRegistry();
        registry.Register(MakeConnector("alpha"));
        registry.Register(MakeConnector("beta"));

        await Given("two connectors registered", () => registry.Names)
            .Then("Names contains both", names =>
            {
                names.Should().Contain("alpha").And.Contain("beta");
                return true;
            })
            .AssertPassed();
    }
}
