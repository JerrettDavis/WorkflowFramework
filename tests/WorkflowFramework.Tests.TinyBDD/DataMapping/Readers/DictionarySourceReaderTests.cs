using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Readers;

[Feature("Dictionary source reader")]
public class DictionarySourceReaderTests : TinyBddTestBase
{
    public DictionarySourceReaderTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Reads a flat string value by key"), Fact]
    public async Task ReadsFlatStringKey() =>
        await Given("dictionary with key 'name'='Alice'", () =>
                new Dictionary<string, object?> { ["name"] = "Alice" })
            .When("read 'name'", d =>
                new DictionarySourceReader().Read("name", d))
            .Then("value is Alice", v =>
            {
                v.Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Returns null for a missing key"), Fact]
    public async Task ReturnsNullForMissingKey() =>
        await Given("empty dictionary", () =>
                new Dictionary<string, object?>())
            .When("read 'missing'", d =>
                new DictionarySourceReader().Read("missing", d))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Reads nested dictionary via dot-notation"), Fact]
    public async Task ReadsNestedDictionary() =>
        await Given("nested dictionary customer.city", () =>
            {
                IDictionary<string, object?> inner = new Dictionary<string, object?> { ["city"] = "Berlin" };
                return new Dictionary<string, object?> { ["customer"] = inner };
            })
            .When("read 'customer.city'", d =>
                new DictionarySourceReader().Read("customer.city", (IDictionary<string, object?>)d))
            .Then("value is Berlin", v =>
            {
                v.Should().Be("Berlin");
            })
            .AssertPassed();

    [Scenario("Returns string representation of a numeric value"), Fact]
    public async Task ReturnsStringForNumericValue() =>
        await Given("dictionary with integer value", () =>
                new Dictionary<string, object?> { ["count"] = 42 })
            .When("read 'count'", d =>
                new DictionarySourceReader().Read("count", d))
            .Then("value is '42'", v =>
            {
                v.Should().Be("42");
            })
            .AssertPassed();

    [Scenario("CanRead is false for paths starting with $."), Fact]
    public async Task CanReadFalseForJsonPrefix() =>
        await Given("a DictionarySourceReader", () => new DictionarySourceReader())
            .When("CanRead called with $.something", r => r.CanRead("$.something"))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();
}
