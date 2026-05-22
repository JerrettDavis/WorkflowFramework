using System.CommandLine;
using System.Text;

namespace WorkflowFramework.Extensions.Approvals.Cli.Commands;

/// <summary>
/// Builds the <c>approvals</c> subcommand tree for the <c>wf</c> CLI tool.
/// Provides <c>list</c>, <c>show</c>, <c>approve</c>, and <c>reject</c> subcommands backed
/// by an <see cref="IApprovalStore"/> and <see cref="PersistentApprovalService"/>.
/// </summary>
public static class WfApprovalsCommand
{
    /// <summary>
    /// Constructs the fully-wired <c>approvals</c> <see cref="Command"/> with all subcommands
    /// registered.
    /// </summary>
    /// <param name="store">The approval store from which pending approvals are read.</param>
    /// <param name="persistent">
    /// The persistent approval service used to submit external votes via
    /// <see cref="PersistentApprovalService.ResolveExternalAsync"/>.
    /// </param>
    /// <param name="console">
    /// The console abstraction used for output. Inject a test double in unit tests.
    /// </param>
    /// <returns>A <see cref="Command"/> ready to be added to a root command.</returns>
    public static Command Build(
        IApprovalStore store,
        PersistentApprovalService persistent,
        IConsole console)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (persistent is null) throw new ArgumentNullException(nameof(persistent));
        if (console is null) throw new ArgumentNullException(nameof(console));

        var approvalsCommand = new Command("approvals", "Manage pending workflow approvals.");

        approvalsCommand.AddCommand(BuildListCommand(store, console));
        approvalsCommand.AddCommand(BuildShowCommand(store, console));
        approvalsCommand.AddCommand(BuildApproveCommand(store, persistent, console));
        approvalsCommand.AddCommand(BuildRejectCommand(store, persistent, console));

