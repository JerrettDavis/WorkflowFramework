#if !NETSTANDARD2_0
namespace WorkflowFramework.Triggers;

/// <summary>
/// Creates trigger source instances from trigger definitions.
/// </summary>
public interface ITriggerSourceFactory
{
    /// <summary>Creates an <see cref="ITriggerSource"/> for the given definition.</summary>
    ITriggerSource Create(TriggerDefinition definition);

    /// <summary>Returns metadata about all registered trigger types.</summary>
    IReadOnlyList<TriggerTypeInfo> GetAvailableTypes();
}

#endif
