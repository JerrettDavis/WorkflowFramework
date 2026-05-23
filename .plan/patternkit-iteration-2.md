# PatternKit Adoption — Iteration 2 Plan

**Branch (target):** `feat/consume-patternkit-0.113+` (after PatternKit ships extensions)
**Date drafted:** 2026-05-22
**Author:** Claude Opus 4.7 (planning agent)
**Predecessor:** `feat/consume-patternkit-0.112` (PR #25) + `docs/patternkit-followup.md`

This plan resolves the 7 deferrals captured during the 0.112.0 adoption pass. For each
deferral we pick one of three paths:

- **A — Extend PatternKit:** add a new variant/overload upstream, then adopt in WF.
- **B — Change WF behavior to match PatternKit:** delete bespoke code, update tests with rationale.
- **C — Keep bespoke:** truly WF-specific glue; document the dead end.

User direction: **strongly prefer A or B**. C only when no cross-project value exists.

---

## Decision Matrix

| # | Step | Path | PatternKit work | WF work | Effort | Priority |
|---|------|------|-----------------|---------|--------|----------|
| 1 | NormalizerStep | **A** | Add `KeyedNormalizer<TKey,TRaw,TCanonical>` | Swap NormalizerStep to wrap it; keep dictionary contract intact | M | P1 |
| 2 | ContentEnricherStep | **C** | none | none (document why) | S | P3 |
| 3 | ClaimCheckStep / ClaimRetrieveStep | **A + B** | Add untyped-payload overload `IClaimCheckStore<object>` convenience adapter + `ClaimCheck` builder that accepts caller-supplied `claimId` and `MessageHeaders.Empty` | Migrate `WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore` to a thin facade over `PatternKit.IClaimCheckStore<object>`; swap steps | L | P2 |
| 4 | IdempotentReceiverStep | **B** | none | Switch from "set.Add before inner" to PatternKit semantics: claim → invoke → mark Completed/Failed; failure allows retry. Replace HashSet with `IIdempotencyStore` (default in-memory). Update characterization test `ReAttemptAfterExceptionIsSkipped` → `ReAttemptAfterExceptionIsAllowed` with rationale comment | M | P1 (bug fix) |
| 5 | PollingConsumerStep | **A** | Add `PollOnceAsync(handler, ct)` to `AsyncPollingConsumer<TPayload>` (single-shot equivalent of `RunAsync`) | Swap PollingConsumerStep to call `PollOnceAsync`; preserve `ResultKey` write contract | S | P1 |
| 6 | ScatterGatherStep | **A + B** | (no new PatternKit primitive required — 0.112 already has `AsyncScatterGather` with `CompletionStrategy.AllOrTimeout`) — but add a public `RecipientHandler` helper that accepts an `IStep` + `IWorkflowContext` adapter (lives in WF, not PK) | Refactor handler contract: each `IStep` returns its `__Result_{Name}` value as the typed response; aggregator wraps PatternKit envelopes; deprecate shared-context fan-out write path; UPDATE `ScatterGatherStepTests.cs` to assert envelope-based outputs (still surface `ResultsKey` for back-compat) | L | P2 |
| 7 | TransactionalOutboxStep | **A + B** | Add an untyped convenience: `IOutboxStore<object>` overloads + `OutboxMessage<object>` access pattern (already typed; just need clean adoption) | Migrate `WorkflowFramework.Extensions.Integration.Abstractions.IOutboxStore` to thin facade over `PatternKit.IOutboxStore<object>`; swap step; write `OutboxIdKey = result.Id` | M | P2 |

**Path tally:** A=2, B=1, A+B=3, C=1. **Dominant pattern: A+B combined adoptions** — the
WF Abstractions layer (`IClaimCheckStore`, `IOutboxStore`) needs to be re-rooted on PatternKit's
typed interfaces, after which the steps become trivial pass-throughs.

---

## Per-step Detail

### 1. NormalizerStep → Path A (extend PatternKit)

**Rationale.** Keyed string-discriminator dispatch is a legitimate, broadly useful variant
distinct from PatternKit's predicate-first design. Other projects that translate
format-tagged messages (CSV/JSON/Avro by header) need the same O(1) keyed dispatch.
Forcing every keyed lookup into a predicate chain is a real perf and ergonomics regression.

**API sketch.**

```csharp
namespace PatternKit.Messaging.Transformation;

public sealed class KeyedNormalizer<TKey, TRaw, TCanonical>
    where TKey : notnull
{
    public delegate TKey KeySelector(TRaw raw);
    public delegate ValueTask<TCanonical> AsyncNormalizerHandler(TRaw raw, CancellationToken ct);

    public static Builder Create(string name, KeySelector keySelector);

    public ValueTask<NormalizerResult<TCanonical>> NormalizeAsync(TRaw raw, CancellationToken ct = default);

    public sealed class Builder
    {
        public Builder For(TKey key, AsyncNormalizerHandler handler);
        public Builder Default(AsyncNormalizerHandler handler);
        // Optional: customise miss-reason formatter so WF test can pin format key in message
        public Builder OnMissReason(Func<TKey, string, string> formatter);
        public KeyedNormalizer<TKey, TRaw, TCanonical> Build();
    }
}
```

**Effort:** M (≈250 LOC + tests in PatternKit; ≈30 LOC change in WF).

**WF adoption:** `NormalizerStep` becomes a wrapper over `KeyedNormalizer<string, IWorkflowContext, IWorkflowContext>` with the format-detector as the `KeySelector` and a custom `OnMissReason` that yields the exact existing error string. Test `UnknownFormatNoDefaultThrows` passes without modification.

---

### 2. ContentEnricherStep → Path C (keep bespoke)

**Rationale.** `ContentEnricherStep` is a 3-line wrapper around `Func<IWorkflowContext, Task>`.
It exists only to give the delegate a `Name` and `IStep` shape so the workflow engine can
order/inspect it. There is no payload, no copy, no error policy — it is workflow-engine glue.
PatternKit's `AsyncContentEnricher<TPayload>` solves a different problem (functional payload
augmentation with per-step audit). Adopting it would force every WF user to invent a fake
payload type and add ~10 lines of builder setup to replace 3 lines.

