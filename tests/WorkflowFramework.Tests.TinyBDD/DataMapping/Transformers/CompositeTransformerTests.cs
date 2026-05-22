using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Transformers;

[Feature("Composite transformer")]
public class CompositeTransformerTests : TinyBddTestBase
{
    // Minimal stub transformer that appends a suffix.
    private sealed class AppendTransformer(string suffix) : IFieldTransformer
    {
        public string Name => $"append_{suffix}";
        public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
            => input == null ? null : input + suffix;
    }

    // Stub transformer that converts to upper case.
    private sealed class UpperCaseTransformer : IFieldTransformer
    {
        public string Name => "upper";
        public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
            => input?.ToUpperInvariant();
    }

    public CompositeTransformerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Chains two transformers in order"), Fact]
    public async Task ChainsTwoTransformersInOrder() =>
        await Given("input 'hello'", () => "hello")
            .When("composite: upper then append _X", input =>
            {
                var composite = new CompositeTransformer("upper_then_append",
                    [new UpperCaseTransformer(), new AppendTransformer("_X")]);
                return composite.Transform(input);
            })
            .Then("result is 'HELLO_X'", result =>
            {
                result.Should().Be("HELLO_X");
            })
            .AssertPassed();

    [Scenario("Empty chain returns input unchanged"), Fact]
    public async Task EmptyChainReturnsInput() =>
        await Given("input 'data'", () => "data")
            .When("composite with no inner transformers", input =>
            {
                var composite = new CompositeTransformer("empty", []);
                return composite.Transform(input);
            })
            .Then("result is still 'data'", result =>
            {
                result.Should().Be("data");
            })
            .AssertPassed();

    [Scenario("Null propagates through the entire chain"), Fact]
    public async Task NullPropagatesThroughChain() =>
        await Given("null input", () => (string?)null)
            .When("composite: upper then append", input =>
            {
                var composite = new CompositeTransformer("chain",
                    [new UpperCaseTransformer(), new AppendTransformer("!")]);
                return composite.Transform(input);
            })
            .Then("result is null", result =>
            {
                result.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Single-transformer composite behaves like the wrapped transformer"), Fact]
    public async Task SingleTransformerBehavesLikeWrapped() =>
        await Given("input 'world'", () => "world")
            .When("composite wrapping only UpperCaseTransformer", input =>
            {
                var composite = new CompositeTransformer("just_upper",
                    [new UpperCaseTransformer()]);
                return composite.Transform(input);
            })
            .Then("result is 'WORLD'", result =>
            {
                result.Should().Be("WORLD");
            })
            .AssertPassed();

    [Scenario("Name property reflects the name given at construction"), Fact]
    public async Task NamePropertyIsSet() =>
        await Given("a composite with name 'my-chain'", () =>
                new CompositeTransformer("my-chain", [new UpperCaseTransformer()]))
            .When("access Name", t => t.Name)
            .Then("Name is 'my-chain'", name =>
            {
                name.Should().Be("my-chain");
            })
            .AssertPassed();
}
