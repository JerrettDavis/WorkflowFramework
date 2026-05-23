# PatternKit Follow-Up — 0.112.0 Adoption Deferrals

**Branch:** `feat/consume-patternkit-0.112`  
**Date assessed:** 2026-05-22  
**Assessor:** Claude Sonnet 4.6 (refactor agent)

This document captures every step evaluated against PatternKit 0.112.0 during
`feat/consume-patternkit-0.112` that was deferred with the specific reason why. It
supersedes the equivalent "Future Evaluation Targets" notes in `docs/patternkit-adoption.md`.
Use this as the starting point for the next PatternKit adoption pass.

---

## Deferred: NormalizerStep → Normalizer<TRaw,TCanonical>

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/NormalizerStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Transformation.Normalizer<TRaw,TCanonical>`

**Why deferred:**

PatternKit's `Normalizer<TRaw,TCanonical>` dispatches via *content predicates* evaluated in
registration order (first-match-wins). `NormalizerStep` dispatches via an O(1) `Dictionary<string, IStep>`
keyed lookup — the format detector returns a string key, not a `bool` predicate.

Two specific blockers:

1. **Predicate vs key dispatch model.** Converting each dictionary entry to a predicate
   (`raw => format == key`) would change the dispatch from O(1) to O(n) and lose the dictionary
   key contract from the constructor signature.

2. **Error message behavioral mismatch.** When no format matches, the bespoke step throws
   `InvalidOperationException($"No translator found for format '{format}' and no default translator configured.")`.
   The test `UnknownFormatNoDefaultThrows` asserts that the message contains the format string.
   PatternKit's miss reason is `"No format handler matched the raw input for normalizer '{name}'."` —
   the unknown format key is absent. Fixing this without modifying the test requires a custom
   exception mapping wrapper that adds complexity rather than removing it.

**Resumption condition:** If PatternKit adds a keyed-dispatch variant of `Normalizer` that maps
string discriminators to handlers (similar to the `TypeDispatchRouter` sketch in `.plan/patternkit-extension-backlog.md`),
evaluate again.

---

## Deferred: ContentEnricherStep → AsyncContentEnricher<TPayload>

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/ContentEnricherStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Transformation.AsyncContentEnricher<IWorkflowContext>`

**Why deferred:**

`AsyncContentEnricher<TPayload>` is designed for functional enrichment: each step receives the
current payload, augments it, and returns a new payload. The step's implementation stores the
final enriched payload back in the `Message<TPayload>`.

`ContentEnricherStep` is a thin named wrapper around `Func<IWorkflowContext, Task>` — a
side-effecting context mutation. The `IWorkflowContext` is passed by reference; the delegate
mutates it in place. Adapting to PatternKit would require:

```csharp
AsyncContentEnricher<IWorkflowContext>.Create()
    .Enrich("enrich", async (ctx, _, ct) => { await enrichAction(ctx); return ctx; })
    .Build();
```

The `return ctx` discards PatternKit's copy-returning semantics entirely. The wrapper buys
nothing — the code after the refactor is longer than the original. The step itself is 3
non-trivial lines; PatternKit would add ~10 lines of builder setup.

**Resumption condition:** Not warranted at this complexity level. If PatternKit adds a
side-effect-oriented enricher (`AsyncSideEffectEnricher<T>` that does not require a return value),
revisit. Otherwise keep bespoke.

---

## Deferred: IdempotentReceiverStep → IdempotentReceiver<TPayload,TResult>

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/IdempotentReceiverStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Reliability.IdempotentReceiver<IWorkflowContext,object>`

**Why deferred — behavioral breaking change:**

The bespoke step registers the message ID in the `HashSet<string>` *before* calling the inner
step. This means: if the inner step throws, a subsequent call with the same ID is *silently
skipped* (because the ID is already registered). This is tested explicitly:

```
Test: ReAttemptAfterExceptionIsSkipped
Pins: "ID was added to the set BEFORE calling inner, so a second call IS skipped
       even if inner threw."
```

PatternKit's `IdempotentReceiver<TPayload,TResult>` has the opposite semantics:

1. `TryClaimAsync` claims the key (status = `Processing`)
2. Handler is called
3. On exception → `MarkFailedAsync` sets status to `Failed`

A subsequent `TryClaimAsync` with the same key in `Failed` status returns `Claimed = false` AND
sets the status back to `Processing`, allowing the handler to be retried. This is the correct
resilient behavior for production idempotency, but it breaks the characterization test.

**To unblock:** The test would need to be updated to remove the `ReAttemptAfterExceptionIsSkipped`
scenario (or relax it to document PatternKit's retry-on-failure semantics). This requires a
deliberate API-evolution decision — it is not safe to change as a refactor.

---

## Deferred: ClaimCheckStep / ClaimRetrieveStep → ClaimCheck<TPayload>

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/ClaimCheckStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Transformation.ClaimCheck<TPayload>`

**Why deferred — interface mismatch:**

The bespoke `IClaimCheckStore` (in `WorkflowFramework.Extensions.Integration.Abstractions`) is
untyped:

```csharp
Task<string> StoreAsync(object payload, CancellationToken ct);
Task<object> RetrieveAsync(string ticket, CancellationToken ct);
```

PatternKit's `IClaimCheckStore<TPayload>` is typed:

