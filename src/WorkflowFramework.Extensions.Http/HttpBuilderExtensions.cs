using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Http;

/// <summary>
/// Fluent builder extensions for HTTP steps.
/// </summary>
public static class HttpBuilderExtensions
{
    /// <summary>Adds an HTTP GET step.</summary>
    public static IWorkflowBuilder HttpGet(this IWorkflowBuilder builder, string url, string? name = null)
    {
        return builder.Step(new HttpStep(new HttpStepOptions { Url = url, Method = HttpMethod.Get, Name = name ?? $"HttpGet({url})" }));
    }

    /// <summary>Adds an HTTP POST step.</summary>
    public static IWorkflowBuilder HttpPost(this IWorkflowBuilder builder, string url, string? body = null, string? name = null)
    {
        return builder.Step(new HttpStep(new HttpStepOptions { Url = url, Method = HttpMethod.Post, Body = body, Name = name ?? $"HttpPost({url})" }));
    }

    /// <summary>Adds an HTTP PUT step.</summary>
    public static IWorkflowBuilder HttpPut(this IWorkflowBuilder builder, string url, string? body = null, string? name = null)
    {
        return builder.Step(new HttpStep(new HttpStepOptions { Url = url, Method = HttpMethod.Put, Body = body, Name = name ?? $"HttpPut({url})" }));
    }

    /// <summary>Adds an HTTP DELETE step.</summary>
    public static IWorkflowBuilder HttpDelete(this IWorkflowBuilder builder, string url, string? name = null)
    {
        return builder.Step(new HttpStep(new HttpStepOptions { Url = url, Method = HttpMethod.Delete, Name = name ?? $"HttpDelete({url})" }));
    }
}
