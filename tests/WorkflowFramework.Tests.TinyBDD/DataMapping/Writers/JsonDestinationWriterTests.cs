using System.Text.Json.Nodes;
using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Extensions.DataMapping.Writers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Writers;

[Feature("JSON destination writer")]
public class JsonDestinationWriterTests : TinyBddTestBase
{
    public JsonDestinationWriterTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Writes a value to a top-level JSON key"), Fact]
    public async Task WritesTopLevelKey() =>
        await Given("empty JsonObject", () => new JsonObject())
            .When("write 'Alice' to $.name", obj =>
            {
                new JsonDestinationWriter().Write("$.name", "Alice", obj);
                return obj;
            })
            .Then("name property is Alice", obj =>
            {
                obj["name"]!.GetValue<string>().Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Creates nested objects for dot-notation paths"), Fact]
    public async Task CreatesNestedObjects() =>
        await Given("empty JsonObject", () => new JsonObject())
            .When("write 'Tokyo' to $.customer.city", obj =>
            {
                new JsonDestinationWriter().Write("$.customer.city", "Tokyo", obj);
                return obj;
            })
            .Then("nested customer.city is Tokyo", obj =>
            {
                obj["customer"]!["city"]!.GetValue<string>().Should().Be("Tokyo");
            })
            .AssertPassed();

    [Scenario("Writes null value as JSON null"), Fact]
    public async Task WritesNullValue() =>
        await Given("empty JsonObject", () => new JsonObject())
            .When("write null to $.field", obj =>
            {
                new JsonDestinationWriter().Write("$.field", null, obj);
                return obj;
            })
            .Then("field node is null", obj =>
            {
                obj.ContainsKey("field").Should().BeTrue();
                obj["field"].Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Returns false for path without $. prefix"), Fact]
    public async Task ReturnsFalseForWrongPrefix() =>
        await Given("empty JsonObject", () => new JsonObject())
            .When("write to path without $.", obj =>
                new JsonDestinationWriter().Write("name", "x", obj))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();

    [Scenario("Round-trip: JSON read then write produces same value"), Fact]
    public async Task RoundTripReadWrite() =>
        await Given("a JSON element with a known value", () =>
            {
                var src = System.Text.Json.JsonDocument.Parse("{\"score\":\"100\"}").RootElement;
                var reader = new JsonSourceReader();
                return reader.Read("$.score", src);
            })
            .When("write read value back into new JsonObject at $.score", readValue =>
            {
                var dest = new JsonObject();
                new JsonDestinationWriter().Write("$.score", readValue, dest);
                return dest;
            })
            .Then("destination score equals original value", obj =>
            {
                obj["score"]!.GetValue<string>().Should().Be("100");
            })
            .AssertPassed();
}