```csharp
ValueTask StoreAsync(string claimId, TPayload payload, MessageHeaders headers, CancellationToken ct);
ValueTask<ClaimCheckStoredPayload<TPayload>?> TryLoadAsync(string claimId, CancellationToken ct);
```

The differences are:
- Untyped `object` vs typed `TPayload`
- PatternKit requires `MessageHeaders` (not available in WF context model)
- PatternKit takes a `claimId` parameter; bespoke generates the ID internally
- PatternKit returns `ClaimCheckStoredPayload<TPayload>?` vs bespoke returning `object`
- PatternKit uses `ValueTask` vs bespoke `Task`

Bridging these interfaces would require an adapter class that wraps `IClaimCheckStore` in
`IClaimCheckStore<object>` (with dummy headers), adding ~20 lines of bridge code to save 0
lines in the step itself. Net: code increase.

**Resumption condition:** If `WorkflowFramework.Extensions.Integration.Abstractions` migrates to
PatternKit's `IClaimCheckStore<TPayload>` as its primary claim check interface, the step can
adopt directly.

---

## Deferred: PollingConsumerStep → AsyncPollingConsumer<TPayload>

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/PollingConsumerStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Consumers.AsyncPollingConsumer<TPayload>`

**Why deferred — semantic lifecycle mismatch:**

`PollingConsumerStep<T>` is a *single-shot* poll step:
1. Call `IPollingSource<T>.PollAsync()` once
2. Write results to `context.Properties[ResultKey]`
3. Return

`AsyncPollingConsumer<TPayload>` is a *continuous polling loop*:
1. Runs a `while (!ct.IsCancellationRequested)` loop
2. Calls the poll source on each iteration
3. Invokes a handler per-message
4. Sleeps between polls with configurable jitter/backoff

These are different abstractions. The step is designed to be called from within a workflow
execution engine (once per workflow tick). PatternKit's consumer is designed to be run as a
background service (long-lived, driven to completion by cancellation).

**Resumption condition:** If a future PatternKit release adds a `SinglePollConsumer<T>` (one-shot
poll adaptor without the loop), that would fit. Alternatively, if WorkflowFramework adds a
background polling host that drives steps in a polling loop, `AsyncPollingConsumer` would be
the right fit there (not in the step itself).

---

## Deferred: ScatterGatherStep → AsyncScatterGather<TRequest,TResponse,TResult>

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/ScatterGatherStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Routing.AsyncScatterGather<IWorkflowContext,object?,IReadOnlyList<object?>>`

**Why deferred — integration complexity and shared-context mutation model:**

The bespoke `ScatterGatherStep` relies on a pattern where:
- All handlers share the same `IWorkflowContext` reference
- Each handler writes its result to `context.Properties[$"__Result_{handler.Name}"]`
- After `Task.WhenAll`, the step reads each handler's named result key from context

PatternKit's `AsyncScatterGather` expects *isolated* per-recipient handlers that return a typed
`TResponse` value. Adapting would require:

1. Each recipient executes the handler (mutating shared context) then reads back its named key
2. The shared `IWorkflowContext` becomes a concurrency hazard when written by parallel recipients
   (the existing implementation has the same hazard but the keys are named distinctly per handler)
3. The aggregator (`Func<IReadOnlyList<object?>, IWorkflowContext, Task>`) has a different
   signature from PatternKit's `ResponseAggregator(IReadOnlyList<ResponseEnvelope<TResponse>>, Message<TRequest>, MessageContext)`

The adaptation would re-implement the shared-context fan-out logic inside PatternKit's
recipient framework without simplifying the code. The characterization tests in
`ScatterGatherStepTests.cs` pin the exact `__Result_{handler.Name}` key convention and the
`context.Properties[ResultsKey]` output.

**Resumption condition:** If the scatter-gather handlers are refactored to return typed values
(rather than writing to named context keys), the step could adopt `AsyncScatterGather` directly.
This is an API-evolution decision beyond the scope of a refactor.

---

## Deferred: TransactionalOutboxStep → IOutboxStore<TPayload>

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/TransactionalOutboxStep.cs`

**PatternKit primitive:** `PatternKit.Messaging.Reliability.IOutboxStore<TPayload>`

**Why deferred — interface mismatch:**

Bespoke `IOutboxStore` (in Abstractions):

```csharp
Task<string> SaveAsync(object message, CancellationToken ct);
```

PatternKit `IOutboxStore<TPayload>`:

```csharp
ValueTask<OutboxMessage<TPayload>> EnqueueAsync(Message<TPayload> message, string? id, DateTimeOffset? createdAt, CancellationToken ct);
```

Key differences:
- Bespoke: untyped `object`, returns outbox ID as `string`
- PatternKit: typed `TPayload` wrapped in `Message<TPayload>`, returns full `OutboxMessage<TPayload>`
- Bespoke: ID is generated by the store; PatternKit accepts an optional caller-provided ID
- The step writes `OutboxIdKey = id` to context; PatternKit's return value is `OutboxMessage<TPayload>`,
  requiring `.Id` access — minor but meaningful difference in null-safety and API surface

**Resumption condition:** If `WorkflowFramework.Extensions.Integration.Abstractions` migrates to
PatternKit's `IOutboxStore<TPayload>` as the primary interface, the step can adopt directly
(write `context.Properties[OutboxIdKey] = result.Id`).
