using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;

namespace WorkflowFramework.Extensions.Approvals.Cli;

/// <summary>
/// An <see cref="IApprovalChannel"/> implementation that surfaces approval requests on the
/// standard output and waits for a human to respond via the <c>wf approvals approve/reject</c>
/// CLI commands.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="RequestApprovalAsync"/> is called, the channel:
/// <list type="number">
///   <item>Checks whether a <see cref="PendingApproval"/> with the same correlation ID already
///   exists in the store (rehydration scenario). If not, creates and saves a new one.</item>
///   <item>Prints a banner to the console containing the request details and the exact CLI
///   commands the operator must run to approve or reject the request.</item>
///   <item>Delegates the actual wait to
///   <see cref="PersistentApprovalService.WaitForCompletionAsync"/>, which resolves once an
///   external caller invokes
///   <see cref="PersistentApprovalService.ResolveExternalAsync"/>.</item>
/// </list>
/// </para>
/// <para>
/// This channel is suited to local development and CI pipelines where a human operator has
/// terminal access to the machine running the workflow.
/// </para>
/// <para>
/// The <see cref="PersistentApprovalService"/> dependency is accepted as a
/// <see cref="Lazy{T}"/> to break the DI circular reference that would otherwise arise when
/// <c>PersistentApprovalService</c> depends on <c>IApprovalChannel</c>, which in turn
/// resolves this channel.
/// </para>
/// </remarks>
public sealed class CliApprovalChannel : IApprovalChannel
{
    private readonly IApprovalStore _store;
    private readonly Lazy<PersistentApprovalService> _persistent;
    private readonly ILogger<CliApprovalChannel> _logger;
    private readonly IConsole _console;

    /// <summary>
    /// Initialises a new instance of <see cref="CliApprovalChannel"/>.
    /// </summary>
    /// <param name="store">
    /// The approval store used to persist in-flight requests. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="persistent">
    /// A lazy reference to the persistent approval service used to wait for external
    /// resolution. Lazy to break the DI circular dependency. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="logger">
    /// Optional structured logger. A no-op logger is used when <see langword="null"/>.
    /// </param>
    /// <param name="console">
    /// Optional console abstraction. Defaults to <see cref="SystemConsole"/> when
    /// <see langword="null"/>.
    /// </param>
    public CliApprovalChannel(
        IApprovalStore store,
        Lazy<PersistentApprovalService> persistent,
        ILogger<CliApprovalChannel>? logger = null,
        IConsole? console = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _persistent = persistent ?? throw new ArgumentNullException(nameof(persistent));
        _logger = logger ?? NullLogger<CliApprovalChannel>.Instance;
        _console = console ?? new SystemConsole();
    }

    /// <inheritdoc />
    public string Name => "cli";

    /// <summary>
    /// Persists the request (if not already present), prints an actionable banner to the
    /// console, and then waits for an external vote submitted via
    /// <see cref="PersistentApprovalService.ResolveExternalAsync"/>.
    /// </summary>
    /// <param name="request">
    /// The fully-constructed approval request. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that abandons the wait when cancelled.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to the <see cref="ApprovalResponse"/> once
    /// a terminal decision is reached.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Only save to the store if no record already exists (handles rehydration).
        var existing = await _store.LoadAsync(request.CorrelationId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var now = DateTimeOffset.UtcNow;
            var pending = new PendingApproval(
                CorrelationId: request.CorrelationId,
                Request: request,
                PrimaryChannel: Name,
                CreatedAt: now,
                DeadlineAt: now + request.Timeout,
                Votes: Array.Empty<ApprovalRecord>(),
                EscalationChannel: null,
                TimeoutAction: OnTimeoutAction.AutoReject);

            await _store.SaveAsync(pending, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "CLI approval request saved. CorrelationId={CorrelationId} Title={Title}",
                request.CorrelationId, request.Title);
        }
        else
        {
            _logger.LogDebug(
                "Rehydrating existing CLI approval. CorrelationId={CorrelationId}",
                request.CorrelationId);
        }

        PrintBanner(request);

        return await _persistent.Value.WaitForCompletionAsync(request.CorrelationId, cancellationToken)
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Banner
    // -------------------------------------------------------------------------

    private void PrintBanner(ApprovalRequest request)
    {
        var separator = new string('=', 80);
        var id = request.CorrelationId;

        _console.WriteLine(separator);
        _console.WriteLine("APPROVAL REQUIRED");
        _console.WriteLine(separator);
        _console.WriteLine($"Title          : {request.Title}");

        if (!string.IsNullOrWhiteSpace(request.Description))
            _console.WriteLine($"Description    : {request.Description}");

        _console.WriteLine($"Correlation ID : {id}");
        _console.WriteLine($"Required votes : {request.RequiredApprovers}");
        _console.WriteLine($"Timeout        : {request.Timeout}");

        if (request.AllowedRoles is { Count: > 0 })
            _console.WriteLine($"Allowed roles  : {string.Join(", ", request.AllowedRoles)}");

        if (request.Context is { Count: > 0 })
        {
            _console.WriteLine("Context        :");
            foreach (var kv in request.Context)
                _console.WriteLine($"  {kv.Key} = {kv.Value}");
        }

        _console.WriteLine(separator);
        _console.WriteLine("Run one of the following commands to respond:");
        _console.WriteLine($"  wf approvals approve {id} --by <approver-id> [--comment \"<reason>\"]");
        _console.WriteLine($"  wf approvals reject  {id} --by <approver-id> [--comment \"<reason>\"]");
        _console.WriteLine(separator);
    }
}
