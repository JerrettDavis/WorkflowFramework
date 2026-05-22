using System.Xml.Linq;
using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Writers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Writers;

[Feature("XML destination writer")]
public class XmlDestinationWriterTests : TinyBddTestBase
{
    public XmlDestinationWriterTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Writes a value to an existing element"), Fact]
    public async Task WritesToExistingElement() =>
        await Given("XDocument with root/name element", () =>
                XDocument.Parse("<root><name></name></root>"))
            .When("write 'Alice' to /root/name", doc =>
            {
                new XmlDestinationWriter().Write("/root/name", "Alice", doc);
                return doc;
            })
            .Then("name element contains Alice", doc =>
            {
                doc.Root!.Element("name")!.Value.Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Creates intermediate elements for a new path"), Fact]
    public async Task CreatesIntermediateElements() =>
        await Given("XDocument with only a root element", () =>
                XDocument.Parse("<root/>"))
            .When("write 'London' to /root/address/city", doc =>
            {
                new XmlDestinationWriter().Write("/root/address/city", "London", doc);
                return doc;
            })
            .Then("nested city element is created with value London", doc =>
            {
                doc.Root!.Element("address")!.Element("city")!.Value.Should().Be("London");
            })
            .AssertPassed();

    [Scenario("Returns false when root element name does not match path"), Fact]
    public async Task ReturnsFalseOnRootMismatch() =>
        await Given("XDocument with root element named 'data'", () =>
                XDocument.Parse("<data/>"))
            .When("write to /root/name (wrong root)", doc =>
                new XmlDestinationWriter().Write("/root/name", "x", doc))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();

    [Scenario("Returns false for a document without a root"), Fact]
    public async Task ReturnsFalseWhenNoRoot() =>
        await Given("XDocument with no root", () => new XDocument())
            .When("write to /root/name", doc =>
                new XmlDestinationWriter().Write("/root/name", "x", doc))
            .Then("returns false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();

    [Scenario("Writes null value as empty string"), Fact]
    public async Task WritesNullAsEmptyString() =>
        await Given("XDocument with root/field element", () =>
                XDocument.Parse("<root><field>old</field></root>"))
            .When("write null to /root/field", doc =>
            {
                new XmlDestinationWriter().Write("/root/field", null, doc);
                return doc;
            })
            .Then("field value is empty string", doc =>
            {
                doc.Root!.Element("field")!.Value.Should().Be(string.Empty);
            })
            .AssertPassed();
}
