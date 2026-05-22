using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Configuration;

namespace WorkflowFramework.Extensions.Configuration.Tests.Configuration;

[Feature("JsonWorkflowDefinitionLoader — deserializes JSON workflow definitions")]
public class JsonWorkflowDefinitionLoaderScenarios : TinyBddXunitBase
{
    public JsonWorkflowDefinitionLoaderScenarios(ITestOutputHelper output) : base(output) { }

    private static readonly JsonWorkflowDefinitionLoader Loader = new();

    [Scenario("loads name from JSON"), Fact]
    public async Task LoadsNameFromJson()
    {
        var json = """{"name":"OrderFlow","steps":[]}""";
        var def = Loader.Load(json);

        await Given("JSON with name 'OrderFlow'", () => def)
            .Then("definition Name is 'OrderFlow'", d =>
            {
                d.Name.Should().Be("OrderFlow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("loads step list from JSON"), Fact]
    public async Task LoadsStepListFromJson()
    {
        var json = """{"name":"W","steps":[{"type":"step","class":"MyStep"}]}""";
        var def = Loader.Load(json);

        await Given("JSON with one step definition", () => def)
            .Then("Steps contains one entry", d =>
            {
                d.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("step type and class are loaded correctly"), Fact]
    public async Task StepTypeAndClassLoaded()
    {
        var json = """{"name":"W","steps":[{"type":"step","class":"DoThing","name":"MyStep"}]}""";
        var def = Loader.Load(json);

        await Given("JSON with step type='step', class='DoThing', name='MyStep'", () => def.Steps[0])
            .Then("step definition has correct type, class, and name", s =>
            {
                s.Type.Should().Be("step");
                s.Class.Should().Be("DoThing");
                s.Name.Should().Be("MyStep");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("compensation flag defaults to false"), Fact]
    public async Task CompensationDefaultsFalse()
    {
        var json = """{"name":"W","steps":[]}""";
        var def = Loader.Load(json);

        await Given("JSON without compensation field", () => def)
            .Then("Compensation is false", d =>
            {
                d.Compensation.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("compensation flag is loaded when set to true"), Fact]
    public async Task CompensationFlagLoaded()
    {
        var json = """{"name":"W","steps":[],"compensation":true}""";
        var def = Loader.Load(json);

        await Given("JSON with compensation:true", () => def)
            .Then("Compensation is true", d =>
            {
                d.Compensation.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("invalid JSON throws exception"), Fact]
    public async Task InvalidJsonThrows()
    {
        await Given("invalid JSON content", () => new JsonWorkflowDefinitionLoader())
            .Then("Load throws an exception", l =>
            {
                var act = () => l.Load("{ INVALID JSON !");
                act.Should().Throw<Exception>();
                return true;
            })
            .AssertPassed();
    }
}
