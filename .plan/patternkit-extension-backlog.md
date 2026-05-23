# PatternKit Extension Backlog

**Source:** Phase G rejection list in `docs/patternkit-adoption.md`  
**PatternKit version assessed against:** 0.105.0  
**Generated:** 2026-05-22

This document characterizes every step kept bespoke in Phase G and proposes the generic PatternKit primitive that would absorb each one. Generality ratings are on a 1–5 scale (5 = clearly broadly useful pattern that belongs in PatternKit; 1 = workflow-engine-specific glue that should stay bespoke). Priority is assigned by combining generality rating with the amount of bespoke code that would be deleted from WorkflowFramework.

---

## Summary Table

| Step | Proposed PatternKit Primitive | Generality | Priority |
|------|-------------------------------|-----------|----------|
| `ScatterGatherStep` | `ScatterGather<TResult>` | 5 | High |
| `WireTapStep` | `WireTap<T>` (or `FireAndForget<T>`) | 5 | High |
| `IdempotentReceiverStep` | `IdempotencyGuard<TKey>` | 5 | High |
| `MessageTranslatorStep<TIn,TOut>` | `AsyncTransformer<TIn,TOut>` | 5 | High |
| `ClaimCheckStep` / `ClaimRetrieveStep` | `ExternalStore<T>` (store+retrieve pair) | 4 | High |
| `AggregatorStep` | `Aggregator<T>` with `CompletionPolicy` | 5 | Med |
| `SplitterStep` | `FanOut<T>` | 4 | Med |
| `NormalizerStep` | `TypeDispatchRouter<TKey>` | 4 | Med |
| `TransactionalOutboxStep` | `OutboxWriter<T>` | 3 | Med |
| `ContentEnricherStep` | `ContextMutator<T>` (or promote `AsyncAction`) | 2 | Low |
| `ContentFilterStep` | `ContextMutator<T>` (or promote `AsyncAction`) | 2 | Low |
| `MessageFilterStep` | `Guard<T>` (predicate gate) | 4 | Med |
| `PollingConsumerStep<T>` | `PollingSource<T>` adapter primitive | 3 | Low |

---

## G.2 — Composition Steps

### `ScatterGatherStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/ScatterGatherStep.cs`

**What it does:** Broadcasts one workflow context to a fixed list of handlers running in parallel via `Task.WhenAll`. Each branch writes its result into a named context key. After the timeout fires (a linked `CancellationTokenSource`), only the branches that completed contribute to the aggregation. Per-branch errors are swallowed so one bad branch cannot abort others.

**Why existing primitive didn't fit:** `AsyncActionComposite` supports parallel execution but has no concept of per-branch result collection, error isolation, or deadline semantics. Adding those on top would require the same amount of code as the bespoke implementation.

**Shape of generic primitive:** A strongly-typed `ScatterGather<TResult>` that fires N async functions in parallel, collects their outputs (null on error or timeout), applies a caller-supplied aggregator, and returns a `ScatterGatherResult<TResult>`. Works on raw `Task<TResult>` delegates — no coupling to `IWorkflowContext`. Every system that fans out to N services and merges responses (pricing engines, search aggregators, health checks) would use this.

**Generality:** 5

```csharp
public sealed class ScatterGather<TResult>
{
    public static Builder Create();

    public sealed class Builder
    {
        // Add a branch that produces a TResult
        public Builder AddBranch(Func<CancellationToken, ValueTask<TResult?>> branch);
        // Swallow per-branch exceptions (default: true)
        public Builder SwallowBranchErrors(bool swallow = true);
        // Overall deadline
        public Builder WithTimeout(TimeSpan timeout);
        public ScatterGather<TResult> Build();
    }

    /// <summary>
    /// Runs all branches. Partial results are returned if the deadline fires.
    /// </summary>
    public ValueTask<ScatterGatherResult<TResult>> ExecuteAsync(CancellationToken ct = default);
}

public sealed class ScatterGatherResult<TResult>
{
    public IReadOnlyList<TResult?> Results { get; }
    public bool TimedOut { get; }
    public int SucceededCount { get; }
}
```

