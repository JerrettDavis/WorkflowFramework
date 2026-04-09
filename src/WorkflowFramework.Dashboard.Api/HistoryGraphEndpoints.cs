using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Persistence;

namespace WorkflowFramework.Dashboard.Api;

/// <summary>
/// Minimal API endpoints for querying the workflow history graph.
/// </summary>
public static class HistoryGraphEndpoints
{
    public static IEndpointRouteBuilder MapHistoryGraphEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/history").WithTags("History");

        // GET /api/history/nodes?query=&limit=20 — search nodes by name
        group.MapGet("/nodes", async (
            string? query,
            int limit,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            limit = Math.Clamp(limit == 0 ? 20 : limit, 1, 200);

            var q = db.HistoryNodes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
                q = q.Where(n => n.Name.Contains(query));

            var nodes = await q
                .OrderByDescending(n => n.LastSeenAt)
                .Take(limit)
                .Select(n => new
                {
                    n.Fingerprint,
                    n.Name,
                    n.Kind,
                    n.Target,
                    n.ExecutionCount,
                    n.SuccessCount,
                    n.FailureCount,
                    n.AverageDurationTicks,
                    n.FirstSeenAt,
                    n.LastSeenAt,
                })
                .ToListAsync(ct);

            return Results.Ok(nodes);
        }).WithName("SearchHistoryNodes");

        // GET /api/history/nodes/{fingerprint} — get single node with its edges
        group.MapGet("/nodes/{fingerprint}", async (
            string fingerprint,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            var node = await db.HistoryNodes
                .AsNoTracking()
                .Where(n => n.Fingerprint == fingerprint)
                .Select(n => new
                {
                    n.Fingerprint,
                    n.Name,
                    n.Kind,
                    n.Target,
                    n.ExecutionCount,
                    n.SuccessCount,
                    n.FailureCount,
                    n.AverageDurationTicks,
                    n.FirstSeenAt,
                    n.LastSeenAt,
                    n.WorkflowNamesJson,
                })
                .FirstOrDefaultAsync(ct);

            return node is null ? Results.NotFound() : Results.Ok(node);
        }).WithName("GetHistoryNode");

