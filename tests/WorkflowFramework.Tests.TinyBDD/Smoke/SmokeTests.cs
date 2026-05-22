using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Smoke;

/// <summary>
/// Minimal smoke scenario — proves TinyBDD wires up correctly in this project.
/// If this fails, the framework setup is broken; no other scenarios will be reliable.
/// </summary>
[Feature("TinyBDD smoke")]
public class SmokeTests : TinyBddTestBase
{
    public SmokeTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Given/When/Then chains pass"), Fact]
    public async Task GivenWhenThen() =>
        await Given("number", () => 1)
            .When("double", x => x * 2)
            .Then("equals 2", v => v == 2)
            .AssertPassed();
}