---

### `AggregatorStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/AggregatorStep.cs`

**What it does:** Collects items from a context-provided enumerable and applies a caller-supplied aggregation action. Collection stops when one of three configurable completion policies triggers: item count reached, predicate satisfied, or (in the options API, preparatory for future streaming use) a timeout fires.

**Why existing primitive didn't fit:** No Aggregator primitive exists in PatternKit 0.105.0. The completion-policy tri-mode logic (count / predicate / timeout) is the entire feature; no existing PatternKit behavioral primitive gets close.

**Shape of generic primitive:** A generic `Aggregator<T>` that takes an `IEnumerable<T>` (or `IAsyncEnumerable<T>`) and a composable `CompletionPolicy` value type. The aggregator iterates until the policy signals done, then hands the collected window to a caller-supplied fold function. Useful in streaming, data pipeline, event-batch, and reactive systems far beyond workflow engines.

**Generality:** 5

```csharp
// Completion policy is a value type to avoid allocations
public readonly struct CompletionPolicy<T>
{
    public static CompletionPolicy<T> AfterCount(int count);
    public static CompletionPolicy<T> When(Func<IReadOnlyList<T>, bool> predicate);
    public static CompletionPolicy<T> AfterTimeout(TimeSpan timeout);
    public static CompletionPolicy<T> All(); // collect everything
}

public sealed class Aggregator<T>
{
    public static Aggregator<T> Create(CompletionPolicy<T> policy);

    /// <summary>
    /// Collects from <paramref name="source"/> until the policy is satisfied,
    /// then returns the collected window.
    /// </summary>
    public IReadOnlyList<T> Collect(IEnumerable<T> source);
    public ValueTask<IReadOnlyList<T>> CollectAsync(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default);
}
```

---

### `SplitterStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/SplitterStep.cs`

**What it does:** Given a delegate that extracts an `IEnumerable<object>` from context, executes a single `IStep` once per item — either sequentially or in parallel. Tracks the current item in `__SplitterCurrentItem` and collects per-item results in `__SplitterResults`.

**Why existing primitive didn't fit:** `AsyncActionComposite` requires all child actions to be registered at build time. `SplitterStep`'s item list is determined at execution time from context; you cannot build the composite without running first.

**Shape of generic primitive:** A `FanOut<TItem, TResult>` that takes an `IEnumerable<TItem>` and a `Func<TItem, CancellationToken, ValueTask<TResult>>`, executes it per-item (parallel or sequential), and returns `IReadOnlyList<TResult>`. Applicable in batch processing, ETL pipelines, parallel API calls, and any map-over-collection pattern.

**Generality:** 4

```csharp
public sealed class FanOut<TItem, TResult>
{
    public static Builder Create();

    public sealed class Builder
    {
        public Builder WithProcessor(Func<TItem, CancellationToken, ValueTask<TResult>> processor);
        public Builder Parallel(bool parallel = true);
        // Optional: max degree of parallelism
        public Builder MaxDegreeOfParallelism(int max);
        public FanOut<TItem, TResult> Build();
    }

    public ValueTask<IReadOnlyList<TResult>> ExecuteAsync(
        IEnumerable<TItem> items,
        CancellationToken ct = default);
}
```

---

### `ResequencerStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/ResequencerStep.cs`

**What it does:** Pure LINQ `OrderBy` over a context-provided collection. One-liner implementation with no structural complexity.

**Why existing primitive didn't fit:** PatternKit has no sort-pipeline primitive; wrapping with PatternKit would add indirection for zero benefit.

**Shape of generic primitive:** No PatternKit primitive is warranted here. The implementation is three lines of LINQ. Any generic `Sort<T>` primitive would be identical to calling `.OrderBy()` directly. **Recommended to stay bespoke.**

**Generality:** 1

---

