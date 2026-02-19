namespace WorkflowFramework.Extensions.Connectors.Abstractions;

/// <summary>
/// Base configuration for all connectors.
/// </summary>
public class ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the connector name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connector type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the authentication configuration.
    /// </summary>
    public AuthenticationConfig? Authentication { get; set; }

    /// <summary>
    /// Gets or sets the retry configuration.
    /// </summary>
    public RetryConfig Retry { get; set; } = new();

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Authentication configuration.
/// </summary>
public class AuthenticationConfig
{
    /// <summary>
    /// Gets or sets the auth type (e.g., "Basic", "Bearer", "ApiKey").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the credentials.
    /// </summary>
    public IDictionary<string, string> Credentials { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Retry configuration.
/// </summary>
public class RetryConfig
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retries.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to use exponential backoff.
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;
}
