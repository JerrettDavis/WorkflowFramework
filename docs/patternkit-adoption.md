# PatternKit Adoption Inventory

**PatternKit version:** 0.113.0  
**Last updated:** 2026-05-22 (feat/iter2-minor-refactors — NormalizerStep / PollingConsumerStep / IdempotentReceiverStep adopted)

This document lists every point in the WorkflowFramework codebase where a PatternKit primitive is used, and every point where a step is intentionally kept bespoke with the rationale for that decision. This is the canonical reference for Phase I and future phases.

---

## Adopted — PatternKit Primitive in Use

### 1. `WorkflowSpec` — Specification pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework/Validation/WorkflowSpec.cs` |
| **PatternKit namespace** | `PatternKit.Application.Specification` |
| **Primitive** | `Specification<T>` (composable predicate objects) |
| **Purpose** | Internal composition of workflow validation rules (`HasAtLeastOneStep`, `NoDuplicateStepNames`) inside `DefaultWorkflowValidator`. Public API is unchanged. |
| **Phase introduced** | QW-2 / Phase I |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/` (DefaultWorkflowValidator scenarios) |

### 2. `WorkflowStatusMachine` — State machine pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework/Internal/WorkflowStatusMachine.cs` |
| **PatternKit namespace** | `PatternKit.Behavioral.State` |
| **Primitive** | `StateMachine<TState, TEvent>` |
| **Purpose** | Authoritative state machine that defines and enforces legal `WorkflowStatus` transitions (`Pending→Running`, `Running→Completed`, `Running→Faulted`, `Running→Aborted`, `Running→Suspended`, `Suspended→Running`, `Running→Compensated`). Wired as an authoritative component of `WorkflowEngine`. |
| **Phase introduced** | QW-3 / Phase F |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Core/WorkflowStatusMachineScenarios.cs` |
| **Limitation note** | The `Running→Compensated` transition is driven by compensation event from `WorkflowEngine` rather than a built-in PatternKit saga hook — PatternKit's state machine builder does not expose a compensation lifecycle hook, so the event is fired manually by the engine. |

### 3. `ContentBasedRouterStep` — Strategy pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Routing/ContentBasedRouterStep.cs` |
| **PatternKit namespace** | `PatternKit.Behavioral.Strategy` |
| **Primitive** | `AsyncActionStrategy<IWorkflowContext>` |
| **Purpose** | Evaluates predicate/handler branch pairs in order and executes the first matching handler. PatternKit Strategy cleanly models the "evaluate predicates until one matches" pattern used by content-based routing. |
| **Phase introduced** | Phase G.1 |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Routing/ContentBasedRouterStepScenarios.cs` |
| **Public API change** | None — swap is internal-only. |

### 4. `WireTapStep` — Wire tap channel pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Channel/WireTapStep.cs` |
| **PatternKit namespace** | `PatternKit.Messaging.Channels` |
| **Primitive** | `AsyncWireTap<IWorkflowContext>` |
| **Purpose** | Wraps the caller-supplied `Func<IWorkflowContext, Task>` into a PatternKit tap handler. The `swallowErrors` constructor parameter maps to `TapErrorPolicy.Swallow` / `TapErrorPolicy.Propagate`. The `IWorkflowContext` is wrapped in a `Message<IWorkflowContext>` for transit and unwrapped inside the tap lambda — no public API or behavioral change. |
| **Phase introduced** | feat/consume-patternkit-0.112 |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Channel/WireTapStepScenarios.cs` — all 8 Phase G.3 scenarios pass without modification. |
| **Public API change** | None — swap is internal-only. |
| **Net delta** | −15 lines of bespoke logic (try/catch error-swallowing replaced by `TapErrorPolicy`). |

### 5. `NormalizerStep` — Keyed normalizer pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Transformation/NormalizerStep.cs` |
| **PatternKit namespace** | `PatternKit.Messaging.Transformation` |
| **Primitive** | `KeyedNormalizer<string, IWorkflowContext, IWorkflowContext>` (new in 0.113.0) |
| **Purpose** | Delegates O(1) key-based dispatch to `KeyedNormalizer`. Each `IStep` translator is wrapped as an async handler. The original `InvalidOperationException` contract is preserved by catching `KeyNotFoundException` from the PatternKit miss path and wrapping it with the bespoke message. |
| **Phase introduced** | feat/iter2-minor-refactors |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Transformation/NormalizerStepScenarios.cs` — all 9 scenarios pass without modification. |
| **Public API change** | None — swap is internal-only. |
| **Net delta** | −10 lines of bespoke dict-lookup logic (absorbed into KeyedNormalizer). |

### 6. `PollingConsumerStep` — Async polling consumer pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Endpoint/PollingConsumerStep.cs` |
| **PatternKit namespace** | `PatternKit.Messaging.Consumers` |
| **Primitive** | `AsyncPollingConsumer<IReadOnlyList<T>>.PollOnceAsync` (new in 0.113.0) |
| **Purpose** | Bridges `IPollingSource<T>` (which returns `IReadOnlyList<T>`) to `AsyncPollingConsumer`'s `AsyncPollSource` delegate via `WithSource`. Calls `PollOnceAsync` for a single-shot poll without entering the consumer's run loop. The `ResultKey` write contract and `IReadOnlyList<T>` output type are unchanged. |
| **Phase introduced** | feat/iter2-minor-refactors |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Endpoint/PollingConsumerStepScenarios.cs` — all 8 scenarios pass without modification. |
| **Public API change** | None — swap is internal-only. |
| **Net delta** | −4 lines (bespoke poll call replaced by consumer delegation). |

### 7. `IdempotentReceiverStep` — Idempotent receiver pattern

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Endpoint/IdempotentReceiverStep.cs` |
| **PatternKit namespace** | `PatternKit.Messaging.Reliability` |
| **Primitive** | `IIdempotencyStore` (in 0.112.0+) with `TryClaimAsync` / `MarkCompletedAsync` / `MarkFailedAsync` |
| **Purpose** | Replaces the bespoke `HashSet<string>` with claim/complete/fail semantics. The default store (`RetryAfterFailureIdempotencyStore`) resets Failed keys to Processing on re-claim, allowing retry after transient failure. Completed keys are still suppressed, preserving the successful-dedup contract. |
| **Phase introduced** | feat/iter2-minor-refactors |
| **Behavior change** | **BUG FIX:** Previously, a step failure permanently locked the idempotency key, preventing retries. A transient inner-step failure should not lock future attempts. The new semantics: `Failed` → retry allowed; `Completed` → deduplicated. |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Endpoint/IdempotentReceiverStepScenarios.cs` — 9 scenarios; `ReAttemptAfterExceptionIsSkipped` renamed to `ReAttemptAfterExceptionIsAllowed` with assertion flipped. |
| **Public API change** | Added optional `IIdempotencyStore` overload in the constructor. Default two-arg constructor preserved. |
| **Net delta** | +83 lines net (bespoke `HashSet` replaced by `IIdempotencyStore` + internal default impl). |

---

## Intentionally Bespoke

The following EIP steps and other components were evaluated against PatternKit 0.105.0 and found to be better served by their current bespoke implementations. Each entry includes the rationale documented in the source file header.

### EIP Composition Steps

#### `ScatterGatherStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/ScatterGatherStep.cs` |
| **Rationale** | PatternKit 0.105.0 does not expose a ScatterGather primitive. `AsyncActionComposite` supports parallel execution but lacks result-collection, per-branch error swallowing, and timeout/partial-result semantics implemented via `Task.WhenAll` + linked `CancellationTokenSource`. |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Composition/ScatterGatherStepScenarios.cs` |
| **Revisit** | If a future PatternKit release adds a ScatterGather primitive with timeout semantics, evaluate in Phase G revision. |

