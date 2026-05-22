using System.Text.Json;
using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Readers;

[Feature("JSON source reader")]
public class JsonSourceReaderTests : TinyBddTestBase
{
    public JsonSourceReaderTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Reads a top-level string property"), Fact]
    public async Task ReadsTopLevelString() =>
        await Given("JSON element with name property", () =>
                JsonDocument.Parse("{\"name\":\"Alice\"}").RootElement)
            .When("read $.name", el =>
                new JsonSourceReader().Read("$.name", el))
            .Then("value is Alice", v =>
            {
                v.Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Reads a nested property via dot path"), Fact]
    public async Task ReadsNestedProperty() =>
        await Given("JSON with nested customer.city", () =>
                JsonDocument.Parse("{\"customer\":{\"city\":\"Tokyo\"}}").RootElement)
            .When("read $.customer.city", el =>
                new JsonSourceReader().Read("$.customer.city", el))
            .Then("value is Tokyo", v =>
            {
                v.Should().Be("Tokyo");
            })
            .AssertPassed();

    [Scenario("Reads an array element by index"), Fact]
    public async Task ReadsArrayElementByIndex() =>
        await Given("JSON with items array", () =>
                JsonDocument.Parse("{\"items\":[\"a\",\"b\",\"c\"]}").RootElement)
            .When("read $.items[1]", el =>
                new JsonSourceReader().Read("$.items[1]", el))
            .Then("value is b", v =>
            {
                v.Should().Be("b");
            })
            .AssertPassed();

    [Scenario("Returns null for a missing property"), Fact]
    public async Task ReturnsNullForMissingProperty() =>
        await Given("JSON without the requested key", () =>
                JsonDocument.Parse("{\"name\":\"Bob\"}").RootElement)
            .When("read $.missing", el =>
                new JsonSourceReader().Read("$.missing", el))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Returns null for path without $. prefix"), Fact]
    public async Task ReturnsNullForWrongPrefix() =>
        await Given("any JSON element", () =>
                JsonDocument.Parse("{\"name\":\"Carol\"}").RootElement)
            .When("read with no dollar prefix", el =>
                new JsonSourceReader().Read("name", el))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Reads a numeric value as raw text"), Fact]
    public async Task ReadsNumericValueAsText() =>
        await Given("JSON with numeric field", () =>
                JsonDocument.Parse("{\"score\":99}").RootElement)
            .When("read $.score", el =>
                new JsonSourceReader().Read("$.score", el))
            .Then("value is '99'", v =>
            {
                v.Should().Be("99");
            })
            .AssertPassed();
}
