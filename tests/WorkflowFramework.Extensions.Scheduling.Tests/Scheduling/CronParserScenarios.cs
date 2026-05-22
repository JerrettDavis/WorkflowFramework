using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Scheduling;

namespace WorkflowFramework.Extensions.Scheduling.Tests.Scheduling;

[Feature("CronParser — 5-part cron expression parser")]
public class CronParserScenarios : TinyBddXunitBase
{
    public CronParserScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("wildcard expression returns next minute"), Fact]
    public async Task WildcardExpressionReturnsNextMinute()
    {
        var now = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("* * * * *", now);

        await Given("cron '* * * * *' and reference time 10:30", () => next)
            .Then("next occurrence is at 10:31", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Minute.Should().Be(31);
                n.Value.Hour.Should().Be(10);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("specific minute expression returns correct next occurrence"), Fact]
    public async Task SpecificMinuteReturnsCorrect()
    {
        var now = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("45 10 * * *", now);

        await Given("cron '45 10 * * *' and reference time 10:30", () => next)
            .Then("next occurrence is at 10:45 same day", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Minute.Should().Be(45);
                n.Value.Hour.Should().Be(10);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("step expression (every 15 minutes) returns correct next minute"), Fact]
    public async Task StepExpressionEvery15Minutes()
    {
        var now = new DateTimeOffset(2025, 1, 15, 10, 10, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("*/15 * * * *", now);

        await Given("cron '*/15 * * * *' at 10:10", () => next)
            .Then("next occurrence is at 10:15", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Minute.Should().Be(15);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("invalid expression (wrong part count) throws FormatException"), Fact]
    public async Task InvalidExpressionThrowsFormatException()
    {
        var now = DateTimeOffset.UtcNow;

        await Given("a malformed cron expression with 4 parts instead of 5", () => now)
            .Then("GetNextOccurrence throws FormatException", n =>
            {
                var act = () => CronParser.GetNextOccurrence("* * * *", n);
                act.Should().Throw<FormatException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("range expression returns correct values"), Fact]
    public async Task RangeExpressionReturnsCorrect()
    {
        var now = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("0 14-16 * * *", now);

        await Given("cron '0 14-16 * * *' at 10:00", () => next)
            .Then("next occurrence is at 14:00", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Hour.Should().Be(14);
                n.Value.Minute.Should().Be(0);
                return true;
            })
            .AssertPassed();
    }
}