**Why no Path A is worthwhile.** A new `AsyncSideEffectEnricher<TContext>` primitive would
essentially be "named delegate with Name property" — the abstraction adds no value beyond what
`IStep` itself already provides in WF. Other projects don't need it because they don't have
the `IStep`/`IWorkflowContext` constraint.

**Action:** add a `// Path C — kept bespoke per .plan/patternkit-iteration-2.md` header
comment to the source file. No code change. No PR.

---

### 3. ClaimCheckStep / ClaimRetrieveStep → Path A + B

**Rationale.** PatternKit's typed `IClaimCheckStore<TPayload>` is the better contract
(typed payload, headers for traceability, caller-supplied claim ID for determinism).
WF's untyped `IClaimCheckStore` is a legacy shape. We adopt PatternKit's interface as
the source of truth and provide a back-compat shim during the migration window.

**Path A — PatternKit extension.** Add an `IClaimCheckStore<object>` convenience overload
that accepts a null/empty `MessageHeaders` (`MessageHeaders.Empty` already exists). This
unblocks projects that don't yet have typed payloads.

**Path B — WF behavior change.**
- Migrate `WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore` to a thin
  facade interface that documents itself as a back-compat shim and delegates to
  `IClaimCheckStore<object>`.
- `ClaimCheckStep` and `ClaimRetrieveStep` consume `IClaimCheckStore<object>` directly.
- Claim IDs become deterministic: WF generates a stable `Guid.NewGuid().ToString("N")` and
  passes it as `claimId`, rather than receiving one back from the store.

**Tests to update.** `ClaimCheckStepTests.cs` — ID generation moves from the store to the
step; assertions on store-returned IDs become assertions on the value placed in
`ClaimTicketKey`. Same behavioral outcome.