#### `AggregatorStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/AggregatorStep.cs` |
| **Rationale** | PatternKit 0.105.0 has no Aggregator primitive. The completion condition logic (count, predicate, timeout) is unique to the EIP Aggregator pattern and cannot be modelled by any existing PatternKit behavioral or structural primitive without recreating all the logic. |
| **Test coverage** | Phase G.2 characterization tests |
| **Revisit** | PatternKit 0.106+ — check for Aggregator or CompletionStrategy primitive. |

#### `SplitterStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/SplitterStep.cs` |
| **Rationale** | `SplitterStep` splits an arbitrary context-provided collection into per-item parallel steps resolved at execution time. The per-item processed-output tracking (`__ProcessedItem` key) is not modelled by PatternKit. |
| **Test coverage** | Phase G.2 characterization tests |

#### `ResequencerStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/ResequencerStep.cs` |
| **Rationale** | Pure LINQ `OrderBy` over a context-provided collection. PatternKit 0.105.0 has no Resequencer or sort-pipeline primitive; adding a PatternKit wrapper would add indirection without any benefit. |
| **Test coverage** | Phase G.2 characterization tests |

#### `ProcessManagerStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/ProcessManagerStep.cs` |
| **Rationale** | PatternKit `AsyncStateMachine` requires all states and transitions to be declared at construction time. `ProcessManagerStep`'s state is determined dynamically by a delegate reading `IWorkflowContext`, and transitions occur when a handler mutates context (not by firing named events). This runtime-dynamic pattern cannot be expressed cleanly with PatternKit's compile-time state machine builder. |
| **Test coverage** | Phase G.2 characterization tests |

