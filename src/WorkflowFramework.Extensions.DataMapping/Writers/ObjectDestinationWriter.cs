using System.Reflection;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Writers;

/// <summary>
/// Writes values to CLR objects via reflection using <c>@.</c> prefixed dot-notation paths.
/// </summary>
public sealed class ObjectDestinationWriter : IDestinationWriter<object>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["@."];

    /// <inheritdoc />
    public bool CanWrite(string path) => path.StartsWith("@.", StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Write(string path, string? value, object destination)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("@.", StringComparison.Ordinal))
            return false;

        try
        {
            var parts = path[2..].Split('.');
            object? current = destination;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (current == null)
                    return false;
                var prop = current.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null)
                    return false;
                current = prop.GetValue(current);
            }

            if (current == null)
                return false;

            var finalProp = current.GetType().GetProperty(parts[^1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (finalProp == null || !finalProp.CanWrite)
                return false;

            var converted = ConvertValue(value, finalProp.PropertyType);
            finalProp.SetValue(current, converted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return value;

        return Convert.ChangeType(value, underlying);
    }
}
