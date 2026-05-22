using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Engine;

namespace WorkflowFramework.Extensions.DataMapping.Tests.DataMapping;

[Feature("FieldTransformerRegistry — thread-safe transformer registration and chain application")]
public class FieldTransformerRegistryScenarios : TinyBddXunitBase
{
    public FieldTransformerRegistryScenarios(ITestOutputHelper output) : base(output) { }

    private static IFieldTransformer MakeTransformer(string name, Func<string?, string?> transform)
    {
        var t = Substitute.For<IFieldTransformer>();
        t.Name.Returns(name);
        t.Transform(Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, string?>?>())
         .Returns(ci => transform(ci.Arg<string?>()));
        return t;
    }

    [Scenario("Get returns registered transformer by name"), Fact]
    public async Task GetReturnsRegisteredTransformer()
    {
        var transformer = MakeTransformer("upper", v => v?.ToUpperInvariant());
        var registry = new FieldTransformerRegistry([transformer]);

        await Given("a registry with 'upper' transformer", () => registry.Get("upper"))
            .Then("Get('upper') returns the transformer", t =>
            {
                t.Should().NotBeNull();
                t!.Name.Should().Be("upper");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Get returns null for unregistered name"), Fact]
    public async Task GetReturnsNullForUnknown()
    {
        var registry = new FieldTransformerRegistry();

        await Given("an empty registry", () => registry.Get("ghost"))
            .Then("result is null", t =>
            {
                t.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register adds a new transformer"), Fact]
    public async Task RegisterAddsTransformer()
    {
        var registry = new FieldTransformerRegistry();
        var transformer = MakeTransformer("trim", v => v?.Trim());
        registry.Register(transformer);

        await Given("'trim' transformer registered", () => registry.Get("trim"))
            .Then("transformer is retrievable", t =>
            {
                t.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register throws for duplicate name"), Fact]
    public async Task RegisterThrowsForDuplicate()
    {
        var registry = new FieldTransformerRegistry([MakeTransformer("dup", v => v)]);

        await Given("a registry with 'dup' transformer", () => registry)
            .Then("registering another 'dup' throws InvalidOperationException", r =>
            {
                var act = () => r.Register(MakeTransformer("dup", v => v));
                act.Should().Throw<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ApplyAll with null chain returns original value"), Fact]
    public async Task ApplyAllNullChainReturnsValue()
    {
        var registry = new FieldTransformerRegistry();
        var result = registry.ApplyAll("hello", null);

        await Given("a null transformer chain", () => result)
            .Then("value is returned unchanged", r =>
            {
                r.Should().Be("hello");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ApplyAll applies transformer chain in order"), Fact]
    public async Task ApplyAllAppliesChainInOrder()
    {
        var upper = MakeTransformer("upper", v => v?.ToUpperInvariant());
        var suffix = MakeTransformer("suffix", v => v + "!");
        var registry = new FieldTransformerRegistry([upper, suffix]);

        var chain = new List<TransformerRef>
        {
            new("upper"),
            new("suffix")
        };

        var result = registry.ApplyAll("hello", chain);

        await Given("chain: upper then suffix applied to 'hello'", () => result)
            .Then("result is 'HELLO!'", r =>
            {
                r.Should().Be("HELLO!");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ApplyAll skips unknown transformer without throwing"), Fact]
    public async Task ApplyAllSkipsUnknownTransformer()
    {
        var registry = new FieldTransformerRegistry();
        var chain = new List<TransformerRef> { new("does-not-exist") };

        var result = registry.ApplyAll("original", chain);

        await Given("a chain with unknown transformer name", () => result)
            .Then("value passes through unchanged", r =>
            {
                r.Should().Be("original");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RegisteredNames lists all transformer names"), Fact]
    public async Task RegisteredNamesListsAll()
    {
        var registry = new FieldTransformerRegistry([
            MakeTransformer("a", v => v),
            MakeTransformer("b", v => v)
        ]);

        await Given("registry with transformers 'a' and 'b'", () => registry.RegisteredNames)
            .Then("RegisteredNames contains both", names =>
            {
                names.Should().Contain("a").And.Contain("b");
                return true;
            })
            .AssertPassed();
    }
}