### `ProcessManagerStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/ProcessManagerStep.cs`

**What it does:** Implements the EIP Process Manager — a long-running correlator that picks the next step to execute by delegating to a context-reading function at runtime, re-evaluating after each step completes.

**Why existing primitive didn't fit:** PatternKit `AsyncStateMachine` requires all states and transitions to be declared at construction time. `ProcessManagerStep` resolves its next-step delegate dynamically from mutable context; the state space is open and runtime-defined.

**Shape of generic primitive:** A true runtime-dynamic state machine would require an entirely different abstraction from `StateMachine<TState, TEvent>`. The delta from PatternKit's existing `StateMachine` is large enough that a separate `DynamicStateMachine<TContext>` primitive might be warranted if multiple projects need it — but it is borderline. **Recommend: keep bespoke for now; revisit if a second consumer emerges.**

**Generality:** 2

---

### `ComposedMessageProcessorStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Composition/ComposedMessageProcessorStep.cs`

**What it does:** Composes SplitterStep → per-item-processor → AggregatorStep as a single pipeline step, all sourced from context delegates at runtime.

**Why existing primitive didn't fit:** No single PatternKit primitive composes all three dynamic operations. It is a thin orchestrator over three other bespoke steps.

**Shape of generic primitive:** Once `FanOut<TItem,TResult>` and `Aggregator<T>` exist in PatternKit, this step can be rewritten as three lines of those primitives. No separate PatternKit primitive is needed — it dissolves when its constituent primitives are promoted.

**Generality:** 2

---

## G.3 — Channel Steps

### `WireTapStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Channel/WireTapStep.cs`

**What it does:** Executes a side-effect action on the current value without affecting the return path. By default, exceptions thrown by the tap action are swallowed so the main pipeline continues unaffected. Configurable to let exceptions propagate when strict auditing is needed.

**Why existing primitive didn't fit:** `AsyncActionDecorator` wraps a component and transforms/intercepts its result. `WireTapStep` wraps nothing — it IS the side effect. The decorator pattern adds indirection without modelling "fire side-effect, return same value."

**Shape of generic primitive:** A value-passing `WireTap<T>` that runs one or more side-effect delegates and returns the original value unchanged. Broadly useful: observability middleware, audit logging, metrics emission, notification fanout in any pipeline or chain. The error-swallowing flag maps to `ObserveErrors` / `SwallowErrors` on the builder.

**Generality:** 5

```csharp
public sealed class WireTap<T>
{
    public static Builder Create();

    public sealed class Builder
    {
        public Builder Tap(Action<T> sideEffect);
        public Builder TapAsync(Func<T, CancellationToken, ValueTask> sideEffect);
        // If false, tap exceptions propagate (default: true = swallow)
        public Builder SwallowErrors(bool swallow = true);
        public WireTap<T> Build();
    }

    /// <summary>
    /// Runs all taps and returns <paramref name="value"/> unchanged.
    /// </summary>
    public ValueTask<T> ExecuteAsync(T value, CancellationToken ct = default);
}
```

---

### `ChannelAdapterStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Channel/ChannelAdapterStep.cs`

**What it does:** Sends or receives a message via an `IChannelAdapter` (abstraction over queue/topic/socket). The operation is a side effect against an external transport, not a type mapping.

**Why existing primitive didn't fit:** PatternKit `AsyncAdapter<TIn,TOut>` is a type-mapping primitive that transforms one type into another. `ChannelAdapterStep` performs an I/O side-effect; the type signature is orthogonal.

**Shape of generic primitive:** A transport-agnostic `ChannelAdapter<T>` primitive with pluggable send/receive contracts would be broadly useful. However, channel semantics (ack, nack, redelivery, back-pressure) vary dramatically between transports. A thin interface like `IChannelAdapter<T>` already lives in the abstractions project. **Recommended: promote `IChannelAdapter<T>` to PatternKit.Messaging if a Messaging namespace is planned; hold on the step wrapper itself.**

