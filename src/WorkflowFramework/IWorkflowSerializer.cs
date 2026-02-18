namespace WorkflowFramework;

/// <summary>
/// Abstraction for serializing/deserializing workflow context and data.
/// </summary>
public interface IWorkflowSerializer
{
    /// <summary>Serializes an object to a string.</summary>
    string Serialize<T>(T value);

    /// <summary>Deserializes a string to an object.</summary>
    T? Deserialize<T>(string data);
}
