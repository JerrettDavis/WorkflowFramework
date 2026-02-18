namespace WorkflowFramework.Testing;

/// <summary>
/// Fluent assertions for workflow results.
/// </summary>
public static class WorkflowAssertions
{
    /// <summary>Asserts the result completed successfully.</summary>
    public static WorkflowResult ShouldBeCompleted(this WorkflowResult result)
    {
        if (result.Status != WorkflowStatus.Completed)
            throw new InvalidOperationException($"Expected Completed but was {result.Status}.");
        return result;
    }

    /// <summary>Asserts the result faulted.</summary>
    public static WorkflowResult ShouldBeFaulted(this WorkflowResult result)
    {
        if (result.Status != WorkflowStatus.Faulted)
            throw new InvalidOperationException($"Expected Faulted but was {result.Status}.");
        return result;
    }

    /// <summary>Asserts the result was compensated.</summary>
    public static WorkflowResult ShouldBeCompensated(this WorkflowResult result)
    {
        if (result.Status != WorkflowStatus.Compensated)
            throw new InvalidOperationException($"Expected Compensated but was {result.Status}.");
        return result;
    }

    /// <summary>Asserts the result has a specific property.</summary>
    public static WorkflowResult ShouldHaveProperty(this WorkflowResult result, string key)
    {
        if (!result.Context.Properties.ContainsKey(key))
            throw new InvalidOperationException($"Expected property '{key}' not found.");
        return result;
    }

    /// <summary>Asserts the result has a property with a specific value.</summary>
    public static WorkflowResult ShouldHaveProperty(this WorkflowResult result, string key, object? expectedValue)
    {
        if (!result.Context.Properties.TryGetValue(key, out var value))
            throw new InvalidOperationException($"Expected property '{key}' not found.");
        if (!Equals(value, expectedValue))
            throw new InvalidOperationException($"Expected property '{key}' to be '{expectedValue}' but was '{value}'.");
        return result;
    }

    /// <summary>Asserts the result has no errors.</summary>
    public static WorkflowResult ShouldHaveNoErrors(this WorkflowResult result)
    {
        if (result.Errors.Count > 0)
            throw new InvalidOperationException($"Expected no errors but found {result.Errors.Count}.");
        return result;
    }
}