**Back-compat.** Keep the old `IClaimCheckStore` interface deprecated (with `[Obsolete]`) for
one release. Provide an in-tree adapter `LegacyClaimCheckStoreAdapter` that implements
`IClaimCheckStore<object>` over the legacy untyped interface so existing integrations keep
working until they migrate.

**Effort:** L (interface migration + adapter + step rewrite + test updates).

---

### 4. IdempotentReceiverStep → Path B (change WF; PatternKit is correct)

**Rationale.** This is the textbook case for Path B — the bespoke "lock the ID at ingress,
permanently dedupe even on failure" behavior is a **latent bug**. A transient infrastructure
failure in the inner step locks the message ID forever; any retry (which is exactly what
idempotency-with-retry is supposed to enable) is silently swallowed. PatternKit's claim →
invoke → mark Completed/Failed semantics are the correct production behavior: dedupe a
*completed* attempt, allow retry of a *failed* attempt.

**What WF behavior changes.**
- Replace the private `HashSet<string> _processedIds` with an injected `IIdempotencyStore`
  (PatternKit interface). Provide an `InMemoryIdempotencyStore` default in the step's parameterless ctor for back-compat.
- Wrap the inner step invocation in a try/catch: on success → `MarkCompletedAsync(key, result: null)`; on exception → `MarkFailedAsync(key, ex.Message)`, then rethrow.
- A repeat call with the same key in `Failed` status returns `Claimed = false` and resets to `Processing` — the inner step IS invoked again.

**Tests to update.**
- Rename `ReAttemptAfterExceptionIsSkipped` → `ReAttemptAfterExceptionIsAllowed`.
- Assertion flips from "inner not called second time" to "inner IS called second time".
- Add a comment block on the test:
  > Behavior change rationale: the bespoke pre-call HashSet.Add was a latent bug. A
  > transient inner-step failure should not lock out future retries. PatternKit's
  > `IdempotentReceiver<TPayload,TResult>` provides the correct claim → invoke →
  > mark-Completed/Failed semantics. See `.plan/patternkit-iteration-2.md` §4.
- Keep `DuplicateAfterSuccessIsSkipped` exactly as-is (completed-status dedupe still works).

**Backward-compat.** SemVer minor bump for the WF extensions package. Release notes call this
out as a "fix" (it removes a silent dedup hazard). Consumers who depended on the bug-as-feature can
opt back in by injecting a custom `IIdempotencyStore` that treats Failed as "stay-locked".

**Effort:** M.

---

### 5. PollingConsumerStep → Path A (extend PatternKit)

**Rationale.** A single-shot poll is genuinely useful outside WF — any cron-tick consumer
(Azure Function, AWS Lambda, scheduled background job) wants to poll once, hand the result to a
handler, then exit. PatternKit's `AsyncPollingConsumer` already encapsulates the source
contract, jitter, and back-off compute; exposing a `PollOnceAsync` simply skips the loop.

**API sketch.**

```csharp
public sealed class AsyncPollingConsumer<TPayload>
{
    // Existing:
    public ValueTask RunAsync(AsyncMessageHandler handler, MessageContext? ctx, CancellationToken ct);

    // New (Path A addition):
    /// <summary>
    /// Performs a single poll cycle: invokes the source once, calls the handler if a message
    /// is produced, and returns. Does not loop, does not sleep, does not apply back-off.
    /// Useful when the consumer is driven externally (cron, workflow tick, test).
    /// </summary>
    public ValueTask<bool> PollOnceAsync(
        AsyncMessageHandler handler,
        MessageContext? context = null,
        CancellationToken cancellationToken = default);
}
```

Returns `true` if a message was processed, `false` if the poll was empty. WF maps the result
to its `ResultKey` context property (preserving the IReadOnlyList contract by wrapping the
single message in a list — or, better, generalising `PollOnceAsync` to surface the
raw message and letting WF wrap it).

**Effort:** S (≈40 LOC in PatternKit + 4 tests; ≈15 LOC change in WF).

**Lifecycle clarification.** WF's `IPollingSource<T>` returns `IReadOnlyList<T>` (batch). The
adoption either (a) keeps WF's `IPollingSource<T>` and uses `PollOnceAsync` as scaffolding only,
or (b) introduces a second `IPollingSource<T>.PollOneAsync()` overload in WF Abstractions for
the single-message contract. Defer (b) to a follow-up — (a) gets the adoption done.

