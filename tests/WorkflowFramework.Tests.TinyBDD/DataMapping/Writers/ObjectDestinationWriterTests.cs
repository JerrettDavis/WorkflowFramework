using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Writers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Writers;

[Feature("Object destination writer")]
public class ObjectDestinationWriterTests : TinyBddTestBase
{
    private sealed class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public Dimensions Dimensions { get; set; } = new();
    }

    private sealed class Dimensions
    {
        public int Width { get; set; }
    }

    public ObjectDestinationWriterTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Writes a string value to a public property"), Fact]
    public async Task WritesStringProperty() =>
        await Given("a Product with empty Name", () => new Product())
            .When("write 'Widget' to @.Name", p =>
            {
                new ObjectDestinationWriter().Write("@.Name", "Widget", p);
                return p;
            })
            .Then("Name is Widget", p =>
            {
                p.Name.Should().Be("Widget");
            })
            .AssertPassed();

    [Scenario("Converts and writes a numeric value to a value-type property"), Fact]
    public async Task WritesConvertedNumericProperty() =>
        await Given("a Product with zero Price", () => new Product())
            .When("write '9.99' to @.Price", p =>
            {
                new ObjectDestinationWriter().Write("@.Price", "9.99", p);
                return p;
            })
            .Then("Price is 9.99m", p =>
            {
                p.Price.Should().Be(9.99m);
            })
            .AssertPassed();

    [Scenario("Returns false and does not throw for an unknown field"), Fact]
    public async Task ReturnsFalseForUnknownField() =>
        await Given("a Product object", () => new Product())
            .When("write to @.NonExistent", p =>
                new ObjectDestinationWriter().Write("@.NonExistent", "x", p))
            .Then("returns false without throwing", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();

    [Scenario("Returns false for path without @. prefix"), Fact]
    public async Task ReturnsFalseForBadPrefix() =>
        await Given("a Product object", () => new Product())
            .When("write with plain path Name", p =>
                new ObjectDestinationWriter().Write("Name", "value", p))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();

    [Scenario("Writes through to a nested property"), Fact]
    public async Task WritesNestedProperty() =>
        await Given("a Product with nested Dimensions", () => new Product())
            .When("write '50' to @.Dimensions.Width", p =>
            {
                new ObjectDestinationWriter().Write("@.Dimensions.Width", "50", p);
                return p;
            })
            .Then("Dimensions.Width is 50", p =>
            {
                p.Dimensions.Width.Should().Be(50);
            })
            .AssertPassed();
}