**Generality:** 3

---

### `MessageBridgeStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Channel/MessageBridgeStep.cs`

**What it does:** Receives from one `IChannelAdapter` and immediately forwards to another in a single atomic step. Implements the EIP Message Bridge pattern.

**Why existing primitive didn't fit:** PatternKit Bridge connects two class hierarchies (abstraction/implementation separation). It does not model a runtime receive→forward relay between two transport endpoints.

**Shape of generic primitive:** A `MessageRelay<T>` or `Bridge<T>` primitive (receive from `ISource<T>` → publish to `ISink<T>`) would be useful anywhere message forwarding between two transports is needed (e.g., bus-to-bus migration, fan-in). Low complexity, but the abstraction is clean. **Low priority compared to higher-complexity primitives.**

**Generality:** 3

---

### `DeadLetterStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Channel/DeadLetterStep.cs`

**What it does:** Extracts a dead-letter payload from context and writes it to an `IDeadLetterStore`. Implements EIP Dead Letter Channel.

**Why existing primitive didn't fit:** PatternKit Decorator transforms inputs; it cannot catch exceptions and route them to error stores. No dead-letter primitive exists in PatternKit 0.105.0.

**Shape of generic primitive:** A `DeadLetterChannel<T>` primitive that accepts a failed item plus an `IDeadLetterSink<T>`, stores it, and optionally emits an event. Useful in any resilient messaging, retry, and error-drain pipeline — well beyond workflow engines.

**Generality:** 4

```csharp
public interface IDeadLetterSink<T>
{
    ValueTask WriteAsync(T item, Exception? reason, CancellationToken ct = default);
}

public sealed class DeadLetterChannel<T>
{
    public static DeadLetterChannel<T> Create(IDeadLetterSink<T> sink);

    /// <summary>
    /// Routes <paramref name="item"/> to the dead-letter sink.
    /// Optionally attaches <paramref name="reason"/> metadata.
    /// </summary>
    public ValueTask SendAsync(T item, Exception? reason = null, CancellationToken ct = default);
}
```

---

## G.4 — Endpoint Steps

### `IdempotentReceiverStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/IdempotentReceiverStep.cs`

**What it does:** Decorates any `IStep` with idempotency: extracts a string message ID from context, checks it against an in-memory `HashSet<string>` (under a plain `lock`), and skips execution if the ID has been seen before.

**Why existing primitive didn't fit:** No idempotency primitive exists in PatternKit 0.105.0. The `HashSet + lock` implementation is correct but in-process only (no persistence, no distributed dedup). A PatternKit primitive should abstract the backing store.

**Shape of generic primitive:** An `IdempotencyGuard<TKey>` that wraps any `Func<CancellationToken, ValueTask>` and enforces once-and-only-once execution per key. The backing store is pluggable (`IIdempotencyStore<TKey>`), with a built-in in-memory default. Useful in event consumers, webhook handlers, API retry deduplication, and any at-least-once delivery context.

**Generality:** 5

```csharp
public interface IIdempotencyStore<TKey>
{
    // Returns true if the key was NEW (first seen); false if already processed
    ValueTask<bool> TryAddAsync(TKey key, CancellationToken ct = default);
}

public sealed class IdempotencyGuard<TKey>
{
    // In-memory default
    public static IdempotencyGuard<TKey> InMemory();
    // Pluggable backing store (Redis, DB, etc.)
    public static IdempotencyGuard<TKey> WithStore(IIdempotencyStore<TKey> store);

    /// <summary>
    /// Executes <paramref name="action"/> only if <paramref name="key"/> has not been seen before.
    /// Returns true if action was executed; false if it was deduplicated.
    /// </summary>
    public ValueTask<bool> ExecuteOnceAsync(
        TKey key,
        Func<CancellationToken, ValueTask> action,
        CancellationToken ct = default);
}
```

---