---

### 6. ScatterGatherStep → Path A + B

**Rationale.** PatternKit's `AsyncScatterGather` with `CompletionStrategy.AllOrTimeout` and
`ResponseEnvelope<TResponse>` already provides the timeout, per-branch error isolation, and
partial-result semantics WF needs. What WF *also* needs is to stop using shared-context
mutation as the result transport — that pattern is a concurrency hazard. The right answer is to
adopt PatternKit AND refactor handlers to return typed values.

**Path A — small PatternKit-side helper.** No new core primitive required. Add a small docs
sample showing the `IStep`-as-recipient pattern; if requested, add a `WorkflowRecipientAdapter`
extension class (in PatternKit or a new `PatternKit.Workflow.Adapters` package) that wraps an
`IStep` + result-key into an `AsyncRecipientHandler`. Optional; the wrap can live in WF.

**Path B — WF behavior change.**
- New `ScatterGatherStep` recipient contract: each recipient is a `(string name, Func<IWorkflowContext, CancellationToken, ValueTask<object?>>)` pair rather than an `IStep` that writes to a shared key.
- Step builds `AsyncScatterGather<IWorkflowContext, object?, IReadOnlyList<object?>>` with `CompletionStrategy.AllOrTimeout(timeout)`.
- Aggregator unpacks envelopes (`envelopes.Where(e => e.Succeeded).Select(e => e.Response)`) into the `ResultsKey` list — preserving the public output contract.
- The `__Result_{handler.Name}` convention is deprecated. Tests that pin those individual keys
  are rewritten to assert against the envelope list or the aggregated `ResultsKey`.

**Tests to update.** `ScatterGatherStepTests.cs` — every scenario that reads
`context.Properties[$"__Result_{name}"]` is rewritten to read the envelope list. Behavior
identical from the workflow author's perspective (the aggregated `ResultsKey` is unchanged); the
internal handler-output transport changes.

**Backward-compat.** Major bump for the recipient contract (this is a public API change in WF).
Provide an obsoleted overload accepting `IEnumerable<IStep>` that internally adapts via an
`IStep`-to-recipient bridge for one release, then remove.

**Effort:** L (recipient contract migration + test rewrite + adapter + docs).

---

### 7. TransactionalOutboxStep → Path A + B

