namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// A fluent builder for constructing validated <see cref="ApprovalRequest"/> instances.
/// All validation is performed eagerly at the setter level so that callers receive
/// <see cref="ArgumentException"/>-family exceptions at the point of the invalid call
/// rather than at <see cref="Build"/> time.
/// </summary>
/// <remarks>
/// <para>
/// Each <c>With*</c> method mutates the builder's internal state and returns <c>this</c>
/// to enable method chaining. The builder is <em>not</em> thread-safe; do not share a
/// builder instance across threads.
/// </para>
/// <para>
/// Call <see cref="Build"/> to produce an immutable <see cref="ApprovalRequest"/>. The
/// builder may be reused after <see cref="Build"/> is called.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ApprovalRequest request = new ApprovalRequestBuilder()
///     .WithTitle("Promote to Production")
///     .WithDescription("Merges release/v2.3 into main and triggers the CD pipeline.")
///     .RequiringApprovers(2)
///     .WithTimeout(TimeSpan.FromHours(4))
///     .AllowedFor("sre", "release-manager")
///     .WithContext("commit", "a1b2c3d4")
///     .WithContext("environment", "production")
///     .Build();
/// </code>
/// </example>
public sealed class ApprovalRequestBuilder
{
    private string _title = "Approval";
    private string? _description;
    private readonly Dictionary<string, object?> _context = new();
    private int _required = 1;
    private TimeSpan _timeout = TimeSpan.FromHours(24);
    private List<string>? _allowedRoles;
    private string? _correlationId;

    /// <summary>
    /// Sets the human-readable title that will appear as the subject of the approval notification.
    /// </summary>
    /// <param name="title">
    /// A non-null, non-empty string. Leading and trailing whitespace is significant.
    /// </param>
    /// <returns>This builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is an empty string or consists only of whitespace.</exception>
    public ApprovalRequestBuilder WithTitle(string title)
    {
        if (title is null)
            throw new ArgumentNullException(nameof(title));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title must not be empty or whitespace.", nameof(title));

        _title = title;
        return this;
    }

    /// <summary>
    /// Sets an optional longer description providing context for approvers.
    /// </summary>
    /// <param name="description">
    /// A descriptive string. May contain markdown if the target channel supports rendering it.
    /// Pass <see langword="null"/> to clear a previously set description.
    /// </param>
    /// <returns>This builder instance, for chaining.</returns>
    public ApprovalRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the minimum number of individual approvers whose affirmative votes are required
    /// before the request is considered approved.
    /// </summary>
    /// <param name="n">Must be greater than or equal to one.</param>
    /// <returns>This builder instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="n"/> is less than one.
    /// </exception>
    public ApprovalRequestBuilder RequiringApprovers(int n)
    {
        if (n < 1)
            throw new ArgumentOutOfRangeException(nameof(n), n, "RequiredApprovers must be at least 1.");

        _required = n;
        return this;
    }

    /// <summary>
    /// Sets the maximum time to wait for approvals before the timeout action is applied.
    /// </summary>
    /// <param name="timeout">Must be a strictly positive <see cref="TimeSpan"/>.</param>
    /// <returns>This builder instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeout"/> is zero or negative.
    /// </exception>
    public ApprovalRequestBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be a positive TimeSpan.");

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Restricts voting to users who hold at least one of the specified roles.
    /// Calling this method replaces any previously configured role list.
    /// </summary>
    /// <param name="roles">
    /// One or more role names. Pass an empty array to allow any authenticated user to vote
    /// (which sets <see cref="ApprovalRequest.AllowedRoles"/> to an empty list rather than
    /// <see langword="null"/>).
    /// </param>
    /// <returns>This builder instance, for chaining.</returns>
    public ApprovalRequestBuilder AllowedFor(params string[] roles)
    {
        _allowedRoles = new List<string>(roles);
        return this;
    }

    /// <summary>
    /// Adds or overwrites a single key-value pair in the contextual data bag attached to the
    /// request. Multiple calls accumulate entries.
    /// </summary>
    /// <param name="key">The context key. Must not be <see langword="null"/>.</param>
    /// <param name="value">
    /// The associated value. May be <see langword="null"/>. The value will be serialized
    /// by each channel implementation as appropriate for its notification format.
    /// </param>
    /// <returns>This builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public ApprovalRequestBuilder WithContext(string key, object? value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        _context[key] = value;
        return this;
    }

    /// <summary>
    /// Overrides the auto-generated correlation identifier with a caller-supplied value.
    /// Useful when the correlation ID must match an external system's identifier (e.g., a
    /// workflow instance ID or a saga step key).
    /// </summary>
    /// <param name="id">
    /// A non-null string that uniquely identifies this request. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <returns>This builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    public ApprovalRequestBuilder WithCorrelationId(string id)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        _correlationId = id;
        return this;
    }

    /// <summary>
    /// Constructs an immutable <see cref="ApprovalRequest"/> from the current builder state.
    /// </summary>
    /// <returns>
    /// A new <see cref="ApprovalRequest"/> whose properties reflect all values configured on
    /// this builder. If <see cref="WithCorrelationId"/> was not called, a new GUID is
    /// generated automatically.
    /// </returns>
    public ApprovalRequest Build()
    {
        var contextSnapshot = new Dictionary<string, object?>(_context);
        var rolesSnapshot = _allowedRoles is null
            ? (IReadOnlyList<string>?)null
            : _allowedRoles.AsReadOnly();

        var request = new ApprovalRequest(
            _title,
            _description,
            contextSnapshot,
            _required,
            _timeout,
            rolesSnapshot);

        return _correlationId is null
            ? request
            : request with { CorrelationId = _correlationId };
    }
}
