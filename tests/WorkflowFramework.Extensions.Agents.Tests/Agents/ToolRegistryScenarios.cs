using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Agents;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Agents.Tests.Agents;

[Feature("ToolRegistry — aggregates tool providers and exposes tools")]
public class ToolRegistryScenarios : TinyBddXunitBase
{
    public ToolRegistryScenarios(ITestOutputHelper output) : base(output) { }

    private static IToolProvider MakeProvider(string toolName, string description = "desc")
    {
        var provider = Substitute.For<IToolProvider>();
        var tool = new ToolDefinition { Name = toolName, Description = description };
        provider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ToolDefinition>>(new List<ToolDefinition> { tool }));
        return provider;
    }

    [Scenario("Empty registry returns no tools"), Fact]
    public async Task EmptyRegistry_ReturnsNoTools()
    {
        var registry = new ToolRegistry();
        var tools = await registry.ListAllToolsAsync();

        await Given("an empty ToolRegistry", () => tools)
            .Then("no tools are returned", t =>
            {
                t.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Registered provider tools are listed"), Fact]
    public async Task RegisteredProvider_ToolsListed()
    {
        var registry = new ToolRegistry();
        registry.Register(MakeProvider("tool-a"));
        var tools = await registry.ListAllToolsAsync();

        await Given("one provider registered with tool 'tool-a'", () => tools)
            .Then("tool-a appears in the list", t =>
            {
                t.Should().Contain(x => x.Name == "tool-a");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple providers' tools are all listed"), Fact]
    public async Task MultipleProviders_AllToolsListed()
    {
        var registry = new ToolRegistry();
        registry.Register(MakeProvider("tool-a"));
        registry.Register(MakeProvider("tool-b"));
        var tools = await registry.ListAllToolsAsync();

        await Given("two providers each with one unique tool", () => tools)
            .Then("both tools are returned", t =>
            {
                t.Should().Contain(x => x.Name == "tool-a");
                t.Should().Contain(x => x.Name == "tool-b");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Last-registered provider wins on name conflict"), Fact]
    public async Task NameConflict_LastRegisteredWins()
    {
        var providerA = MakeProvider("my-tool", "first");
        var providerB = MakeProvider("my-tool", "second");
        var registry = new ToolRegistry();
        registry.Register(providerA);
        registry.Register(providerB);
        var tools = await registry.ListAllToolsAsync();

        await Given("two providers both exposing 'my-tool'", () => tools)
            .Then("only one tool entry for 'my-tool' and it is from the last provider", t =>
            {
                var myTool = t.Where(x => x.Name == "my-tool").ToList();
                myTool.Should().HaveCount(1);
                myTool[0].Description.Should().Be("second");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register with null provider throws ArgumentNullException"), Fact]
    public async Task Register_NullProvider_Throws()
    {
        var registry = new ToolRegistry();
        Exception? caught = null;
        try { registry.Register(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null passed to Register", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Providers property reflects all registered providers"), Fact]
    public async Task Providers_ReflectsAllRegistered()
    {
        var registry = new ToolRegistry();
        var p1 = MakeProvider("t1");
        var p2 = MakeProvider("t2");
        registry.Register(p1);
        registry.Register(p2);

        await Given("two providers registered", () => registry.Providers)
            .Then("Providers has count 2", providers =>
            {
                providers.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }
}