#### `ComposedMessageProcessorStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Composition/ComposedMessageProcessorStep.cs` |
| **Rationale** | A pipeline of three dynamic operations (splitter→per-item-processor→aggregator) all sourced from context at runtime. No single PatternKit primitive composes this pattern without recreating the full logic. |
| **Test coverage** | Phase G.2 characterization tests |

### EIP Routing Steps

#### `RoutingSlipStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Routing/RoutingSlipStep.cs` |
| **Rationale** | PatternKit `AsyncActionChain` builds its pipeline at construction time from a fixed handler set. `RoutingSlip` selects the step registry and itinerary dynamically at runtime from `IWorkflowContext`. No PatternKit primitive cleanly models a dynamic, state-advancing chain; keeping bespoke avoids a leaky abstraction. |
| **Test coverage** | `tests/WorkflowFramework.Tests.TinyBDD/Integration/Routing/RoutingSlipStepScenarios.cs` |

#### `DynamicRouterStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Routing/DynamicRouterStep.cs` |
| **Rationale** | The routing function can return a different step each iteration based on evolving context state (a feedback loop). PatternKit Strategy/Chain patterns pre-bake the route set at construction time and cannot model this runtime-adaptive routing. |
| **Test coverage** | Phase G.1 characterization tests |

#### `RecipientListStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Routing/RecipientListStep.cs` |
| **Rationale** | `AsyncActionComposite<IWorkflowContext>` requires child actions to be known and registered at build time. Recipients are resolved dynamically from `IWorkflowContext` at execution time via a delegate. |
| **Test coverage** | Phase G.1 characterization tests |

#### `MessageFilterStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Routing/MessageFilterStep.cs` |
| **Rationale** | Single-predicate step (predicate → abort-or-continue). PatternKit.Core 0.105.0 does not expose a standalone Specification type; Behavioral.Strategy primitives are overkill for a single boolean predicate. |
| **Test coverage** | Phase G.1 characterization tests |

### EIP Channel Steps

#### `ChannelAdapterStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Channel/ChannelAdapterStep.cs` |
| **Rationale** | PatternKit `AsyncAdapter<TIn,TOut>` is a type-mapping pattern (produce `TOut` from `TIn`). `ChannelAdapterStep` is a side-effect operation (send/receive via `IChannelAdapter`). The send/receive contract doesn't fit the adapt-a-type signature. |
| **Test coverage** | Phase G.3 characterization tests |

#### ~~`WireTapStep`~~ — **Adopted in feat/consume-patternkit-0.112**

Moved to the **Adopted** section above. Now delegates to `PatternKit.Messaging.Channels.AsyncWireTap<IWorkflowContext>`.

