using FluentAssertions;
using WorkflowFramework.Triggers;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class CronExpressionTests
{
    [Fact]
    public void Parse_ValidExpression_Succeeds()
    {
        var cron = CronExpression.Parse("*/5 * * * *");
        cron.Should().NotBeNull();
    }

    [Fact]
    public void Parse_InvalidFieldCount_Throws()
    {
        var act = () => CronExpression.Parse("* * *");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        var act = () => CronExpression.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_Invalid_ReturnsNull()
    {
        CronExpression.TryParse("bad").Should().BeNull();
    }

    [Fact]
    public void Matches_EveryMinute_AlwaysTrue()
    {
        var cron = CronExpression.Parse("* * * * *");
        var time = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        cron.Matches(time).Should().BeTrue();
    }

    [Fact]
    public void Matches_SpecificMinute_OnlyMatchesExact()
    {
        var cron = CronExpression.Parse("30 * * * *");
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 15, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Matches_Step_Every5Minutes()
    {
        var cron = CronExpression.Parse("*/5 * * * *");
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 3, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Matches_Range_HoursRange()
    {
        var cron = CronExpression.Parse("0 9-17 * * *");
        cron.Matches(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 17, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Matches_List_SpecificDays()
    {
        var cron = CronExpression.Parse("0 0 * * 1,3,5"); // Mon, Wed, Fri
        // 2026-01-05 is Monday
        cron.Matches(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        // 2026-01-06 is Tuesday
        cron.Matches(new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Matches_DayOfWeek_Sunday()
    {
        var cron = CronExpression.Parse("0 0 * * 0"); // Sunday
        // 2026-01-04 is Sunday
        cron.Matches(new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
    }

    [Fact]
    public void Matches_SpecificMonth()
    {
        var cron = CronExpression.Parse("0 0 1 6 *"); // June 1st
        cron.Matches(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void GetNextOccurrence_FindsNext()
    {
        var cron = CronExpression.Parse("0 12 * * *"); // Every day at noon
        var after = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
        var next = cron.GetNextOccurrence(after);
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(12);
        next.Value.Minute.Should().Be(0);
    }

    [Fact]
    public void GetNextOccurrence_SkipsCurrentMinute()
    {
        var cron = CronExpression.Parse("30 * * * *");
        var after = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero);
        var next = cron.GetNextOccurrence(after);
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(11);
        next.Value.Minute.Should().Be(30);
    }

    [Fact]
    public void Parse_OutOfRange_Throws()
    {
        var act = () => CronExpression.Parse("60 * * * *");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ToString_ReturnsOriginal()
    {
        var expr = "*/5 9-17 * * 1-5";
        CronExpression.Parse(expr).ToString().Should().Be(expr);
    }

    [Fact]
    public void Matches_StepWithRange()
    {
        var cron = CronExpression.Parse("0-30/10 * * * *"); // 0,10,20,30
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 10, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 20, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.Zero)).Should().BeTrue();
        cron.Matches(new DateTimeOffset(2026, 1, 1, 0, 15, 0, TimeSpan.Zero)).Should().BeFalse();
    }
}