### `PollingConsumerStep<T>`

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/PollingConsumerStep.cs`

**What it does:** Calls `IPollingSource<T>.PollAsync()` and writes the resulting items list into `context.Properties[ResultKey]`. The step itself is a thin adaptor; all polling policy (interval, backoff) is the caller's responsibility.

**Why existing primitive didn't fit:** No polling primitive exists in PatternKit 0.105.0. The step is almost trivially thin — one method call.

**Shape of generic primitive:** A `PollingLoop<T>` primitive that adds the poll interval, back-off, and error-retry concerns that the current `IPollingSource<T>` interface leaves to the caller. `IPollingSource<T>` is already a good abstraction; it just needs a runner. However, because the step itself is so thin, the value is primarily in the runner/loop primitive rather than the step wrapper. **Moderate priority.**

**Generality:** 3

```csharp
public sealed class PollingLoop<T>
{
    public static Builder Create(IPollingSource<T> source);

    public sealed class Builder
    {
        public Builder WithInterval(TimeSpan interval);
        public Builder WithExponentialBackoff(TimeSpan initial, TimeSpan max);
        public Builder OnError(Func<Exception, bool> shouldContinue);
        public PollingLoop<T> Build();
    }

    /// <summary>
    /// Polls until <paramref name="ct"/> is cancelled, calling
    /// <paramref name="onBatch"/> for each non-empty result.
    /// </summary>
    public ValueTask RunAsync(
        Func<IReadOnlyList<T>, CancellationToken, ValueTask> onBatch,
        CancellationToken ct = default);
}
```

---

### `TransactionalOutboxStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Endpoint/TransactionalOutboxStep.cs`

**What it does:** Extracts a message from context via a selector delegate, calls `IOutboxStore.SaveAsync()`, and writes the returned outbox ID back into context. Implements the write half of the Transactional Outbox pattern.

**Why existing primitive didn't fit:** No outbox primitive exists in PatternKit 0.105.0. The pattern requires coordination between the write step (this) and a background relay publisher, which PatternKit does not model.

**Shape of generic primitive:** An `OutboxWriter<T>` that wraps `IOutboxStore` and provides a clean typed entry point. Pairing this with an `OutboxRelay<T>` background pump would make the full pattern reusable. `IOutboxStore` already exists in the Abstractions project and could be promoted to PatternKit.Messaging. **Moderate generality** — the pattern is universal in microservices, but the backing store contract is highly persistence-tier-specific.

**Generality:** 3

```csharp
public sealed class OutboxWriter<T>
{
    public OutboxWriter(IOutboxStore store);

    /// <summary>
    /// Saves <paramref name="message"/> to the outbox.
    /// Returns the assigned outbox message ID.
    /// </summary>
    public ValueTask<string> WriteAsync(T message, CancellationToken ct = default);
}

// Companion (separate class): background drain pump
public sealed class OutboxRelay<T>
{
    public OutboxRelay(IOutboxStore store, Func<T, CancellationToken, ValueTask> publisher);
    public ValueTask DrainAsync(int batchSize = 100, CancellationToken ct = default);
}
```

---

## G.5 — Transformation Steps

### `MessageTranslatorStep<TIn, TOut>`

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/MessageTranslatorStep.cs`

**What it does:** Pulls `TIn` from context using a selector, calls `IMessageTranslator<TIn,TOut>.TranslateAsync()`, and writes `TOut` back into context under a named key. Bridges a typed async conversion contract into the workflow context bag.

**Why existing primitive didn't fit:** PatternKit `AsyncAdapter<TIn,TOut>` requires constructing with a fixed conversion function and does not integrate with a named-service interface (`IMessageTranslator<TIn,TOut>`) or participate in context key routing. The mismatch is minor — this is one of the strongest adoption candidates.

**Shape of generic primitive:** An `AsyncTransformer<TIn, TOut>` that takes any `Func<TIn, CancellationToken, ValueTask<TOut>>` (or the `IMessageTranslator` interface as an overload) and applies it. This is strictly more composable than `AsyncAdapter` — it is a pure function wrapper with no side-effect coupling. Useful in mapping pipelines, serialization chains, ML pre-processing, ETL, and anywhere a typed async transform is needed.

