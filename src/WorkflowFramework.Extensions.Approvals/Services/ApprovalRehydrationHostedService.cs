using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An <see cref="IHostedService"/> that re-attaches in-flight approvals from the
/// <see cref="IApprovalStore"/> to the <see cref="PersistentApprovalService"/> after a
/// process restart, ensuring that pending approvals survive host recycling.
/// </summary>
/// <remarks>
/// <para>
/// On <see cref="StartAsync"/>:
/// <list type="number">
///   <item>Loads all non-complete pending approvals from the store.</item>
///   <item>Calls <see cref="PersistentApprovalService.Rehydrate"/> for each, which registers
///   a TCS and reschedules the deadline timer.</item>
///   <item>If a pending approval's deadline has already passed, the rehydration path fires
///   the timeout action immediately.</item>
/// </list>
/// </para>
/// <para>
/// On <see cref="StopAsync"/>: no in-flight TCS instances are forcibly completed — they
/// survive the next restart and are re-loaded via rehydration.
/// </para>
/// </remarks>
public sealed class ApprovalRehydrationHostedService : IHostedService
{
    private readonly IApprovalStore _store;
    private readonly PersistentApprovalService _service;
    private readonly ILogger<ApprovalRehydrationHostedService> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="ApprovalRehydrationHostedService"/>.
    /// </summary>
    /// <param name="store">The store to query for pending approvals. Must not be <see langword="null"/>.</param>
    /// <param name="service">
    /// The persistent service into which approvals are rehydrated. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="logger">Optional structured logger.</param>
    public ApprovalRehydrationHostedService(
        IApprovalStore store,
        PersistentApprovalService service,
        ILogger<ApprovalRehydrationHostedService>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? NullLogger<ApprovalRehydrationHostedService>.Instance;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var pending = await _store.ListPendingAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Rehydrating {Count} pending approval(s) from store.",
            pending.Count);

        foreach (var item in pending)
        {
            try
            {
                _service.Rehydrate(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to rehydrate approval '{CorrelationId}'.",
                    item.CorrelationId);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Does not complete any pending TCS instances. Approvals survive the shutdown and are
    /// re-attached on next startup.
    /// </remarks>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
