using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Readers;

[Feature("Object source reader")]
public class ObjectSourceReaderTests : TinyBddTestBase
{
    private sealed class Customer
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public Address Address { get; set; } = new();
    }

    private sealed class Address
    {
        public string City { get; set; } = "";
    }

    public ObjectSourceReaderTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Reads a top-level public property"), Fact]
    public async Task ReadsTopLevelProperty() =>
        await Given("a customer object with Name=Alice", () =>
                new Customer { Name = "Alice" })
            .When("read @.Name", c =>
                new ObjectSourceReader().Read("@.Name", c))
            .Then("value is Alice", v =>
            {
                v.Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Reads a nested property via dot-notation"), Fact]
    public async Task ReadsNestedProperty() =>
        await Given("a customer with nested address city", () =>
                new Customer { Address = new Address { City = "Paris" } })
            .When("read @.Address.City", c =>
                new ObjectSourceReader().Read("@.Address.City", c))
            .Then("value is Paris", v =>
            {
                v.Should().Be("Paris");
            })
            .AssertPassed();

    [Scenario("Returns null for a property that does not exist"), Fact]
    public async Task ReturnsNullForMissingProperty() =>
        await Given("a customer object", () => new Customer { Name = "Bob" })
            .When("read @.NonExistent", c =>
                new ObjectSourceReader().Read("@.NonExistent", c))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Returns null when path has wrong prefix"), Fact]
    public async Task ReturnsNullForWrongPrefix() =>
        await Given("a customer object", () => new Customer { Name = "Carol" })
            .When("read with plain property name (no @. prefix)", c =>
                new ObjectSourceReader().Read("Name", c))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Returns null when intermediate property is null"), Fact]
    public async Task ReturnsNullWhenIntermediateIsNull() =>
        await Given("a customer with null Address", () =>
                new Customer { Address = null! })
            .When("read @.Address.City", c =>
                new ObjectSourceReader().Read("@.Address.City", c))
            .Then("result is null without exception", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();
}
