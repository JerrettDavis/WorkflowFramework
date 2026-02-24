namespace WorkflowFramework.Triggers;

/// <summary>
/// A simple cron expression parser supporting standard 5-field cron (minute, hour, day-of-month, month, day-of-week).
/// Supports numeric values, ranges (1-5), lists (1,3,5), steps (*/5), and wildcards (*).
/// </summary>
public sealed class CronExpression
{
    private readonly HashSet<int> _minutes;
    private readonly HashSet<int> _hours;
    private readonly HashSet<int> _daysOfMonth;
    private readonly HashSet<int> _months;
    private readonly HashSet<int> _daysOfWeek;
    private readonly string _expression;

    private CronExpression(string expression, HashSet<int> minutes, HashSet<int> hours,
        HashSet<int> daysOfMonth, HashSet<int> months, HashSet<int> daysOfWeek)
    {
        _expression = expression;
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
    }

    /// <summary>
    /// Parses a 5-field cron expression.
    /// </summary>
    public static CronExpression Parse(string expression)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));

        var parts = expression.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new FormatException($"Cron expression must have 5 fields, got {parts.Length}: '{expression}'");

        return new CronExpression(
            expression,
            ParseField(parts[0], 0, 59),
            ParseField(parts[1], 0, 23),
            ParseField(parts[2], 1, 31),
            ParseField(parts[3], 1, 12),
            ParseField(parts[4], 0, 6));
    }

    /// <summary>
    /// Tries to parse a cron expression, returning null on failure.
    /// </summary>
    public static CronExpression? TryParse(string expression)
    {
        try { return Parse(expression); }
        catch { return null; }
    }

    /// <summary>
    /// Returns true if the given time matches this cron expression.
    /// </summary>
    public bool Matches(DateTimeOffset time)
    {
        return _minutes.Contains(time.Minute)
            && _hours.Contains(time.Hour)
            && _daysOfMonth.Contains(time.Day)
            && _months.Contains(time.Month)
            && _daysOfWeek.Contains((int)time.DayOfWeek);
    }

    /// <summary>
    /// Returns the next occurrence after the given time, or null if none found within ~4 years.
    /// </summary>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after)
    {
        // Start from the next minute
        var candidate = new DateTimeOffset(after.Year, after.Month, after.Day,
            after.Hour, after.Minute, 0, after.Offset).AddMinutes(1);

        // Search up to ~4 years
        var limit = after.AddYears(4);
        while (candidate < limit)
        {
            if (Matches(candidate))
                return candidate;

            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    /// <inheritdoc />
    public override string ToString() => _expression;

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var values = new HashSet<int>();

        foreach (var part in field.Split(','))
        {
            var trimmed = part.Trim();

            if (trimmed == "*")
            {
                for (var i = min; i <= max; i++) values.Add(i);
                continue;
            }

            // Handle step: */N or M-N/S
            var stepParts = trimmed.Split('/');
            if (stepParts.Length == 2)
            {
                if (!int.TryParse(stepParts[1], out var step) || step <= 0)
                    throw new FormatException($"Invalid step value in '{trimmed}'");

                int rangeMin = min, rangeMax = max;
                if (stepParts[0] != "*")
                {
                    var rangeParts = stepParts[0].Split('-');
                    if (rangeParts.Length == 2)
                    {
                        rangeMin = int.Parse(rangeParts[0]);
                        rangeMax = int.Parse(rangeParts[1]);
                    }
                    else
                    {
                        rangeMin = int.Parse(stepParts[0]);
                        rangeMax = max;
                    }
                }

                for (var i = rangeMin; i <= rangeMax; i += step) values.Add(i);
                continue;
            }

            // Handle range: M-N
            var dashParts = trimmed.Split('-');
            if (dashParts.Length == 2)
            {
                var lo = int.Parse(dashParts[0]);
                var hi = int.Parse(dashParts[1]);
                if (lo < min || hi > max || lo > hi)
                    throw new FormatException($"Range {lo}-{hi} out of bounds [{min}-{max}]");
                for (var i = lo; i <= hi; i++) values.Add(i);
                continue;
            }

            // Single value
            var val = int.Parse(trimmed);
            if (val < min || val > max)
                throw new FormatException($"Value {val} out of bounds [{min}-{max}]");
            values.Add(val);
        }

        return values;
    }
}