        // GET /api/history/edges?workflow= — edges associated with a workflow name
        group.MapGet("/edges", async (
            string? workflow,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            var q = db.HistoryEdges.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(workflow))
            {
                var searchTerm = $"\"{workflow}\"";
                q = q.Where(e => e.WorkflowNamesJson.Contains(searchTerm));
            }

            var edges = await q
                .OrderByDescending(e => e.Weight)
                .Select(e => new
                {
                    e.Id,
                    e.SourceFingerprint,
                    e.TargetFingerprint,
                    e.Kind,
                    e.Weight,
                    e.AverageTransitionTimeTicks,
                    e.FirstSeenAt,
                    e.LastSeenAt,
                })
                .ToListAsync(ct);

            return Results.Ok(edges);
        }).WithName("GetHistoryEdges");

        // GET /api/history/edges/{fingerprint}/outgoing?topN=10 — top outgoing edges from a node
        group.MapGet("/edges/{fingerprint}/outgoing", async (
            string fingerprint,
            int topN,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            topN = Math.Clamp(topN == 0 ? 10 : topN, 1, 100);

            var edges = await db.HistoryEdges
                .AsNoTracking()
                .Where(e => e.SourceFingerprint == fingerprint)
                .OrderByDescending(e => e.Weight)
                .Take(topN)
                .Select(e => new
                {
                    e.Id,
                    e.SourceFingerprint,
                    e.TargetFingerprint,
                    e.Kind,
                    e.Weight,
                    e.AverageTransitionTimeTicks,
                    e.FirstSeenAt,
                    e.LastSeenAt,
                })
                .ToListAsync(ct);

            return Results.Ok(edges);
        }).WithName("GetTopOutgoingEdges");

        // GET /api/history/mermaid?maxNodes=50&minEdgeWeight=1
        group.MapGet("/mermaid", async (
            int maxNodes,
            long minEdgeWeight,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            maxNodes = Math.Clamp(maxNodes == 0 ? 50 : maxNodes, 1, 500);
            if (minEdgeWeight < 1) minEdgeWeight = 1;

            var nodes = await db.HistoryNodes
                .AsNoTracking()
                .OrderByDescending(n => n.ExecutionCount)
                .Take(maxNodes)
                .Select(n => new { n.Fingerprint, n.Name, n.Kind, n.ExecutionCount, n.SuccessCount })
                .ToListAsync(ct);

            var fingerprints = nodes.Select(n => n.Fingerprint).ToHashSet();

            var edges = await db.HistoryEdges
                .AsNoTracking()
                .Where(e => e.Weight >= minEdgeWeight &&
                            fingerprints.Contains(e.SourceFingerprint) &&
                            fingerprints.Contains(e.TargetFingerprint))
                .Select(e => new { e.SourceFingerprint, e.TargetFingerprint, e.Kind, e.Weight })
                .ToListAsync(ct);

            var mermaid = BuildMermaid(nodes.Select(n => new NodeSummary(n.Fingerprint, n.Name, n.Kind, n.ExecutionCount, n.SuccessCount)),
                                       edges.Select(e => new EdgeSummary(e.SourceFingerprint, e.TargetFingerprint, e.Kind, e.Weight)));

            return Results.Text(mermaid, "text/plain");
        }).WithName("GetHistoryMermaid");

        // GET /api/history/mermaid/{fingerprint}?maxDepth=5
        group.MapGet("/mermaid/{fingerprint}", async (
            string fingerprint,
            int maxDepth,
            DashboardDbContext db,
            CancellationToken ct) =>
        {
            maxDepth = Math.Clamp(maxDepth == 0 ? 5 : maxDepth, 1, 20);

            // BFS from the given fingerprint to collect reachable fingerprints
            var included = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<(string fp, int depth)>();
            queue.Enqueue((fingerprint, 0));
            included.Add(fingerprint);

            // Load all edges for BFS traversal (bounded by reasonable depth)
            var allEdges = await db.HistoryEdges
                .AsNoTracking()
                .Select(e => new { e.SourceFingerprint, e.TargetFingerprint, e.Kind, e.Weight })
                .ToListAsync(ct);

            var edgeLookup = allEdges.GroupBy(e => e.SourceFingerprint)
                .ToDictionary(g => g.Key, g => g.ToList());

            while (queue.Count > 0)
            {
                var (fp, depth) = queue.Dequeue();
                if (depth >= maxDepth)
                    continue;
                if (!edgeLookup.TryGetValue(fp, out var outgoing))
                    continue;
                foreach (var e in outgoing)
                    if (included.Add(e.TargetFingerprint))
                        queue.Enqueue((e.TargetFingerprint, depth + 1));
            }

            var nodes = await db.HistoryNodes
                .AsNoTracking()
                .Where(n => included.Contains(n.Fingerprint))
                .Select(n => new { n.Fingerprint, n.Name, n.Kind, n.ExecutionCount, n.SuccessCount })
                .ToListAsync(ct);

            if (nodes.Count == 0)
                return Results.NotFound();

            var subEdges = allEdges
                .Where(e => included.Contains(e.SourceFingerprint) && included.Contains(e.TargetFingerprint));

            var mermaid = BuildMermaid(nodes.Select(n => new NodeSummary(n.Fingerprint, n.Name, n.Kind, n.ExecutionCount, n.SuccessCount)),
                                       subEdges.Select(e => new EdgeSummary(e.SourceFingerprint, e.TargetFingerprint, e.Kind, e.Weight)));

            return Results.Text(mermaid, "text/plain");
        }).WithName("GetHistoryMermaidSubgraph");

        return endpoints;
    }

    // ── Mermaid renderer ──────────────────────────────────────────────────────

    private sealed record NodeSummary(string Fingerprint, string Name, string Kind, long ExecutionCount, long SuccessCount);
    private sealed record EdgeSummary(string SourceFingerprint, string TargetFingerprint, string Kind, long Weight);

    private static string BuildMermaid(IEnumerable<NodeSummary> nodes, IEnumerable<EdgeSummary> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        var nodeList = nodes.ToList();

        foreach (var node in nodeList)
        {
            var id = $"N_{node.Fingerprint}";
            var label = EscapeMermaidLabel(node.Name);
            var shape = node.Kind switch
            {
                "Conditional" => $"{id}{{{{{label}}}}}",
                "Loop"        => $"{id}[/{label}\\]",
                "Nested"      => $"{id}[[{label}]]",
                _             => $"{id}[\"{label}\"]",
            };
            sb.AppendLine($"    {shape}");
        }

        foreach (var edge in edges)
        {
            var src = $"N_{edge.SourceFingerprint}";
            var tgt = $"N_{edge.TargetFingerprint}";
            var label = $"w:{edge.Weight}";
            if (string.Equals(edge.Kind, "SubStep", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"    {src} -. \"{label}\" .-> {tgt}");
            else
                sb.AppendLine($"    {src} -- \"{label}\" --> {tgt}");
        }

        foreach (var node in nodeList)
        {
            var id = $"N_{node.Fingerprint}";
            var rate = node.ExecutionCount > 0 ? (double)node.SuccessCount / node.ExecutionCount : 0.0;
            var style = rate >= 0.9
                ? $"style {id} fill:#2d6a2d,color:#fff"
                : rate >= 0.5
                    ? $"style {id} fill:#c8a600,color:#000"
                    : $"style {id} fill:#8b1a1a,color:#fff";
            sb.AppendLine($"    {style}");
        }

        return sb.ToString();
    }

    private static string EscapeMermaidLabel(string name) =>
        name.Replace("\"", "'")
            .Replace("[", "(")
            .Replace("]", ")")
            .Replace("{", "(")
            .Replace("}", ")");
}