#### `MessageBridgeStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Channel/MessageBridgeStep.cs` |
| **Rationale** | Receives from one `IChannelAdapter` and sends to another in a single atomic step. PatternKit Bridge connects two hierarchies abstractly (implementation vs abstraction); it does not model a runtime channel receive→forward pipeline. Two-call thin wrapper; no PatternKit primitive reduces the code further. |
| **Test coverage** | Phase G.3 characterization tests |

#### `DeadLetterStep`

| Item | Detail |
|------|--------|
| **File** | `src/WorkflowFramework.Extensions.Integration/Channel/DeadLetterStep.cs` |
| **Rationale** | Extracts the dead-letter payload from context and routes it to an `IDeadLetterStore`. PatternKit 0.105.0 has no dead-letter or error-routing primitive. PatternKit Decorator transforms inputs; it does not catch exceptions and route them to external stores. |
| **Test coverage** | Phase G.3 characterization tests |

---

## Decision Criteria

When evaluating a bespoke component for PatternKit adoption, the following criteria guide the decision:

1. **Does a matching PatternKit primitive exist?** If no matching primitive exists in the current version, keep bespoke and document.
2. **Does adoption remove real complexity?** If wrapping the primitive requires the same or more code, adoption adds indirection without benefit. Keep bespoke.
3. **Is the behavior determined at construction time or runtime?** PatternKit primitives (Chain, Composite, Strategy) typically require their structure to be declared at construction. Steps that build their structure dynamically from `IWorkflowContext` at execution time are a poor fit.
4. **Is the public API preserved?** Adoption must be internal-only. Any public type or method signature change requires a separate API-evolution PR.
5. **Does characterization test coverage exist first?** No adoption without a TinyBDD scenario set that pins current behavior.

---

## Future Evaluation Targets

The following components are candidates for PatternKit adoption in later phases if suitable primitives become available or interface alignment is achieved:

| Component | Potential Primitive | Blocking Reason (assessed against 0.113.0) |
|-----------|--------------------|-----------------------|
| `ContentEnricherStep` | `AsyncContentEnricher<TPayload>` (now in 0.113.0) | Path C — intentionally bespoke. PatternKit returns an enriched payload copy; bespoke mutates `IWorkflowContext` in place via a `Func<IWorkflowContext, Task>`. Wrapping adds indirection for zero functional benefit. See `.plan/patternkit-iteration-2.md` §2. |
| `ClaimCheckStep` / `ClaimRetrieveStep` | `ClaimCheck<TPayload>` (now in 0.113.0) | Interface mismatch: bespoke `IClaimCheckStore` is untyped (`object`); PatternKit `IClaimCheckStore<TPayload>` is typed. Deferred to Iteration 2 Phase 3 — requires adapter + interface migration. |
| `ScatterGatherStep` | `AsyncScatterGather<TRequest,TResponse,TResult>` (now in 0.113.0) | Integration complexity: handlers mutate a shared `IWorkflowContext` and write results to named context keys; PatternKit's per-recipient isolation model returns typed `TResponse` values. Deferred to Iteration 2 Phase 3. |
| `TransactionalOutboxStep` | `IOutboxStore<TPayload>` (now in 0.113.0) | Interface mismatch: bespoke `IOutboxStore` uses `SaveAsync(object) → string`; PatternKit `IOutboxStore<TPayload>` uses `EnqueueAsync(Message<TPayload>) → OutboxMessage<TPayload>`. Deferred to Iteration 2 Phase 3. |
| `AggregatorStep` | PatternKit Aggregator (future) | No Aggregator primitive in 0.113.0 |
| `PluginManager` | `Strategy` + `AbstractFactory` | Phase H.8 — not yet started |
| `AgentLoopStep` / `AgentDecisionStep` | TypeDispatcher | Phase H.7 — not yet started |
| `ResilienceMiddleware` (Polly) | `RetryPolicy` | Phase F pilot option B — deferred |

**Adopted in feat/iter2-minor-refactors (Iteration 2 Phase 2):** `NormalizerStep` (→ `KeyedNormalizer`), `PollingConsumerStep` (→ `PollOnceAsync`), `IdempotentReceiverStep` (→ `IIdempotencyStore` claim/complete/fail — includes bug fix).
