---
uid: dashboard-api-reference
title: API Reference
---

# API Reference

The Dashboard API (`Dashboard.Api`) exposes RESTful endpoints for all dashboard operations. All endpoints are registered via `MapWorkflowDashboardApi()`.

## Base URL

When running via the Aspire AppHost, the API base URL is resolved through service discovery (typically `https://localhost:5000`).

## Endpoint Groups

### Workflows

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/workflows` | List all workflows (supports `?search=` and `?tags=`) |
| `GET` | `/api/workflows/{id}` | Get workflow by ID |
| `POST` | `/api/workflows` | Create a new workflow |
| `PUT` | `/api/workflows/{id}` | Update a workflow (creates new version) |
| `DELETE` | `/api/workflows/{id}` | Delete a workflow |
| `POST` | `/api/workflows/{id}/duplicate` | Duplicate a workflow |
| `POST` | `/api/workflows/import` | Import from JSON/YAML |
| `GET` | `/api/workflows/{id}/export?format=json` | Export as JSON or YAML |

**Create/Update request body:**
```json
{
  "name": "Order Processing",
  "description": "Processes incoming orders",
  "tags": ["production", "orders"],
  "steps": [
    {
      "id": "step-1",
      "type": "action",
      "name": "ValidateOrder",
      "config": { "actionType": "validateOrder" },
      "position": { "x": 100, "y": 200 }
    }
  ],
  "connections": [
    { "from": "step-1", "to": "step-2" }
  ]
}
```

### Steps (Catalog)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/steps` | List all available step types |
| `GET` | `/api/steps/{type}` | Get step type details including config schema |
| `GET` | `/api/steps/categories` | List step categories |

**Step type response:**
```json
{
  "type": "conditional",
  "category": "Core",
  "description": "Branches execution based on a condition",
  "schema": {
    "properties": {
      "name": { "type": "text", "required": true },
      "condition": { "type": "text", "required": true }
    }
  }
}
```

### Runs

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/workflows/{id}/runs` | List runs for a workflow |
| `GET` | `/api/runs/{id}` | Get run details |
| `POST` | `/api/workflows/{id}/run` | Start a new run |
| `POST` | `/api/runs/{id}/cancel` | Cancel a running workflow |
| `GET` | `/api/runs/{id}/logs` | Get execution log entries |

**Run response:**
```json
{
  "id": "run-abc",
  "workflowId": "wf-123",
  "status": "Completed",
  "startedAt": "2026-02-19T15:00:00Z",
  "completedAt": "2026-02-19T15:00:05Z",
  "duration": "00:00:05",
  "steps": [
    {
      "stepName": "ValidateOrder",
      "status": "Completed",
      "startedAt": "2026-02-19T15:00:00Z",
      "completedAt": "2026-02-19T15:00:01Z"
    }
  ]
}
```

### Plugins

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/plugins` | List registered plugins |
| `GET` | `/api/plugins/{id}` | Get plugin details |

### Connectors

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/connectors` | List available connectors |
| `GET` | `/api/connectors/{id}` | Get connector details |

See also: [Connectors](../connectors.md).

### Templates

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/templates` | List all templates (supports `?category=` and `?difficulty=`) |
| `GET` | `/api/templates/{id}` | Get template details |
| `POST` | `/api/templates/{id}/use` | Create workflow from template |

### Versioning

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/workflows/{id}/versions` | List all versions |
| `GET` | `/api/workflows/{id}/versions/{version}` | Get specific version |
| `POST` | `/api/workflows/{id}/versions/{version}/restore` | Restore a version |
| `GET` | `/api/workflows/{id}/versions/{v1}/diff/{v2}` | Diff two versions |

**Diff response:**
```json
{
  "fromVersion": 3,
  "toVersion": 5,
  "addedSteps": ["SendEmail"],
  "removedSteps": ["OldValidation"],
  "renamedSteps": [
    { "from": "Check", "to": "ValidateInput" }
  ],
  "addedConnections": 2,
  "removedConnections": 1
}
```

### Audit

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/audit` | List audit entries (supports filtering — see [Audit Trail](audit.md)) |
| `GET` | `/api/audit/{id}` | Get audit entry by ID |

### Tags

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/tags` | List all tags with usage counts |

### Validation

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/workflows/{id}/validate` | Validate a saved workflow |
| `POST` | `/api/workflows/validate` | Validate an inline workflow definition |

See [Validation](validation.md) for response format details.

## SignalR Hub

The `WorkflowExecutionHub` at `/hubs/execution` pushes real-time events during workflow runs:

| Event | Payload |
|---|---|
| `StepStarted` | `{ runId, stepName, timestamp }` |
| `StepCompleted` | `{ runId, stepName, timestamp, duration }` |
| `StepFailed` | `{ runId, stepName, timestamp, error }` |
| `WorkflowCompleted` | `{ runId, timestamp, duration }` |
| `WorkflowFailed` | `{ runId, timestamp, error }` |

Connect from JavaScript:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/execution")
    .withAutomaticReconnect()
    .build();

connection.on("StepCompleted", (event) => {
    console.log(`${event.stepName} completed in ${event.duration}`);
});

await connection.start();
```