        return approvalsCommand;
    }

    // -------------------------------------------------------------------------
    // list
    // -------------------------------------------------------------------------

    private static Command BuildListCommand(IApprovalStore store, IConsole console)
    {
        var cmd = new Command("list", "List all pending approvals.");

        cmd.SetHandler(async (ctx) =>
        {
            var ct = ctx.GetCancellationToken();
            var pending = await store.ListPendingAsync(ct).ConfigureAwait(false);

            if (pending.Count == 0)
            {
                console.WriteLine("No pending approvals.");
                return;
            }

            // Header
            console.WriteLine(BuildSeparator());
            console.WriteLine(
                $"{"CorrelationId",-36}  {"Title",-30}  {"Required",8}  {"Votes",8}  {"Deadline",-25}");
            console.WriteLine(BuildSeparator());

            foreach (var p in pending)
            {
                var voteCount = p.Votes.Count;
                var required = p.Request.RequiredApprovers;
                console.WriteLine(
                    $"{p.CorrelationId,-36}  {Truncate(p.Request.Title, 30),-30}  {required,8}  {voteCount + "/" + required,8}  {p.DeadlineAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
            }

            console.WriteLine(BuildSeparator());
        });

        return cmd;
    }

    // -------------------------------------------------------------------------
    // show
    // -------------------------------------------------------------------------

    private static Command BuildShowCommand(IApprovalStore store, IConsole console)
    {
        var idArg = new Argument<string>("correlationId", "The correlation ID of the approval to show.");
        var cmd = new Command("show", "Show full details of a pending approval.")
        {
            idArg
        };

        cmd.SetHandler(async (ctx) =>
        {
            var correlationId = ctx.ParseResult.GetValueForArgument(idArg);
            var ct = ctx.GetCancellationToken();

            var pending = await store.LoadAsync(correlationId, ct).ConfigureAwait(false);
            if (pending is null)
            {
                console.WriteLine($"Approval '{correlationId}' not found.");
                ctx.ExitCode = 2;
                return;
            }

            console.WriteLine(BuildSeparator());
            console.WriteLine($"Approval: {pending.CorrelationId}");
            console.WriteLine($"Title   : {pending.Request.Title}");

            if (!string.IsNullOrWhiteSpace(pending.Request.Description))
                console.WriteLine($"Desc    : {pending.Request.Description}");

            console.WriteLine($"Channel : {pending.PrimaryChannel}");
            console.WriteLine($"Created : {pending.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
            console.WriteLine($"Deadline: {pending.DeadlineAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
            console.WriteLine($"Required: {pending.Request.RequiredApprovers}");

            if (pending.Request.Context is { Count: > 0 })
            {
                console.WriteLine("Context :");
                foreach (var kv in pending.Request.Context)
                    console.WriteLine($"  {kv.Key} = {kv.Value}");
            }

            if (pending.Request.AllowedRoles is { Count: > 0 })
                console.WriteLine($"Roles   : {string.Join(", ", pending.Request.AllowedRoles)}");

            console.WriteLine(BuildSeparator());

            if (pending.Votes.Count == 0)
            {
                console.WriteLine("Votes   : (no votes)");
            }
            else
            {
                console.WriteLine("Votes   :");
                foreach (var v in pending.Votes)
                {
                    var decision = v.Approved ? "APPROVED" : "REJECTED";
                    var name = v.ApproverDisplayName is not null ? $" ({v.ApproverDisplayName})" : string.Empty;
                    var comment = v.Comment is not null ? $" -- {v.Comment}" : string.Empty;
                    console.WriteLine(
                        $"  [{decision}] {v.ApproverId}{name} at {v.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{comment}");
                }
            }

            console.WriteLine(BuildSeparator());
        });

        return cmd;
    }

    // -------------------------------------------------------------------------
    // approve
    // -------------------------------------------------------------------------

    private static Command BuildApproveCommand(
        IApprovalStore store,
        PersistentApprovalService persistent,
        IConsole console)
    {
        var idArg = new Argument<string>("correlationId", "The correlation ID of the approval to approve.");
        var byOption = new Option<string>("--by", "The approver ID (user ID, email, or service account).") { IsRequired = true };
        var commentOption = new Option<string?>("--comment", "Optional comment explaining the decision.");
        var nameOption = new Option<string?>("--name", "Optional human-readable display name for the approver.");

        var cmd = new Command("approve", "Approve a pending approval request.")
        {
            idArg,
            byOption,
            commentOption,
            nameOption
        };

        cmd.SetHandler(async (ctx) =>
        {
            var correlationId = ctx.ParseResult.GetValueForArgument(idArg);
            var approverId = ctx.ParseResult.GetValueForOption(byOption)!;
            var comment = ctx.ParseResult.GetValueForOption(commentOption);
            var displayName = ctx.ParseResult.GetValueForOption(nameOption);
            var ct = ctx.GetCancellationToken();

            ctx.ExitCode = await HandleVoteAsync(
                store, persistent, console,
                correlationId, approverId, displayName, comment,
                approved: true, ct).ConfigureAwait(false);
        });

        return cmd;
    }

    // -------------------------------------------------------------------------
    // reject
    // -------------------------------------------------------------------------

    private static Command BuildRejectCommand(
        IApprovalStore store,
        PersistentApprovalService persistent,
        IConsole console)
    {
        var idArg = new Argument<string>("correlationId", "The correlation ID of the approval to reject.");
        var byOption = new Option<string>("--by", "The approver ID (user ID, email, or service account).") { IsRequired = true };
        var commentOption = new Option<string?>("--comment", "Optional comment explaining the rejection.");
        var nameOption = new Option<string?>("--name", "Optional human-readable display name for the approver.");

        var cmd = new Command("reject", "Reject a pending approval request.")
        {
            idArg,
            byOption,
            commentOption,
            nameOption
        };

        cmd.SetHandler(async (ctx) =>
        {
            var correlationId = ctx.ParseResult.GetValueForArgument(idArg);
            var approverId = ctx.ParseResult.GetValueForOption(byOption)!;
            var comment = ctx.ParseResult.GetValueForOption(commentOption) ?? "Rejected by CLI";
            var displayName = ctx.ParseResult.GetValueForOption(nameOption);
            var ct = ctx.GetCancellationToken();

            ctx.ExitCode = await HandleVoteAsync(
                store, persistent, console,
                correlationId, approverId, displayName, comment,
                approved: false, ct).ConfigureAwait(false);
        });

        return cmd;
    }

    // -------------------------------------------------------------------------
    // Shared vote handler
    // -------------------------------------------------------------------------

    private static async Task<int> HandleVoteAsync(
        IApprovalStore store,
        PersistentApprovalService persistent,
        IConsole console,
        string correlationId,
        string approverId,
        string? displayName,
        string? comment,
        bool approved,
        CancellationToken ct)
    {
        var existing = await store.LoadAsync(correlationId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            console.WriteLine($"Approval '{correlationId}' not found.");
            return 2;
        }

        var record = new ApprovalRecord(
            ApproverId: approverId,
            ApproverDisplayName: displayName,
            Approved: approved,
            Comment: comment,
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "cli");

        try
        {
            await persistent.ResolveExternalAsync(correlationId, record, ct).ConfigureAwait(false);

            var action = approved ? "Approved" : "Rejected";
            console.WriteLine($"{action} by {approverId}.");
            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            console.WriteLine($"Unauthorized: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            console.WriteLine($"Error ({ex.GetType().Name}): {ex.Message}");
            return 1;
        }
    }

    // -------------------------------------------------------------------------
    // Formatting helpers
    // -------------------------------------------------------------------------

    private static string BuildSeparator() => new('-', 100);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
}
