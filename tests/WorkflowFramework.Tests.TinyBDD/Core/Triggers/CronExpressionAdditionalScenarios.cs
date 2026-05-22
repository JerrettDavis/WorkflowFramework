using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Triggers;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("CronExpression — extended parsing and next-occurrence scenarios")]
public class CronExpressionAdditionalScenarios : TinyBddTestBase
{
    public CronExpressionAdditionalScenarios(ITestOutputHelper output) : base(output) { }

    // ── range syntax ──────────────────────────────────────────────────────────

    [Scenario("Range syntax M-N in minute field expands to all values in range"), Fact]
    public async Task RangeSyntaxExpandsCorrectly()
    {
        var cron = CronExpression.Parse("5-10 * * * *");

        var matchAt5  = cron.Matches(new DateTimeOffset(2024, 6, 1, 12, 5, 0, TimeSpan.Zero));
        var matchAt10 = cron.Matches(new DateTimeOffset(2024, 6, 1, 12, 10, 0, TimeSpan.Zero));
        var noMatch4  = cron.Matches(new DateTimeOffset(2024, 6, 1, 12, 4, 0, TimeSpan.Zero));
        var noMatch11 = cron.Matches(new DateTimeOffset(2024, 6, 1, 12, 11, 0, TimeSpan.Zero));

        await Given("cron '5-10 * * * *'", () => (matchAt5, matchAt10, noMatch4, noMatch11))
            .Then("matches at 5 and 10, not at 4 or 11", t =>
            {
                t.matchAt5.Should().BeTrue();
                t.matchAt10.Should().BeTrue();
                t.noMatch4.Should().BeFalse();
                t.noMatch11.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Comma-separated list in minute field matches exactly those minutes"), Fact]
    public async Task CommaSeparatedListMatchesExactValues()
    {
        var cron = CronExpression.Parse("0,15,30,45 * * * *");

        var match0  = cron.Matches(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var match15 = cron.Matches(new DateTimeOffset(2024, 1, 1, 0, 15, 0, TimeSpan.Zero));
        var match45 = cron.Matches(new DateTimeOffset(2024, 1, 1, 0, 45, 0, TimeSpan.Zero));
        var noMatch1 = cron.Matches(new DateTimeOffset(2024, 1, 1, 0, 1, 0, TimeSpan.Zero));

        await Given("cron '0,15,30,45 * * * *'", () => (match0, match15, match45, noMatch1))
            .Then("matches at 0,15,45 and not at 1", t =>
            {
                t.match0.Should().BeTrue();
                t.match15.Should().BeTrue();
                t.match45.Should().BeTrue();
                t.noMatch1.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ── day-of-month / month ──────────────────────────────────────────────────

    [Scenario("Daily-at-midnight cron fires on every day and month"), Fact]
    public async Task DailyAtMidnightCronMatchesMidnight()
    {
        var cron = CronExpression.Parse("0 0 * * *");
        var midnight = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var notMidnight = new DateTimeOffset(2024, 6, 15, 0, 1, 0, TimeSpan.Zero);

        await Given("cron '0 0 * * *'", () => (cron.Matches(midnight), cron.Matches(notMidnight)))
            .Then("matches midnight but not 00:01", t =>
            {
                t.Item1.Should().BeTrue();
                t.Item2.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Month-specific cron matches only on the specified month"), Fact]
    public async Task MonthSpecificCronMatchesOnlyInMonth()
    {
        var cron = CronExpression.Parse("0 12 1 6 *"); // noon on June 1

        var juneFirst  = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var mayFirst   = new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

        await Given("cron '0 12 1 6 *'", () => (cron.Matches(juneFirst), cron.Matches(mayFirst)))
            .Then("matches June 1 at noon but not May 1 at noon", t =>
            {
                t.Item1.Should().BeTrue();
                t.Item2.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ── leap year edge case ───────────────────────────────────────────────────

    [Scenario("Cron for February 29 matches on a leap year"), Fact]
    public async Task CronMatchesLeapDayOnLeapYear()
    {
        var cron = CronExpression.Parse("0 0 29 2 *"); // midnight on Feb 29

        var leapDay    = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero); // 2024 is leap
        var nonLeapDay = new DateTimeOffset(2023, 2, 28, 0, 0, 0, TimeSpan.Zero);

        await Given("cron '0 0 29 2 *' (Feb 29)", () => (cron.Matches(leapDay), cron.Matches(nonLeapDay)))
            .Then("matches 2024-02-29 but not 2023-02-28", t =>
            {
                t.Item1.Should().BeTrue();
                t.Item2.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetNextOccurrence skips non-existent dates — Feb 29 in non-leap year"), Fact]
    public async Task GetNextOccurrenceSkipsNonExistentDate()
    {
        // NOTE: current behavior — this expression will never match in non-leap years
        // because the engine searches minute by minute; Feb 29 simply won't be encountered.
        var cron = CronExpression.Parse("0 0 29 2 *");
        var startOfNonLeapYear = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var next = cron.GetNextOccurrence(startOfNonLeapYear);

        await Given("GetNextOccurrence for Feb-29 cron starting from 2023-01-01", () => next)
            .Then("next occurrence is in the next leap year (2024-02-29)", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Year.Should().Be(2024);
                n.Value.Month.Should().Be(2);
                n.Value.Day.Should().Be(29);
                return true;
            })
            .AssertPassed();
    }

    // ── day-of-week ───────────────────────────────────────────────────────────

    [Scenario("Day-of-week cron matches only on the specified weekday"), Theory]
    [InlineData(0, true)]  // Sunday = 0
    [InlineData(1, false)] // Monday
    [InlineData(6, false)] // Saturday
    public async Task DayOfWeekCronMatchesCorrectly(int dayOfWeek, bool shouldMatch)
    {
        var cron = CronExpression.Parse("0 9 * * 0"); // 9am Sundays
        // 2024-01-07 is a Sunday
        var baseDate = new DateTimeOffset(2024, 1, 7, 9, 0, 0, TimeSpan.Zero);
        var testDate = baseDate.AddDays(dayOfWeek);

        var matched = cron.Matches(testDate);

        await Given($"cron '0 9 * * 0' against day-of-week offset {dayOfWeek}", () => matched)
            .Then($"match result should be {shouldMatch}", m =>
            {
                m.Should().Be(shouldMatch);
                return true;
            })
            .AssertPassed();
    }

    // ── GetNextOccurrence ─────────────────────────────────────────────────────

    [Scenario("GetNextOccurrence returns exactly the next matching minute"), Fact]
    public async Task GetNextOccurrenceReturnsExactlyNextMatch()
    {
        var cron = CronExpression.Parse("5 10 * * *"); // 10:05 every day
        var before = new DateTimeOffset(2024, 3, 15, 10, 4, 0, TimeSpan.Zero);
        var next = cron.GetNextOccurrence(before);

        await Given("cron '5 10 * * *' queried from 10:04", () => next)
            .Then("next occurrence is 10:05 on the same day", n =>
            {
                n.Should().NotBeNull();
                n!.Value.Hour.Should().Be(10);
                n.Value.Minute.Should().Be(5);
                n.Value.Day.Should().Be(15);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetNextOccurrence returns null for a cron that can never match (out-of-range)"), Fact]
    public async Task GetNextOccurrenceReturnsNullForImpossibleCron()
    {
        // NOTE: current behavior — a cron that only matches Feb 30 (impossible)
        // would return null after searching ~4 years.
        // We approximate with a day=31 month=2 combo.
        var cron = CronExpression.Parse("0 0 31 2 *"); // Feb 31 — never exists
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var next = cron.GetNextOccurrence(start);

        await Given("a cron expression that can never match (Feb 31)", () => next)
            .Then("GetNextOccurrence returns null", n =>
            {
                n.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Scenario("CronExpression.ToString returns the original expression string"), Fact]
    public async Task ToStringReturnsOriginalExpression()
    {
        const string expr = "30 14 1 1 *";
        var cron = CronExpression.Parse(expr);

        await Given("a parsed cron expression", () => cron.ToString())
            .Then("ToString returns the original expression", s => { s.Should().Be(expr); return true; })
            .AssertPassed();
    }

    // ── invalid expressions ───────────────────────────────────────────────────

    [Scenario("Parse rejects an out-of-range minute value"), Fact]
    public async Task ParseRejectsOutOfRangeMinute()
    {
        Exception? caught = null;
        try { CronExpression.Parse("60 * * * *"); }
        catch (FormatException ex) { caught = ex; }

        await Given("minute field '60' (max is 59)", () => caught)
            .Then("FormatException is thrown", ex =>
            {
                ex.Should().BeOfType<FormatException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parse rejects an out-of-range hour value"), Fact]
    public async Task ParseRejectsOutOfRangeHour()
    {
        Exception? caught = null;
        try { CronExpression.Parse("* 24 * * *"); }
        catch (FormatException ex) { caught = ex; }

        await Given("hour field '24' (max is 23)", () => caught)
            .Then("FormatException is thrown", ex =>
            {
                ex.Should().BeOfType<FormatException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parse rejects a range where start > end"), Fact]
    public async Task ParseRejectsInvalidRange()
    {
        Exception? caught = null;
        try { CronExpression.Parse("10-5 * * * *"); }
        catch (FormatException ex) { caught = ex; }

        await Given("range '10-5' where start > end", () => caught)
            .Then("FormatException is thrown", ex =>
            {
                ex.Should().BeOfType<FormatException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TryParse returns a valid instance for a well-formed expression"), Fact]
    public async Task TryParseReturnsInstanceForValidExpression()
    {
        var cron = CronExpression.TryParse("0 12 * * 1");

        await Given("TryParse with '0 12 * * 1'", () => cron)
            .Then("a non-null CronExpression is returned", c => { c.Should().NotBeNull(); return true; })
            .AssertPassed();
    }

    [Scenario("TryParse returns null for null input"), Fact]
    public async Task TryParseNullReturnsNull()
    {
        var cron = CronExpression.TryParse(null!);

        await Given("TryParse with null input", () => cron)
            .Then("null is returned without throwing", c => { c.Should().BeNull(); return true; })
            .AssertPassed();
    }
}
