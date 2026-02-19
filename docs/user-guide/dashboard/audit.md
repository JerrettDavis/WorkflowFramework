---
uid: dashboard-audit
title: Audit Trail
---

# Audit Trail

The Dashboard logs every significant action to an audit trail, providing a complete record of who did what and when.

## Tracked Actions

| Action | Description |
|---|---|
| `create` | New workflow created |
| `update` | Workflow saved/modified |
| `delete` | Workflow deleted |
| `run` | Workflow execution started |
| `template_use` | Workflow created from a template |
| `version_restore` | Workflow reverted to a previous version |

## Audit Entry Fields

Each audit entry contains:

- **Timestamp** — when the action occurred
- **Action** — the action type (see table above)
- **Workflow ID** — which workflow was affected
- **Workflow Name** — human-readable workflow name
- **User** — who performed the action
- **IP Address** — source IP for traceability
- **Details** — additional context (e.g., version number, template name)

## Viewing the Audit Log

Access the audit log from the **Audit** menu item or toolbar button.

![Audit trail](../../images/dashboard/audit-trail.png)
*The audit trail with filtering options.*

### Filtering

Filter audit entries by:

- **Action** — select one or more action types
- **Workflow** — filter by specific workflow
- **User** — filter by user
- **Date range** — start and end date

## API Endpoints

### List Audit Entries

```http
GET /api/audit?action=create&workflowId={id}&user={user}&from={date}&to={date}&skip=0&take=50
```

All query parameters are optional. Returns paginated results.

**Response:**
```json
{
  "items": [
    {
      "id": "a1b2c3",
      "timestamp": "2026-02-19T15:30:00Z",
      "action": "update",
      "workflowId": "wf-123",
      "workflowName": "Order Processing",
      "user": "jd",
      "ipAddress": "192.168.1.100",
      "details": "Version 5 saved"
    }
  ],
  "total": 142,
  "skip": 0,
  "take": 50
}
```

### Get Audit Entry by ID

```http
GET /api/audit/{id}
```
