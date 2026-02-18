namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Provides data for workflow dashboard UIs.
/// </summary>
public interface IDashboardDataProvider
{
    /// <summary>Gets a summary of workflow execution metrics.</summary>
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a dashboard summary of workflow execution.
/// </summary>
public sealed class DashboardSummary
{
    /// <summary>Gets or sets total workflows executed.</summary>
    public long TotalWorkflows { get; set; }

    /// <summary>Gets or sets total steps executed.</summary>
    public long TotalSteps { get; set; }

    /// <summary>Gets or sets total failed steps.</summary>
    public long FailedSteps { get; set; }

    /// <summary>Gets or sets the average step duration.</summary>
    public TimeSpan AverageStepDuration { get; set; }

    /// <summary>Gets or sets the last update time.</summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dashboard data provider that sources data from <see cref="MetricsMiddleware"/>.
/// </summary>
public sealed class MetricsDashboardDataProvider : IDashboardDataProvider
{
    private readonly MetricsMiddleware _metrics;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsDashboardDataProvider"/>.
    /// </summary>
    public MetricsDashboardDataProvider(MetricsMiddleware metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <inheritdoc />
    public Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DashboardSummary
        {
            TotalSteps = _metrics.TotalSteps,
            FailedSteps = _metrics.FailedSteps,
            AverageStepDuration = _metrics.AverageDuration
        });
    }
}