**Generality:** 5

```csharp
public sealed class AsyncTransformer<TIn, TOut>
{
    public static AsyncTransformer<TIn, TOut> FromFunc(
        Func<TIn, CancellationToken, ValueTask<TOut>> func);

    public static AsyncTransformer<TIn, TOut> FromTranslator(
        IAsyncTranslator<TIn, TOut> translator);

    public ValueTask<TOut> TransformAsync(TIn input, CancellationToken ct = default);
}

// Thin interface PatternKit can own (aligns with IMessageTranslator, renamed for generality)
public interface IAsyncTranslator<in TIn, TOut>
{
    ValueTask<TOut> TranslateAsync(TIn input, CancellationToken ct = default);
}
```

---

### `ContentEnricherStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/ContentEnricherStep.cs`

**What it does:** Accepts a single `Func<IWorkflowContext, Task>` and calls it. Provides a named wrapper (`ContentEnricher`) around an arbitrary async context-mutation action. No structural logic; purely a named step.

**Why existing primitive didn't fit:** No enricher primitive was assessed. The implementation is so thin it is essentially a named lambda adapter.

**Shape of generic primitive:** Could be absorbed by a general `ContextMutator<TContext>` primitive, but this would just be a renamed `Action<TContext>` wrapper. The value is in the name, not the structure. **Recommend: keep bespoke; not worth a PatternKit primitive at this granularity.**

**Generality:** 2

---

### `ContentFilterStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/ContentFilterStep.cs`

**What it does:** Structurally identical to `ContentEnricherStep` — a single `Func<IWorkflowContext, Task>` with a different name (`ContentFilter`). Strips fields from context rather than adding them.

**Why existing primitive didn't fit:** Same reasoning as `ContentEnricherStep` — no structural complexity to abstract.

**Shape of generic primitive:** Same as `ContentEnricherStep` analysis — not worth a PatternKit primitive. A general `AsyncAction<T>` or `ContextMutator<T>` primitive would cover both, but both steps would become one-liners at the call site anyway. **Recommend: keep bespoke; consolidate with `ContentEnricherStep` into a shared internal base if desired.**

**Generality:** 2

---

### `NormalizerStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/NormalizerStep.cs`

**What it does:** Detects the incoming data format using a delegate, looks up a matching `IStep` in a `Dictionary<string, IStep>`, and dispatches to it. Falls back to a default translator if the format is unknown. Throws if neither is found.

**Why existing primitive didn't fit:** PatternKit's `AsyncActionStrategy` (used by `ContentBasedRouterStep`) builds its predicate/handler pairs at construction time and evaluates them in order. `NormalizerStep` uses a keyed dictionary lookup (O(1)) not a sequential predicate scan.

**Shape of generic primitive:** A `TypeDispatchRouter<TKey, TTarget>` — a keyed dispatch table that maps a runtime-computed discriminator key to an action/handler, with an optional default case. Broadly useful: format routing, command dispatch, event handlers, variant execution — any "switch on a runtime string/enum and execute the matching handler" pattern.

**Generality:** 4

```csharp
public sealed class TypeDispatchRouter<TKey, TTarget>
    where TKey : notnull
{
    public static Builder Create();

    public sealed class Builder
    {
        public Builder Register(TKey key, Func<TTarget, CancellationToken, ValueTask> handler);
        public Builder WithDefault(Func<TKey, TTarget, CancellationToken, ValueTask> fallback);
        // Throw if no match and no default (default behavior)
        public Builder ThrowIfUnmatched(bool shouldThrow = true);
        public TypeDispatchRouter<TKey, TTarget> Build();
    }

    public ValueTask DispatchAsync(TKey key, TTarget target, CancellationToken ct = default);
}
```

---

