using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("CronExpression parsing and scheduling")]
public class CronExpressionTests : TinyBddTestBase
{
    public CronExpressionTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Parse accepts a standard five-field wildcard expression"), Fact]
    public async Task ParseValidExpression() =>
        await Given("a valid five-field cron string", () => "* * * * *")
            .When("Parse is called", expr => CronExpression.Parse(expr))
            .Then("a CronExpression instance is returned without exception", cron =>
            {
                cron.Should().NotBeNull();
                cron.ToString().Should().Be("* * * * *");
                return true;
            })
            .AssertPassed();

    [Scenario("Parse rejects an expression with the wrong field count"), Theory]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("oops")]
    public async Task ParseInvalidFieldCount(string expr) =>
        await Given("an expression with the wrong number of fields", () => expr)
            .When("Parse is invoked and exception is caught", e =>
            {
                Exception? thrown = null;
                try { CronExpression.Parse(e); }
                catch (Exception ex) { thrown = ex; }
                return thrown;
            })
            .Then("a FormatException or ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();

    [Scenario("TryParse returns null for an invalid expression"), Fact]
    public async Task TryParseInvalidReturnsNull() =>
        await Given("an invalid cron string", () => "not-a-cron")
            .When("TryParse is called", expr => CronExpression.TryParse(expr))
            .Then("null is returned", result =>
            {
                result.Should().BeNull();
                return true;
            })
            .AssertPassed();

    [Scenario("GetNextOccurrence returns the next matching minute"), Fact]
    public async Task GetNextOccurrenceReturnsNextMatch()
    {
        var cron = CronExpression.Parse("* * * * *");
        var now = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var next = cron.GetNextOccurrence(now);

        await Given("the next occurrence for '* * * * *' after 10:30", () => next)
            .Then("the result is 10:31 on the same day", n =>
            {
                n.Should().NotBeNull();
                var expected = new DateTimeOffset(2024, 1, 15, 10, 31, 0, TimeSpan.Zero);
                n!.Value.Should().Be(expected);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Matches returns true only for times matching the expression"), Theory]
    [InlineData("0 9 * * *", 2024, 1, 15, 9, 0, true)]
    [InlineData("0 9 * * *", 2024, 1, 15, 9, 1, false)]
    [InlineData("30 * * * *", 2024, 3, 1, 14, 30, true)]
    public async Task MatchesReturnsTrueForMatchingTime(
        string expr, int y, int mo, int d, int h, int min, bool expected)
    {
        var cron = CronExpression.Parse(expr);
        var time = new DateTimeOffset(y, mo, d, h, min, 0, TimeSpan.Zero);
        var result = cron.Matches(time);

        await Given($"whether '{expr}' matches {y}-{mo:00}-{d:00} {h:00}:{min:00}", () => result)
            .Then($"the match result is {expected}", r =>
            {
                r.Should().Be(expected);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step syntax */5 expands to the correct minute set"), Fact]
    public async Task StepSyntaxParsesCorrectly()
    {
        var cron = CronExpression.Parse("*/5 * * * *");
        var base_ = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var at0 = cron.Matches(base_);
        var at5 = cron.Matches(base_.AddMinutes(5));
        var at1 = cron.Matches(base_.AddMinutes(1));

        await Given("match results for */5 at minutes 0, 1, and 5", () => (at0, at5, at1))
            .Then("multiples of 5 match and others do not", r =>
            {
                r.at0.Should().BeTrue();
                r.at5.Should().BeTrue();
                r.at1.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }
}
