using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Writers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Writers;

[Feature("Dictionary destination writer")]
public class DictionaryDestinationWriterTests : TinyBddTestBase
{
    public DictionaryDestinationWriterTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Writes a value to a flat key"), Fact]
    public async Task WritesFlatKey() =>
        await Given("empty dictionary", () => new Dictionary<string, object?>())
            .When("write 'Alice' to 'name'", d =>
            {
                new DictionaryDestinationWriter().Write("name", "Alice", d);
                return d;
            })
            .Then("dictionary contains name=Alice", d =>
            {
                d["name"].Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Creates nested dictionary for a dot-notation key"), Fact]
    public async Task CreatesNestedDictionary() =>
        await Given("empty dictionary", () => new Dictionary<string, object?>())
            .When("write 'Berlin' to 'customer.city'", d =>
            {
                new DictionaryDestinationWriter().Write("customer.city", "Berlin", d);
                return d;
            })
            .Then("nested customer.city is Berlin", d =>
            {
                var nested = d["customer"] as IDictionary<string, object?>;
                nested.Should().NotBeNull();
                nested!["city"].Should().Be("Berlin");
            })
            .AssertPassed();

    [Scenario("Overwrites an existing value"), Fact]
    public async Task OverwritesExistingValue() =>
        await Given("dictionary with key 'status'='old'", () =>
                new Dictionary<string, object?> { ["status"] = "old" })
            .When("write 'new' to 'status'", d =>
            {
                new DictionaryDestinationWriter().Write("status", "new", d);
                return d;
            })
            .Then("status is now new", d =>
            {
                d["status"].Should().Be("new");
            })
            .AssertPassed();

    [Scenario("Returns false for an empty path"), Fact]
    public async Task ReturnsFalseForEmptyPath() =>
        await Given("empty dictionary", () => new Dictionary<string, object?>())
            .When("write with empty path", d =>
                new DictionaryDestinationWriter().Write(string.Empty, "x", d))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();
}
