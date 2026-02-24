namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Simple cron expression parser supporting: minute hour day month weekday.
/// Supports * and */n syntax.
/// </summary>
public static class SimpleCronParser
{
    /// <summary>
    /// Returns true if the given cron expression matches the specified time.
    /// Format: "minute hour day month weekday" (0-based weekday, 0=Sunday).
    /// </summary>
    public static bool Matches(string cronExpression, DateTimeOffset time)
    {
        var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        return FieldMatches(parts[0], time.Minute, 0, 59)
            && FieldMatches(parts[1], time.Hour, 0, 23)
            && FieldMatches(parts[2], time.Day, 1, 31)
            && FieldMatches(parts[3], time.Month, 1, 12)
            && FieldMatches(parts[4], (int)time.DayOfWeek, 0, 6);
    }

    /// <summary>
    /// Validates a cron expression format.
    /// </summary>
    public static bool IsValid(string cronExpression)
    {
        var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        return IsValidField(parts[0], 0, 59)
            && IsValidField(parts[1], 0, 23)
            && IsValidField(parts[2], 1, 31)
            && IsValidField(parts[3], 1, 12)
            && IsValidField(parts[4], 0, 6);
    }

    private static bool FieldMatches(string field, int value, int min, int max)
    {
        if (field == "*") return true;

        if (field.StartsWith("*/"))
        {
            if (int.TryParse(field.AsSpan(2), out var interval) && interval > 0)
                return value % interval == 0;
            return false;
        }

        // Comma-separated values
        foreach (var part in field.Split(','))
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var lo) && int.TryParse(range[1], out var hi))
                {
                    if (value >= lo && value <= hi) return true;
                }
            }
            else if (int.TryParse(part, out var exact) && exact == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidField(string field, int min, int max)
    {
        if (field == "*") return true;
        if (field.StartsWith("*/"))
            return int.TryParse(field.AsSpan(2), out var i) && i > 0;

        foreach (var part in field.Split(','))
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (range.Length != 2 || !int.TryParse(range[0], out _) || !int.TryParse(range[1], out _))
                    return false;
            }
            else if (!int.TryParse(part, out var v) || v < min || v > max)
            {
                return false;
            }
        }
        return true;
    }
}
