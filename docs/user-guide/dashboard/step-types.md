---
uid: dashboard-step-types
title: Step Types Reference
---

# Step Types Reference

The Dashboard supports **30 built-in step types** organized into seven categories. Each step type has a specific configuration schema surfaced in the [Properties Panel](designer.md#properties-panel).

> [!TIP]
> For the code-first equivalents of these step types, see the [Fluent Builder](../fluent-builder.md) and related documentation.

## Core

Fundamental workflow control flow steps.

### action

A generic execution step that runs a named action.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `actionType` | text | ✅ | The action to execute |
| `parameters` | text | | JSON parameters passed to the action |

### conditional

Branches execution based on a boolean expression. Requires `then` and optionally `else` child connections.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `condition` | text | ✅ | Expression that evaluates to true/false |

> [!WARNING]
> The [validator](validation.md) requires conditional steps to have at least a `then` branch connected.

### parallel

Executes multiple child branches concurrently. Connect child steps to the parallel node's output handles.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `maxConcurrency` | number | | Maximum parallel branches (0 = unlimited) |

### loop

Repeats a body block while a condition is true.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `condition` | text | ✅ | Loop continuation condition |
| `maxIterations` | number | | Safety limit on iterations |

### forEach

Iterates over a collection, executing the body for each item.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `collection` | text | ✅ | Expression resolving to an enumerable |
| `itemVariable` | text | | Variable name for current item (default: `item`) |

### tryCatch

Wraps a body in error handling. Has `body` and `catch` output connections.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `catchAction` | text | | Action to run on error |
| `retryOnCatch` | bool | | Whether to retry the body after catch |

### retry

Retries a child step on failure with configurable attempts.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `maxAttempts` | number | ✅ | Maximum retry attempts (must be ≥ 1) |
| `delayMs` | number | | Delay between retries in milliseconds |
| `backoffMultiplier` | number | | Exponential backoff multiplier |

See also: [Resilience (Polly)](../resilience.md) for policy-based resilience.

### timeout

Wraps a child step with a time limit.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `seconds` | number | ✅ | Timeout duration in seconds |

> [!NOTE]
> The validator ensures `seconds` is provided and that the timeout step has an inner child step connected.

### delay

Pauses execution for a specified duration.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `duration` | text | ✅ | Duration (e.g., `"00:00:30"` for 30 seconds) |

### saga

Implements the saga pattern with compensating actions for distributed transactions.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `compensationAction` | text | ✅ | Action to run if saga needs to roll back |

## Integration

Steps implementing [Enterprise Integration Patterns](../integration-patterns.md).

### contentRouter

Routes messages to different branches based on message content.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `routeExpression` | text | ✅ | Expression that determines the route |
| `routes` | text | ✅ | JSON map of route keys to target step names |

### messageFilter

Filters messages based on a predicate, discarding non-matching messages.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `filterExpression` | text | ✅ | Boolean expression; messages matching pass through |

### splitter

Splits a single message into multiple parts for individual processing.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `splitExpression` | text | ✅ | Expression that returns a collection |

### aggregator

Collects multiple messages and combines them into one.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `correlationExpression` | text | ✅ | Groups related messages |
| `completionCondition` | text | ✅ | When to emit the aggregated message |
| `aggregationStrategy` | select | | How to combine messages (default: `list`) |

### recipientList

Sends a message to a dynamically determined list of recipients.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `recipientsExpression` | text | ✅ | Expression returning a list of target steps |

### wiretap

Copies messages to a secondary channel for monitoring without affecting the main flow.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `tapTarget` | text | ✅ | Step or channel to receive copied messages |

### deadLetterChannel

Routes failed messages to a dead-letter destination for later analysis.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `deadLetterTarget` | text | ✅ | Destination for failed messages |
| `maxRetries` | number | | Retries before sending to dead letter |

### enricher

Augments messages with additional data from an external source.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `enrichSource` | text | ✅ | Data source expression or endpoint |
| `mergeStrategy` | select | | How to merge enriched data (default: `merge`) |

### normalizer

Transforms messages from different formats into a common canonical format.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `targetSchema` | text | ✅ | Target canonical schema |

### claimCheck

Stores large payloads externally and replaces them with a claim token.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `operation` | select | ✅ | `store` or `retrieve` |
| `storeName` | text | | Name of the claim check store |

### dynamicRouter

Routes messages dynamically based on runtime conditions, recalculating at each step.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `routeExpression` | text | ✅ | Expression evaluated at each routing point |

### routingSlip

Processes a message through a dynamically determined sequence of steps.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `slipExpression` | text | ✅ | Expression returning an ordered list of steps |

## AI/Agents

Steps for AI and autonomous agent workflows. See also: [AI Agents](../ai-agents.md) and [Agentic Workflows](../agents.md).

### llmCall

Invokes a large language model with a prompt.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `model` | text | ✅ | Model identifier (e.g., `gpt-4o`, `claude-sonnet`) |
| `prompt` | text | ✅ | Prompt template (supports variable interpolation) |
| `temperature` | number | | Sampling temperature (0.0–2.0) |
| `maxTokens` | number | | Maximum response tokens |

### agentDecision

An autonomous agent that makes decisions based on context and available tools.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `agentRole` | text | ✅ | Agent's role description |
| `availableTools` | text | | Comma-separated list of tool names |
| `decisionStrategy` | select | | `greedy`, `deliberate`, or `consensus` |

### agentLoop

Runs an agent in a loop until a goal is achieved or a limit is reached.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `goal` | text | ✅ | The objective the agent should achieve |
| `maxIterations` | number | ✅ | Maximum loop iterations |
| `exitCondition` | text | | Expression that terminates the loop early |

### toolCall

Invokes a specific tool or function as part of an agent workflow.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `toolName` | text | ✅ | Name of the tool to invoke |
| `parameters` | text | | JSON parameters for the tool |

## Data

Steps for data transformation and validation. See also: [Data Mapping](../data-mapping.md).

### dataMap

Transforms data from one shape to another using mapping expressions.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `mappings` | text | ✅ | JSON mapping definition (source → target paths) |

### validate

Validates data against custom rules.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `rules` | text | ✅ | JSON validation rules |
| `failOnError` | bool | | Whether to throw on validation failure (default: true) |

### schemaValidate

Validates data against a JSON Schema.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `schema` | text | ✅ | JSON Schema definition or reference |
| `failOnError` | bool | | Whether to throw on validation failure (default: true) |

## HTTP

Steps for HTTP communication.

### httpRequest

Makes an HTTP request to an external endpoint.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `url` | text | ✅ | Request URL |
| `method` | select | ✅ | HTTP method (GET, POST, PUT, DELETE, PATCH) |
| `headers` | text | | JSON headers object |
| `body` | text | | Request body |
| `timeoutSeconds` | number | | Request timeout |

### webhook

Registers a webhook endpoint that triggers workflow execution when called.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `path` | text | ✅ | Webhook URL path |
| `method` | select | | Accepted HTTP method (default: POST) |
| `secret` | text | | Shared secret for signature validation |

## Events

Steps for event-driven architectures. See also: [Event-Driven Workflows](../events.md).

### publishEvent

Publishes an event to the event bus.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `eventType` | text | ✅ | Event type identifier |
| `payload` | text | | Event payload expression |

### subscribeEvent

Waits for a specific event before continuing execution.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `eventType` | text | ✅ | Event type to subscribe to |
| `filter` | text | | Expression to filter events |
| `timeoutSeconds` | number | | Maximum wait time |

## Human Tasks

Steps requiring human interaction. See also: [Human-in-the-Loop](../human-tasks.md).

### humanApproval

Pauses workflow execution until a human approves or rejects.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `approver` | text | ✅ | User or group who can approve |
| `message` | text | | Approval request message |
| `timeoutHours` | number | | Auto-reject after timeout |

### humanTask

Assigns a task to a human and waits for completion.

| Property | Type | Required | Description |
|---|---|---|---|
| `name` | text | ✅ | Unique step name |
| `assignee` | text | ✅ | User or group assigned to the task |
| `taskDescription` | text | ✅ | Description of what needs to be done |
| `formSchema` | text | | JSON schema for the task completion form |
| `timeoutHours` | number | | Auto-fail after timeout |