**Rationale.** Same story as ClaimCheck (#3). PatternKit's typed `IOutboxStore<TPayload>` +
`OutboxMessage<TPayload>` is the right contract (typed payload, dispatched/attempt tracking,
deterministic IDs). WF's `IOutboxStore` predates it and was untyped. Adopt PatternKit as the
source of truth.

**Path A — PatternKit extension.** Likely unnecessary: `IOutboxStore<object>` already works
with `Message<object>`. If WF integrations need it, add a small `OutboxStoreExtensions.EnqueuePayloadAsync(this IOutboxStore<TPayload> store, TPayload payload, …)`
overload that constructs the `Message` envelope internally. ≈10 LOC.

**Path B — WF behavior change.**
- Re-root `WorkflowFramework.Extensions.Integration.Abstractions.IOutboxStore` as a thin
  facade over `IOutboxStore<object>` (mirroring ClaimCheck approach).
- `TransactionalOutboxStep` consumes `IOutboxStore<object>` directly:

  ```csharp
  var msg = new Message<object>(payload, MessageHeaders.Empty);
  var record = await _store.EnqueueAsync(msg, id: null, createdAt: null, ct);
  context.Properties[OutboxIdKey] = record.Id;
  ```
- Provide `LegacyOutboxStoreAdapter : IOutboxStore<object>` over the deprecated
  `WF.IOutboxStore` for one release window.

**Tests to update.** `TransactionalOutboxStepTests.cs` — `OutboxIdKey` is now sourced from
`record.Id` rather than `SaveAsync`'s return — same value, different code path. Existing
assertions on the stored payload need a wrapping change (`Payload` → `Message.Payload`).

**Backward-compat.** Minor bump with `[Obsolete]` on the legacy interface; remove next major.

**Effort:** M.

---

## Execution Sequence

Three sequential phases. Within each phase, work is parallelizable across multiple PRs.

### Phase 1 — PatternKit extensions (one upstream PR, blocked on PatternKit maintainer)

Single PR titled **"Add keyed normalizer + single-shot polling consumer + outbox payload convenience"**:

1. `KeyedNormalizer<TKey,TRaw,TCanonical>` + tests (NormalizerStep adoption unblock)
2. `AsyncPollingConsumer<TPayload>.PollOnceAsync` + tests (PollingConsumerStep adoption unblock)
3. `OutboxStoreExtensions.EnqueuePayloadAsync` convenience + tests (TransactionalOutboxStep ergonomics)

Ship as `PatternKit 0.113.0`. ScatterGather and ClaimCheck need NO upstream changes —
PatternKit 0.112.0 already has the required surface; only WF-side changes are needed.

**Effort total:** M+S+S = ≈400 LOC + tests in one PatternKit PR.

### Phase 2 — WF refactor PR #1: drop-in adoptions (after 0.113.0 ships)

Single WF PR titled **"feat: adopt PatternKit 0.113 — NormalizerStep / PollingConsumerStep / IdempotentReceiverStep"**:

- NormalizerStep → KeyedNormalizer (no test changes)
- PollingConsumerStep → PollOnceAsync (no test changes)
- IdempotentReceiverStep → IdempotentReceiver behavior fix (test rename + assertion flip with documented rationale)

These three are independent and the behavioral change (IdempotentReceiver) is isolated to its
own characterization test set. Ship as one PR for atomic review.

### Phase 3 — WF refactor PR #2: interface migrations (larger, separate review)

Single WF PR titled **"feat!: migrate IClaimCheckStore / IOutboxStore / ScatterGather recipient contract to PatternKit"** (major bump):

- ClaimCheckStep / ClaimRetrieveStep → `IClaimCheckStore<object>` + legacy adapter
- TransactionalOutboxStep → `IOutboxStore<object>` + legacy adapter
- ScatterGatherStep → typed recipient contract + envelope-based aggregation + legacy `IStep` adapter

These three share the same shape (untyped → typed abstraction migration + legacy adapter
+ test rewrite) and benefit from being reviewed together.

### Phase 4 — Path C documentation (no PR; comment-only)

Add a header comment to `ContentEnricherStep.cs` linking to this plan and explaining the
"workflow-engine glue, no broader pattern" reasoning. Update `docs/patternkit-adoption.md` to
move ContentEnricherStep from "Future Evaluation Targets" to "Intentionally Bespoke" with
the Path C rationale.

---

## Summary Of Outcomes (post-plan)

| Step | After Iteration 2 |
|------|-------------------|
| NormalizerStep | Wraps `KeyedNormalizer<string,IWorkflowContext,IWorkflowContext>` |
| ContentEnricherStep | Bespoke, documented Path C |
| ClaimCheckStep / ClaimRetrieveStep | Consume `IClaimCheckStore<object>`; legacy adapter retained 1 release |
| IdempotentReceiverStep | Wraps `IdempotentReceiver<…>`; bug fixed; characterization test rewritten |
| PollingConsumerStep | Wraps `AsyncPollingConsumer<T>.PollOnceAsync` |
| ScatterGatherStep | Wraps `AsyncScatterGather<…>` with `AllOrTimeout` strategy; typed recipient contract |
| TransactionalOutboxStep | Consumes `IOutboxStore<object>`; legacy adapter retained 1 release |

**Bespoke code deleted (estimate):** ~280 LOC across the 6 adopted steps.
**Latent bugs fixed:** 1 (IdempotentReceiver dedup-on-failure).
**Legacy adapters added:** 2 (IClaimCheckStore, IOutboxStore) — removable next major.
**Tests rewritten (count):** ~6 scenarios across 3 test files (IdempotentReceiver, ScatterGather, Outbox).
