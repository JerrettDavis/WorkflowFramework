namespace WorkflowFramework.Extensions.Connectors.Abstractions.BuiltIn;

/// <summary>
/// Configuration for file-based connectors.
/// </summary>
public sealed class FileConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file encoding name (default: UTF-8).
    /// </summary>
    public string Encoding { get; set; } = "utf-8";
}

/// <summary>
/// Configuration for HTTP connectors.
/// </summary>
public sealed class HttpConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the base URL.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method.
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the request headers.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration for SQL database connectors.
/// </summary>
public sealed class SqlConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the SQL command to execute.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Gets or sets the command timeout.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration for FTP/SFTP connectors.
/// </summary>
public sealed class FtpConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the remote host.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port.
    /// </summary>
    public int Port { get; set; } = 21;

    /// <summary>
    /// Gets or sets the remote path.
    /// </summary>
    public string RemotePath { get; set; } = "/";

    /// <summary>
    /// Gets or sets whether to use SFTP.
    /// </summary>
    public bool UseSftp { get; set; }
}
