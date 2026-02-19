# Human-in-the-Loop Tasks

The `Extensions.HumanTasks` package adds human task management to workflows — approval gates, manual review steps, escalation, and delegation.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.HumanTasks
```

## HumanTaskStep

Creates a task and waits for a human to complete it:

```csharp
using WorkflowFramework.Extensions.HumanTasks;

var inbox = new InMemoryTaskInbox();

var workflow = new WorkflowBuilder()
    .Step(new HumanTaskStep(inbox, new HumanTaskOptions
    {
        Title = "Review expense report",
        Description = "Please review the attached expense report for Q4.",
        Assignee = "manager@example.com",
        Timeout = TimeSpan.FromHours(48),
        Escalation = new EscalationRule
        {
            Timeout = TimeSpan.FromHours(24),
            EscalateTo = "director@example.com"
        }
    }))
    .Build();
```

After completion, the context contains:

- `HumanTask(Review expense report).TaskId` — the task ID
- `HumanTask(Review expense report).Outcome` — the outcome string
- `HumanTask(Review expense report).Status` — `Approved`, `Rejected`, `Completed`, etc.

## ApprovalStep

A specialized step requiring approval from one or more people:

```csharp
var workflow = new WorkflowBuilder()
    .Step(new ApprovalStep(inbox, new ApprovalOptions
    {
        Title = "Budget approval",
        Approvers = { "finance@example.com", "cfo@example.com" },
        Mode = ApprovalMode.Sequential,  // or Parallel
        Timeout = TimeSpan.FromHours(24)
    }))
    .If(ctx => (bool)ctx.Properties["Approval.Approved"]!)
        .Step(proceedStep)
    .Else()
        .Step(rejectStep)
    .EndIf()
    .Build();
```

### Approval Modes

| Mode | Behavior |
|------|----------|
| `Sequential` | Approvers are asked one at a time; first rejection stops the chain |
| `Parallel` | All approvers are asked simultaneously; all must approve |

## ITaskInbox

The core abstraction for task lifecycle management:

```csharp
public interface ITaskInbox
{
    Task<HumanTask> CreateTaskAsync(HumanTask task, CancellationToken ct = default);
    Task<HumanTask?> GetTaskAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<HumanTask>> GetTasksForAssigneeAsync(string assignee, CancellationToken ct = default);
    Task CompleteTaskAsync(string taskId, string outcome, IDictionary<string, object?>? data = null, CancellationToken ct = default);
    Task DelegateTaskAsync(string taskId, string newAssignee, CancellationToken ct = default);
    Task<HumanTask> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken ct = default);
}
```

## Completing Tasks Externally

From your API or UI, complete tasks via the inbox:

```csharp
// Approve
await inbox.CompleteTaskAsync(taskId, "approved");

// Reject with reason
await inbox.CompleteTaskAsync(taskId, "rejected", new Dictionary<string, object?>
{
    ["reason"] = "Budget exceeded policy limits"
});

// Delegate to someone else
await inbox.DelegateTaskAsync(taskId, "alternate@example.com");
```

## Task Statuses

`Pending` → `InProgress` → `Approved` | `Rejected` | `Completed` | `Escalated` | `Cancelled`

> [!NOTE]
> `InMemoryTaskInbox` is suitable for development and testing. For production, implement `ITaskInbox` against your database or task management system.