### `ClaimCheckStep` / `ClaimRetrieveStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Transformation/ClaimCheckStep.cs`

**What it does:** `ClaimCheckStep` stores a large payload in an `IClaimCheckStore` and writes the resulting claim ticket string into context. `ClaimRetrieveStep` reads the ticket from context and retrieves the payload, writing it back under a configurable key. The two steps are designed to be used as a matched pair flanking a pipeline that needs to carry only a lightweight reference.

**Why existing primitive didn't fit:** No claim-check primitive exists in PatternKit 0.105.0. PatternKit Decorator transforms inputs in place; it does not model external reference storage with a retrieve step.

**Shape of generic primitive:** An `ExternalStore<T>` primitive (or `ClaimCheck<T>`) that owns the store-and-retrieve pair. The key insight is the generic ticket type — making it `string` is a design choice that should be preserved or made configurable. Useful in any pipeline where oversized payloads need to be offloaded: file processing, ML inference, document transformation, cross-service data handoff.

**Generality:** 4

```csharp
public interface IExternalStore<T>
{
    ValueTask<string> StoreAsync(T payload, CancellationToken ct = default);
    ValueTask<T> RetrieveAsync(string ticket, CancellationToken ct = default);
}

public sealed class ExternalStore<T>
{
    public ExternalStore(IExternalStore<T> store);

    /// <summary>Stores payload; returns the claim ticket.</summary>
    public ValueTask<string> StoreAsync(T payload, CancellationToken ct = default);

    /// <summary>Retrieves payload using a previously obtained ticket.</summary>
    public ValueTask<T> RetrieveAsync(string ticket, CancellationToken ct = default);
}
```

---

### `MessageFilterStep`

**File:** `src/WorkflowFramework.Extensions.Integration/Routing/MessageFilterStep.cs`

**What it does:** Evaluates a single synchronous `Func<IWorkflowContext, bool>` predicate. If it returns false, sets `context.IsAborted = true` and returns. One of the simplest implementations in the codebase — but the pattern (predicate gate that halts a pipeline) recurs everywhere.

**Why existing primitive didn't fit:** PatternKit.Core 0.105.0 does not expose a standalone predicate/guard primitive. Strategy is overkill for a single boolean.

**Shape of generic primitive:** A `Guard<T>` primitive — a lightweight predicate gate that conditionally blocks execution. Useful as a composable building block in pipelines, interceptors, authorization checks, and circuit-breaker logic. The guard can throw, return false, or call a fallback on mismatch.

**Generality:** 4

```csharp
public sealed class Guard<T>
{
    public static Guard<T> When(Func<T, bool> predicate);
    public static Guard<T> WhenAsync(Func<T, CancellationToken, ValueTask<bool>> predicate);

    /// <summary>
    /// Returns true if the guard passes; false if blocked.
    /// Optionally invokes <paramref name="onBlocked"/> when predicate fails.
    /// </summary>
    public ValueTask<bool> EvaluateAsync(
        T value,
        Func<T, CancellationToken, ValueTask>? onBlocked = null,
        CancellationToken ct = default);
}
```

---

## Notes on Steps Outside G.4/G.5 Scope

The following steps from the adoption doc are bespoke for architectural reasons and are not candidates for PatternKit primitives:

- **`RoutingSlipStep`** — Dynamic itinerary from context; PatternKit `AsyncActionChain` is construction-time only. Stay bespoke.
- **`DynamicRouterStep`** — Routing function produces a different step each iteration from evolving context (feedback loop). No static primitive models this. Stay bespoke.
- **`RecipientListStep`** — Recipients resolved from context at execution time. `AsyncActionComposite` requires build-time registration. Stay bespoke.
- **`ResequencerStep`** — Three lines of LINQ. No abstraction warranted.
- **`ProcessManagerStep`** / **`ComposedMessageProcessorStep`** — Runtime-dynamic state; dissolves naturally once constituent primitives (`FanOut`, `Aggregator`) are promoted to PatternKit.
