namespace WorkflowFramework.Extensions.DataMapping.Internal;

internal static class DictHelper
{
    internal static TValue? TryGet<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? dict, TKey key)
        where TKey : notnull
    {
        if (dict != null && dict.TryGetValue(key, out var value))
            return value;
        return default;
    }
}
