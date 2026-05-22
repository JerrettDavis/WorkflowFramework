using System.Xml.Linq;
using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Readers;

[Feature("XML source reader")]
public class XmlSourceReaderTests : TinyBddTestBase
{
    public XmlSourceReaderTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Reads a simple element value via XPath"), Fact]
    public async Task ReadsSimpleElement() =>
        await Given("XML document with root/name element", () =>
                XDocument.Parse("<root><name>Alice</name></root>"))
            .When("read with path //name", doc =>
                new XmlSourceReader().Read("//name", doc))
            .Then("value is Alice", v =>
            {
                v.Should().Be("Alice");
            })
            .AssertPassed();

    [Scenario("Reads a nested element value via XPath"), Fact]
    public async Task ReadsNestedElement() =>
        await Given("XML with nested customer/address/city", () =>
                XDocument.Parse("<root><customer><address><city>London</city></address></customer></root>"))
            .When("read with path //city", doc =>
                new XmlSourceReader().Read("//city", doc))
            .Then("value is London", v =>
            {
                v.Should().Be("London");
            })
            .AssertPassed();

    [Scenario("Reads an XML attribute value via XPath"), Fact]
    public async Task ReadsAttribute() =>
        await Given("XML element with an attribute", () =>
                XDocument.Parse("<root><item id=\"42\">value</item></root>"))
            .When("read the id attribute with XPath", doc =>
                new XmlSourceReader().Read("//item/@id", doc))
            .Then("value is 42", v =>
            {
                v.Should().Be("42");
            })
            .AssertPassed();

    [Scenario("Returns null for a path that matches nothing"), Fact]
    public async Task ReturnNullWhenPathMissing() =>
        await Given("XML document without the requested element", () =>
                XDocument.Parse("<root><name>Bob</name></root>"))
            .When("read a non-existent path", doc =>
                new XmlSourceReader().Read("//missing", doc))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Returns null for an empty path"), Fact]
    public async Task ReturnsNullForEmptyPath() =>
        await Given("any XML document", () =>
                XDocument.Parse("<root/>"))
            .When("read with an empty path", doc =>
                new XmlSourceReader().Read(string.Empty, doc))
            .Then("result is null", v =>
            {
                v.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("CanRead returns true for paths starting with /"), Fact]
    public async Task CanReadSlashPaths() =>
        await Given("an XmlSourceReader", () => new XmlSourceReader())
            .When("CanRead is called with /root/name", r => r.CanRead("/root/name"))
            .Then("result is true", result =>
            {
                result.Should().BeTrue();
            })
            .AssertPassed();

    [Scenario("CanRead returns false for non-slash paths"), Fact]
    public async Task CanReadReturnsFalseForOtherPrefixes() =>
        await Given("an XmlSourceReader", () => new XmlSourceReader())
            .When("CanRead is called with a $.path", r => r.CanRead("$.something"))
            .Then("result is false", result =>
            {
                result.Should().BeFalse();
            })
            .AssertPassed();
}
