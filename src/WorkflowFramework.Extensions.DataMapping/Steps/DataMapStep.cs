using System.Text.Json;
using System.Text.Json.Nodes;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Steps;

/// <summary>
/// Workflow step that applies a <see cref="DataMappingProfile"/> to transform data in the workflow context.
/// Reads from the <c>__Source</c> property and writes to <c>__Destination</c>.
/// </summary>
public sealed class DataMapStep : StepBase
{
    /// <summary>
    /// Property key for source data.
    /// </summary>
    public const string SourceKey = "__Source";

    /// <summary>
    /// Property key for destination data.
    /// </summary>
    public const string DestinationKey = "__Destination";

    private readonly IDataMapper _mapper;
    private readonly DataMappingProfile _profile;

    /// <summary>
    /// Initializes a new instance of <see cref="DataMapStep"/>.
    /// </summary>
    /// <param name="mapper">The data mapper.</param>
    /// <param name="profile">The mapping profile to apply.</param>
    public DataMapStep(IDataMapper mapper, DataMappingProfile profile)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IWorkflowContext context)
    {
        if (!context.Properties.TryGetValue(SourceKey, out var sourceObj) || sourceObj == null)
            throw new InvalidOperationException($"No source data found in context property '{SourceKey}'.");

        // Create destination if not present
        if (!context.Properties.TryGetValue(DestinationKey, out var destObj) || destObj == null)
        {
            destObj = new JsonObject();
            context.Properties[DestinationKey] = destObj;
        }

        // Handle JSON string source by parsing to JsonElement
        if (sourceObj is string jsonString)
        {
            using var doc = JsonDocument.Parse(jsonString);
            var element = doc.RootElement.Clone();
            var result = await _mapper.MapAsync(_profile, element, (JsonObject)destObj).ConfigureAwait(false);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Data mapping failed: {string.Join("; ", result.Errors)}");
            return;
        }

        // For other source types, use dynamic dispatch
        var mapMethod = typeof(IDataMapper).GetMethod(nameof(IDataMapper.MapAsync))!;
        var generic = mapMethod.MakeGenericMethod(sourceObj.GetType(), destObj.GetType());
        var task = (Task<DataMappingResult>)generic.Invoke(_mapper, new[] { _profile, sourceObj, destObj, CancellationToken.None })!;
        var res = await task.ConfigureAwait(false);
        if (!res.IsSuccess)
            throw new InvalidOperationException($"Data mapping failed: {string.Join("; ", res.Errors)}");
    }
}
