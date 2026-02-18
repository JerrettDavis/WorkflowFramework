using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.EntityFramework;

/// <summary>
/// Entity Framework Core implementation of <see cref="IWorkflowStateStore"/>.
/// </summary>
public sealed class EfCoreWorkflowStateStore : IWorkflowStateStore
{
    private readonly WorkflowDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="EfCoreWorkflowStateStore"/>.
    /// </summary>
    /// <param name="context">The database context.</param>
    public EfCoreWorkflowStateStore(WorkflowDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(string workflowId, WorkflowState state, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WorkflowStates
            .FindAsync(new object[] { workflowId }, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            entity = new WorkflowStateEntity { WorkflowId = workflowId };
            _context.WorkflowStates.Add(entity);
        }

        entity.CorrelationId = state.CorrelationId;
        entity.WorkflowName = state.WorkflowName;
        entity.LastCompletedStepIndex = state.LastCompletedStepIndex;
        entity.Status = (int)state.Status;
        entity.PropertiesJson = JsonSerializer.Serialize(state.Properties, JsonOptions);
        entity.SerializedData = state.SerializedData;
        entity.Timestamp = state.Timestamp;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowState?> LoadCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WorkflowStates
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorkflowId == workflowId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null) return null;

        return new WorkflowState
        {
            WorkflowId = entity.WorkflowId,
            CorrelationId = entity.CorrelationId,
            WorkflowName = entity.WorkflowName,
            LastCompletedStepIndex = entity.LastCompletedStepIndex,
            Status = (WorkflowStatus)entity.Status,
            Properties = string.IsNullOrEmpty(entity.PropertiesJson)
                ? new Dictionary<string, object?>()
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.PropertiesJson) ?? new(),
            SerializedData = entity.SerializedData,
            Timestamp = entity.Timestamp
        };
    }

    /// <inheritdoc />
    public async Task DeleteCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WorkflowStates
            .FindAsync(new object[] { workflowId }, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null)
        {
            _context.WorkflowStates.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
