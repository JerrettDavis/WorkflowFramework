using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class SchedulerTests
{
    [Theory]
    [InlineData("* * * * *", true)]
    [InlineData("0 * * * *", true)]
    [InlineData("*/5 * * * *", true)]
    [InlineData("0 9 * * 1-5", true)]
    [InlineData("30 8,12,18 * * *", true)]
    [InlineData("invalid", false)]
    [InlineData("60 * * * *", false)]  // minute > 59
    [InlineData("* 25 * * *", false)]  // hour > 23
    [InlineData("* * * * * *", false)] // too many fields
    public void CronParser_Validates(string expression, bool expected)
    {
        SimpleCronParser.IsValid(expression).Should().Be(expected);
    }

    [Fact]
    public void CronParser_Wildcard_MatchesAny()
    {
        var time = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        SimpleCronParser.Matches("* * * * *", time).Should().BeTrue();
    }

    [Fact]
    public void CronParser_ExactMinute_Matches()
    {
        var time = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        SimpleCronParser.Matches("30 10 * * *", time).Should().BeTrue();
        SimpleCronParser.Matches("31 10 * * *", time).Should().BeFalse();
    }

    [Fact]
    public void CronParser_StepValue_Matches()
    {
        var time0 = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var time5 = new DateTimeOffset(2025, 6, 15, 10, 5, 0, TimeSpan.Zero);
        var time7 = new DateTimeOffset(2025, 6, 15, 10, 7, 0, TimeSpan.Zero);

        SimpleCronParser.Matches("*/5 * * * *", time0).Should().BeTrue();
        SimpleCronParser.Matches("*/5 * * * *", time5).Should().BeTrue();
        SimpleCronParser.Matches("*/5 * * * *", time7).Should().BeFalse();
    }

    [Fact]
    public void CronParser_Range_Matches()
    {
        // Monday=1 through Friday=5
        var monday = new DateTimeOffset(2025, 6, 16, 9, 0, 0, TimeSpan.Zero); // Monday
        var sunday = new DateTimeOffset(2025, 6, 15, 9, 0, 0, TimeSpan.Zero); // Sunday

        SimpleCronParser.Matches("0 9 * * 1-5", monday).Should().BeTrue();
        SimpleCronParser.Matches("0 9 * * 1-5", sunday).Should().BeFalse();
    }

    [Fact]
    public void CronParser_CommaList_Matches()
    {
        var time8 = new DateTimeOffset(2025, 6, 15, 8, 0, 0, TimeSpan.Zero);
        var time12 = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var time10 = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

        SimpleCronParser.Matches("0 8,12,18 * * *", time8).Should().BeTrue();
        SimpleCronParser.Matches("0 8,12,18 * * *", time12).Should().BeTrue();
        SimpleCronParser.Matches("0 8,12,18 * * *", time10).Should().BeFalse();
    }
}
