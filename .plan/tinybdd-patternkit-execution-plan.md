# TinyBDD Coverage + PatternKit Adoption — Phased Execution Plan

**Repo:** `C:\git\WorkflowFramework` (GitHub: `jerrettdavis/WorkflowFramework`)
**Companion:** `C:\git\PatternKit` (GitHub: `jerrettdavis/PatternKit`, NuGet: `PatternKit`)
**Working branch:** `feat/tinybdd-coverage`
**End state:** All phases merged green to `main` via independent PRs (or an explicit stacked series).

---

## Cross-Cutting Rules

These rules apply to **every** phase. Violating any of them is a blocker for merge.

1. **One phase = one PR.** Each phase must be independently mergeable. The next phase may not start until the previous PR is green on CI and merged (or explicitly stacked).
2. **Public API is frozen.** PatternKit adoption is internal-only — wrap, don't replace. Any change to public surface needs a separate API-evolution PR. If a public type would shift, fall back to an internal helper.
3. **TinyBDD style is mandatory for new tests.** `[Scenario("...")] [Fact]` on `async` methods. Inherit `WorkflowFramework.Tests.TinyBDD.Support.TinyBddTestBase(ITestOutputHelper)`. Use `Given(...).When(...).Then(...)` and the shared `ScenarioExpect` helper. xUnit `[Fact]/[Theory]` alone is not acceptable for new coverage.
4. **Multi-target green.** Tests run on `net8.0`, `net9.0`, `net10.0` and must pass on all three on CI (`.github/workflows/ci.yml`).
5. **Conventional commits** per `memstack.md`: `feat(phase-X): ...`, `test(phase-X): ...`, `refactor(phase-X): ...`, `chore(phase-X): ...`. Scope = phase ID.
6. **No `--no-verify`.** No skipping pre-commit hooks, no bypassing CI checks, no force pushes to `main`/`feat/tinybdd-coverage` shared base.
7. **Coverage may not regress.** Every PR must hold or improve line coverage on the project(s) it touches. CI gate enforces `codecov.yml` thresholds.
8. **Behavioral equivalence first.** When swapping a bespoke implementation for a PatternKit-backed one, the *first* commit in that phase must be a TinyBDD scenario set that pins current behavior. The *second* commit does the swap. Diff = green-on-both-sides.
9. **No PatternKit prerelease pins.** Use stable NuGet versions only. Pin the version in `Directory.Packages.props`, not in individual `.csproj` files.
10. **Compensation paths get explicit scenarios.** Any state-machine or saga adoption must include a failing-step → compensate path in TinyBDD coverage, not just the happy path.

---

## Quick Wins (Do First — Land Each Within One Small PR)

These are independent, one-PR-each warmups that build muscle memory for the bigger phases and visibly raise the project count before Phase D begins. Order is loose; any can be done in parallel by separate sessions.

### QW-1 — Add `Extensions.Polly` test project skeleton
- **Scope:** Create `tests/WorkflowFramework.Extensions.Polly.Tests/` with `.csproj` mirroring an existing test project (e.g., `WorkflowFramework.Tests.TinyBDD.csproj`), reference `src/WorkflowFramework.Extensions.Polly`, add one smoke scenario for `ResilienceMiddleware` and one for `WorkflowBuilderPollyExtensions`.
- **Tests:** ~6 scenarios.
- **Done:** Project compiles on net8/9/10, green in CI.

### QW-2 — `Specification` for `DefaultWorkflowValidator`
- **Scope:** Add `PatternKit` package ref to `src/WorkflowFramework`. Introduce an internal `WorkflowSpec` (composed of `HasAtLeastOneStep`, `NoDuplicateStepNames`) using `PatternKit.Core.Behavioral` Specification pattern (or fall back to a plain composable `IWorkflowRule` if Specification isn't there). `DefaultWorkflowValidator` delegates to it. Public API unchanged.
- **Tests:** Add `tests/WorkflowFramework.Tests.TinyBDD/Core/Validation/DefaultWorkflowValidatorScenarios.cs` — ~8 scenarios covering empty steps, duplicate names (case-insensitive), success path, null guard.
- **Done:** Coverage on `src/WorkflowFramework/Validation/` ≥ 95%, CI green.

### QW-3 — `StateMachine` pilot for `WorkflowStatus` transitions
- **Scope:** Add an internal `WorkflowStatusMachine` in `src/WorkflowFramework/Internal/` using PatternKit `Behavioral.State.StateMachine<TState,TEvent>`. Allowed transitions: `Pending→Running`, `Running→Completed`, `Running→Faulted`, `Running→Compensated` (via compensation event), `Running→Aborted`, `Running→Suspended`, `Suspended→Running`. Wire it as an *advisory* observer of existing `WorkflowEngine` transitions — engine remains authoritative.
- **Tests:** `tests/WorkflowFramework.Tests.TinyBDD/Core/WorkflowStatusMachineScenarios.cs` — ~12 scenarios covering legal and illegal transitions, idempotent terminal states.
- **Done:** No public API change; advisory layer can be removed in one revert if PatternKit StateMachine doesn't fit. CI green.

### QW-4 — `Extensions.Reactive` test project skeleton
- **Scope:** Create `tests/WorkflowFramework.Extensions.Reactive.Tests/` with TinyBDD style from-the-start. Cover `IAsyncStep` (single happy path, single fault, cancellation). 1 file, ~5 scenarios.
- **Done:** Project compiles + green, multi-target.

### QW-5 — `Extensions.Expressions` test project skeleton
- **Scope:** Create `tests/WorkflowFramework.Extensions.Expressions.Tests/`. Cover `SimpleExpressionEvaluator` (literals, simple arithmetic, variable lookup, missing variable), `TemplateEngine` (substitution, escaping), `ExpressionBuilderExtensions` (wires into builder). ~10 scenarios.
- **Done:** Project compiles + green, multi-target. Establishes baseline for Phase F PatternKit `Interpreter` adoption.

**Cumulative Quick-Wins payoff:** +4 projects under TinyBDD coverage, ~41 new scenarios, first PatternKit reference landed, no public API churn.

---

## Phase D — Core Engine BDD Coverage *(HIGHEST PRIORITY)*

### Goal
Reach near-complete TinyBDD coverage of the workflow execution path (`src/WorkflowFramework/`) so subsequent PatternKit adoptions have a behavioral safety net.

### Scope (src targets — already present, need coverage)
- `src/WorkflowFramework/WorkflowEngine.cs`
- `src/WorkflowFramework/Builder/WorkflowBuilder.cs`
- `src/WorkflowFramework/Builder/WorkflowBuilder{TData}.cs`
- `src/WorkflowFramework/Builder/WorkflowBuilderExtensions.cs`
- `src/WorkflowFramework/Builder/IConditionalBuilder.cs` + `IParallelBuilder.cs`
- `src/WorkflowFramework/Internal/ConditionalStep.cs`
- `src/WorkflowFramework/Internal/LoopSteps.cs`
- `src/WorkflowFramework/Internal/ParallelStep.cs`
- `src/WorkflowFramework/Internal/DelegateStep.cs`
- `src/WorkflowFramework/Internal/TypedStepAdapter.cs` + `TypedWorkflowAdapter.cs`
- `src/WorkflowFramework/Pipeline/Pipeline.cs` + `IPipelineStep.cs` (+ a PipelineBuilder if it exists; add scenarios if found)
- `src/WorkflowFramework/Registry/WorkflowRegistry.cs`
- `src/WorkflowFramework/Checkpointing/WorkflowResumeEngine.cs` (fill the gaps left by the partial coverage)
- `src/WorkflowFramework/Triggers/CronExpression.cs`, `TriggerSourceFactory.cs`, `WorkflowTriggerService.cs`

### Test files to add (under `tests/WorkflowFramework.Tests.TinyBDD/Core/`)
- `Engine/WorkflowEngineScenarios.cs` — happy path, middleware chain order, event raise order, compensation on failure, abort token, cancellation, OperationCanceledException pass-through, multiple errors aggregation.
- `Builder/WorkflowBuilderScenarios.cs` — step add ordering, naming, duplicate detection, typed data flow.
- `Builder/WorkflowBuilderExtensionsScenarios.cs` — fluent helpers (`.AddStep<T>()`, conditional wiring, parallel wiring).
- `Internal/ConditionalStepScenarios.cs` — true/false branch, missing branch, predicate exception, nested conditional.
- `Internal/LoopStepsScenarios.cs` — count-loop, while-loop, do-while, break, max-iteration safety.
- `Internal/ParallelStepScenarios.cs` — all-success, partial-failure aggregation, cancellation propagation.
- `Internal/DelegateStepScenarios.cs` — sync delegate, async delegate, throwing delegate.
- `Internal/TypedAdapterScenarios.cs` — typed step round-trip, typed workflow round-trip.
- `Pipeline/PipelineScenarios.cs` — single-step, multi-step, short-circuit, error.
- `Registry/WorkflowRegistryScenarios.cs` — register, lookup, duplicate, missing.
- `Checkpointing/WorkflowResumeEngineScenarios.cs` — resume from checkpoint, missing checkpoint, replay-skipping completed steps, compensation on resume failure.
- `Triggers/CronExpressionScenarios.cs` — common cron expressions, edge cases (leap year, DST window), invalid expression rejection.
- `Triggers/TriggerSourceFactoryScenarios.cs`, `Triggers/WorkflowTriggerServiceScenarios.cs`.

### Estimated test count
**~95 new scenarios** across ~14 files.

### PatternKit adoptions in this phase
**None.** Phase D is purely characterization. Adoptions land in Phase F+.

### Dependencies
- Quick Wins QW-1 through QW-5 merged (optional but recommended — they validate the test-project scaffolding pattern).
- Otherwise depends only on `main`.

### Done criteria
- +90–100 new TinyBDD scenarios green on net8/9/10.
- Coverage on `src/WorkflowFramework/` rises from current baseline (estimated ~60%) to **≥ 90% line, ≥ 85% branch**.
- `tests/WorkflowFramework.Tests.TinyBDD/` totals **≥ 250 scenarios** (current 164 + Quick Wins + Phase D).
- `.github/workflows/ci.yml` green on PR head commit.
- No public API symbol added or removed (verify with API-diff tool or manual `grep` over public surface).

### Risk / rollback
- **Risk:** `CronExpression` parsing may have subtle DST/timezone bugs surfaced by new scenarios. **Mitigation:** treat newly-found bugs as separate fix PRs; mark the failing scenario `[Scenario(Skip="bug #N")]` only if blocking, with a tracking issue.
- **Risk:** `WorkflowResumeEngine` scenarios may require a fake checkpoint store. **Mitigation:** reuse `InMemoryWorkflowCheckpointStore` or add a minimal test-only fake under `tests/WorkflowFramework.Tests.TinyBDD/Support/`.
- **Rollback:** Tests are additive — revert the PR commit, no source changes to undo.

---

## Phase E — Resurrect Missing Test Projects with TinyBDD-from-Start

### Goal
Close the four "zero tests at all" gaps so every extension shipped today has at least baseline BDD coverage. These are the highest-risk projects because they have no behavioral net.

### Scope
For each of the following, create a new TinyBDD-style test project under `tests/`:

1. **`tests/WorkflowFramework.Extensions.Polly.Tests/`** *(already started in QW-1; this phase rounds it out)*
   - `ResilienceMiddlewareScenarios.cs` — retry on transient, circuit-break, fallback, timeout.
   - `WorkflowBuilderPollyExtensionsScenarios.cs` — fluent retry wiring, named pipeline lookup.
2. **`tests/WorkflowFramework.Extensions.Reactive.Tests/`** *(extends QW-4)*
   - `IAsyncStepScenarios.cs` — happy, fault, cancel, backpressure (if applicable).
3. **`tests/WorkflowFramework.Extensions.Expressions.Tests/`** *(extends QW-5)*
   - `SimpleExpressionEvaluatorScenarios.cs` — full grammar coverage.
   - `TemplateEngineScenarios.cs` — substitution, escaping, missing-token policy.
   - `ExpressionBuilderExtensionsScenarios.cs` — DI wiring, evaluator selection.
4. **`tests/WorkflowFramework.Extensions.Distributed.Redis.Tests/`**
   - `RedisDistributedLockScenarios.cs` — acquire, release, timeout, contention. Use `Testcontainers.Redis` (already a likely dep — verify in `Directory.Packages.props`) or `StackExchange.Redis` test harness.
   - `RedisWorkflowQueueScenarios.cs` — enqueue, dequeue, FIFO order, visibility timeout.

### Estimated test count
**~35–40 scenarios** across ~7 files (in addition to Quick Wins).

### PatternKit adoptions
**None directly.** Lays groundwork for Phase F pilot (`RetryPolicy` wrapping in Polly extension) and for Phase G `Interpreter` adoption in Expressions.

### Dependencies
- Quick Wins (provide skeleton structure for 3 of the 4 projects).
- Phase D can run in parallel — no source overlap.

### Done criteria
- 4 previously-untested projects each have a passing test project on net8/9/10.
- Each project reaches **≥ 70% line coverage** (these are smaller projects, so 70% is reasonable in one PR; raise in Phase I).
- CI matrix includes the new projects.
- README test-project list updated.

### Risk / rollback
- **Risk:** Redis tests require a container — CI runner may not have Docker. **Mitigation:** gate Redis container-based scenarios behind a `[Trait("Category","Integration")]` filter; ship at least one non-container scenario (lock semantics via in-process fake) so net8/9/10 unit lanes always run something. If CI doesn't support containers at all, mark integration lane optional.
- **Risk:** Polly v8 API surface may have shifted from v7 references in `ResilienceMiddleware`. **Mitigation:** verify `Polly` package version in `Directory.Packages.props` before writing scenarios.
- **Rollback:** Per-project revert; each test project is its own folder.

---

## Phase F — PatternKit Foundation + Pilot Adoption

### Goal
Land PatternKit as a first-class internal dependency with **one** real adoption proving the wrap-don't-replace pattern, and lock the API-stability invariant.

### Scope
- **`Directory.Packages.props`**: add `<PackageVersion Include="PatternKit" Version="<stable>" />`. Source: confirm via `dotnet list package` or NuGet.org once a stable version is pinned.
- **`src/WorkflowFramework/WorkflowFramework.csproj`**: reference `PatternKit`.
- **`src/WorkflowFramework.Extensions.Polly/WorkflowFramework.Extensions.Polly.csproj`**: reference `PatternKit`.
- **Pilot adoption** — pick exactly **one** of the two below (recommend A; B is fallback if A hits friction):
  - **A. `StateMachine` for `WorkflowStatus`** *(promote QW-3 to authoritative)*: move `WorkflowStatusMachine` from advisory to in-engine. `WorkflowEngine` uses the machine to gate transitions; rejected transitions throw `InvalidWorkflowStateException`. All current paths in `WorkflowEngine.cs` get a corresponding `Fire(event)` call.
  - **B. `RetryPolicy` wrapping `IStep.ExecuteAsync` in Polly extension**: `ResilienceMiddleware` delegates retry/backoff to `PatternKit.Core.Behavioral.RetryPolicy` (verify exact namespace). Existing Polly v8 `ResiliencePipeline` remains the outer envelope; PatternKit handles the per-step retry decision tree.
- **Pilot tests**: `Core/PatternKit/StateMachinePilotScenarios.cs` (option A) or `Extensions.Polly.Tests/PatternKitRetryScenarios.cs` (option B). ~15 scenarios pinning the wrapped behavior.

### Estimated test count
**~15 new scenarios** plus the existing Phase D + Phase E test count must remain green (regression gate).

### PatternKit adoptions
- One pilot (A or B above).
- Foundation: package referenced from 2 projects, **public API unchanged**.

### Dependencies
- **Phase D merged** (need engine coverage in place before touching engine internals for option A).
- Quick Win QW-3 merged if option A is chosen.
- Phase E merged if option B is chosen (need Polly test project baseline).

### Done criteria
- PatternKit referenced in `Directory.Packages.props` at a pinned stable version.
- One pilot adoption merged with paired TinyBDD scenarios.
- Pre-adoption tests still pass (behavioral equivalence proven).
- Public API diff = empty.
- CI green on net8/9/10.

### Risk / rollback
- **Risk:** PatternKit `StateMachine<TState,TEvent>` may not expose a hook for the compensation transition (`Running → Compensated` via a saga rollback event). **Mitigation:** if so, restrict the machine to happy-path transitions and leave compensation as direct status assignment. Document the limitation in `src/WorkflowFramework/Internal/WorkflowStatusMachine.cs` header.
- **Risk:** PatternKit `RetryPolicy` semantics differ from Polly's (e.g., jitter, backoff base). **Mitigation:** characterization tests first — capture exact current behavior, then prove PatternKit replicates.
- **Rollback:** Drop the pilot adoption commit, keep the package reference. Pre-adoption tests stay green.

---

## Phase G — EIP Integration Step Replacement (PatternKit-Backed)

### Goal
Replace bespoke `Extensions.Integration` EIP step implementations with PatternKit Enterprise Integration backers. Each replacement requires a TinyBDD scenario set proving behavioral equivalence *before* the swap.

### Scope — src (24 EIP step classes confirmed)

**Routing** (`src/WorkflowFramework.Extensions.Integration/Routing/`)
- `ContentBasedRouterStep.cs`, `DynamicRouterStep.cs`, `MessageFilterStep.cs`, `RecipientListStep.cs`, `RoutingSlipStep.cs`

**Composition** (`Composition/`)
- `AggregatorStep.cs`, `ComposedMessageProcessorStep.cs`, `ProcessManagerStep.cs`, `ResequencerStep.cs`, `ScatterGatherStep.cs`, `SplitterStep.cs`

**Channel** (`Channel/`)
- `ChannelAdapterStep.cs`, `DeadLetterStep.cs`, `MessageBridgeStep.cs`, `WireTapStep.cs`

**Endpoint** (`Endpoint/`)
- `IdempotentReceiverStep.cs`, `PollingConsumerStep.cs`, `TransactionalOutboxStep.cs`

**Transformation** (`Transformation/`)
- `ClaimCheckStep.cs`, `ContentEnricherStep.cs`, `ContentFilterStep.cs`, `MessageTranslatorStep.cs`, `NormalizerStep.cs`

**Builder** (`Builder/`)
- `IntegrationBuilderExtensions.cs` — adapts wiring to PatternKit-backed steps where the type changes internally.

### Scope — tests (under `tests/WorkflowFramework.Tests.TinyBDD/Integration/` — already has 3 files; expand to one per step)
Add or extend (existing files are noted in **bold**):
- **`RoutingSlipStepScenarios.cs`** (rename current `RoutingSlipStepTests.cs` to scenario style if not already)
- **`ScatterGatherStepScenarios.cs`**
- **`ContentBasedRouterStepScenarios.cs`**
- `DynamicRouterStepScenarios.cs`, `MessageFilterStepScenarios.cs`, `RecipientListStepScenarios.cs`
- `AggregatorStepScenarios.cs`, `ComposedMessageProcessorStepScenarios.cs`, `ProcessManagerStepScenarios.cs`, `ResequencerStepScenarios.cs`, `SplitterStepScenarios.cs`
- `ChannelAdapterStepScenarios.cs`, `DeadLetterStepScenarios.cs`, `MessageBridgeStepScenarios.cs`, `WireTapStepScenarios.cs`
- `IdempotentReceiverStepScenarios.cs`, `PollingConsumerStepScenarios.cs`, `TransactionalOutboxStepScenarios.cs`
- `ClaimCheckStepScenarios.cs`, `ContentEnricherStepScenarios.cs`, `ContentFilterStepScenarios.cs`, `MessageTranslatorStepScenarios.cs`, `NormalizerStepScenarios.cs`
- `IntegrationBuilderExtensionsScenarios.cs`

### Estimated test count
**~120–150 new scenarios** (5–7 per step × ~24 steps).

### PatternKit adoptions (each verified to exist under `src/PatternKit.Core/EnterpriseIntegration/`)
- `RoutingSlipStep` → `PatternKit.Core.Behavioral.Chain` or dedicated `AsyncRoutingSlip` if exposed.
- `ScatterGatherStep` → PatternKit ScatterGather.
- `AggregatorStep` → PatternKit Aggregator.
- `SplitterStep` → PatternKit Splitter.
- `WireTapStep` → PatternKit WireTap.
- `RecipientListStep` → PatternKit RecipientList.
- `ContentBasedRouterStep`/`DynamicRouterStep` → PatternKit `Strategy` or content-based router primitive.
- `MessageFilterStep` → PatternKit `Specification` filter.
- `ProcessManagerStep` → PatternKit `AsyncSaga` (if present).
- `MessageTranslatorStep`/`NormalizerStep`/`ContentEnricherStep`/`ContentFilterStep` → PatternKit `Adapter`/`Decorator`.
- `ChannelAdapterStep`/`MessageBridgeStep` → PatternKit `Adapter`.
- `DeadLetterStep`/`IdempotentReceiverStep`/`PollingConsumerStep`/`TransactionalOutboxStep` → may not have direct PatternKit analogues; keep bespoke but add scenarios.
- `ClaimCheckStep` → PatternKit `Proxy`/`Flyweight` or bespoke.
- `ComposedMessageProcessorStep`/`ResequencerStep` → likely bespoke; add scenarios only.

**Sub-PR strategy:** Group steps by pattern (Routing PR, Composition PR, Channel PR, Endpoint PR, Transformation PR) — 5 sub-PRs, each independently mergeable.

### Dependencies
- **Phase D** (engine coverage in place — EIP steps invoke engine internals via `IStep`).
- **Phase F** (PatternKit package reference and pilot adoption pattern established).

### Done criteria
- All 24 EIP step classes have a TinyBDD scenario file with ≥ 5 scenarios each.
- At least 12 of 24 steps have a PatternKit-backed internal implementation; the rest are documented as "intentionally bespoke" in their file header.
- Coverage on `src/WorkflowFramework.Extensions.Integration/` ≥ 90%.
- Public API on `Extensions.Integration` unchanged (verified via API diff).
- CI green on each sub-PR.

### Risk / rollback
- **Risk:** A PatternKit primitive lacks a hook the bespoke step needed (e.g., `RoutingSlipStep` may emit custom telemetry events not exposed by `AsyncRoutingSlip`). **Mitigation:** wrap PatternKit primitive in a thin adapter that surfaces the missing event; if adapter is too heavy, keep bespoke and just add scenarios. Document the decision in the step file.
- **Risk:** Behavioral differences in error aggregation (Aggregator, ScatterGather) — PatternKit may aggregate differently from current bespoke logic. **Mitigation:** characterization scenarios *first*; if a difference is found, treat as a behavior decision, not a bug — document and choose explicitly which side wins.
- **Rollback:** Each sub-PR is reverted independently. Tests stay (additive); only the implementation swap reverts.

---

## Phase H — Long-Tail Extensions (Coverage Sweep)

### Goal
Bring every remaining extension project under TinyBDD coverage at a baseline level. Order chosen by user-facing surface area and risk.

### Scope (priority order — each gets its own sub-PR or grouped sub-PR)

**H.1 — Hosting & DI (foundational for everything else)**
- `src/WorkflowFramework.Extensions.Hosting/` → `tests/WorkflowFramework.Extensions.Hosting.Tests/`
- `src/WorkflowFramework.Extensions.DependencyInjection/` → augment existing tests if any; otherwise add TinyBDD project.

**H.2 — Http & Connectors (external surface)**
- `src/WorkflowFramework.Extensions.Http/` → new test project, TinyBDD scenarios for request/response, retry, error mapping.
- `src/WorkflowFramework.Extensions.Connectors.Abstractions/`, `Connectors.Grpc/`, `Connectors.Messaging/` → new test projects.

**H.3 — Events & Diagnostics (observability)**
- `src/WorkflowFramework.Extensions.Events/` → new test project.
- `src/WorkflowFramework.Extensions.Diagnostics/` → new test project (OpenTelemetry/activity scenarios).

**H.4 — Persistence backends (5 projects)**
- `src/WorkflowFramework.Extensions.Persistence/` (abstractions) → new test project.
- `src/WorkflowFramework.Extensions.Persistence.InMemory/` → new test project.
- `src/WorkflowFramework.Extensions.Persistence.EntityFramework/` → new test project.
- `src/WorkflowFramework.Extensions.Persistence.PostgreSQL/` → new test project; Testcontainers gate.
- `src/WorkflowFramework.Extensions.Persistence.SqlServer/` → new test project; Testcontainers gate.
- `src/WorkflowFramework.Extensions.Persistence.Sqlite/` → new test project.

**H.5 — Distributed backends**
- `src/WorkflowFramework.Extensions.Distributed/` (abstractions) → new test project.
- `src/WorkflowFramework.Extensions.Distributed.PostgreSQL/` → new test project; Testcontainers.
  *(Redis covered in Phase E.)*

**H.6 — HumanTasks**
- `src/WorkflowFramework.Extensions.HumanTasks/` → new test project.

**H.7 — Agents & AI**
- `src/WorkflowFramework.Extensions.Agents/`, `Agents.Mcp/`, `Agents.Skills/` → new test projects.
- `src/WorkflowFramework.Extensions.AI/` → new test project.

**H.8 — Configuration, Scheduling, Plugins, Visualization, DataMapping**
- `src/WorkflowFramework.Extensions.Configuration/` → new test project.
- `src/WorkflowFramework.Extensions.Scheduling/` → augment existing if any; otherwise add TinyBDD project.
- `src/WorkflowFramework.Extensions.Plugins/` → new test project; **also** evaluate `Strategy`/`AbstractFactory` PatternKit adoption here.
- `src/WorkflowFramework.Extensions.Visualization/` → new test project (rendering scenarios, mermaid-style output verification).
- `src/WorkflowFramework.Extensions.DataMapping/`, `DataMapping.Formats/`, `DataMapping.Schema/` → new test projects.

### Test files
~25 new test projects, ~6–10 scenarios per src class, ~300+ scenarios total.

### Estimated test count
**~300–400 new scenarios** across ~25 new test projects.

### PatternKit adoptions
- `Extensions.Plugins` → `Strategy` + `AbstractFactory` for plugin loading.
- `Extensions.AI` / `Extensions.Agents` → potential `TypeDispatcher` for message routing.
- `Extensions.Visualization` → no adoption likely.
- All others: opportunistic — only adopt where it's clean.

### Dependencies
- **Phase D** (engine baseline).
- **Phase F** (PatternKit reference pattern).
- H.4 and H.5 partially block on Testcontainers availability in CI.

### Done criteria
- All 42 src projects have at least one test project under `tests/` (gap = zero).
- Every test project compiles + green on net8/9/10.
- Aggregate TinyBDD scenario count **≥ 600**.
- CI matrix includes every new test project.
- README updated with full test-project list.

### Risk / rollback
- **Risk:** Testcontainers-dependent test projects may fail on locked-down CI runners. **Mitigation:** classify all integration tests with `[Trait("Category","Integration")]` and `dotnet test --filter Category!=Integration` in main lane; run integration lane on a separate workflow allowed to use Docker.
- **Risk:** Some extensions (AI, Agents.Mcp) may need fakes for external services. **Mitigation:** ship in-memory fakes under `tests/.../Support/` per-project.
- **Rollback:** Per-sub-PR revert; each project is independent.

---

## Phase I — Coverage Tightening & Documentation

### Goal
Hit the project-wide coverage target, surface coverage publicly, and document the testing pattern so future contributors stay consistent.

### Scope
- Run `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura` across the solution.
- Generate coverage report (`coverage-report/` already exists in repo — reuse).
- Identify projects below **95% line / 90% branch** and add targeted scenarios.
- Add or update coverage badge in `README.md` (Codecov badge — `codecov.yml` already present).
- Update `tests/WorkflowFramework.Tests.TinyBDD/README.md` with the canonical scenario template.
- Add a `.github/workflows/coverage.yml` (if not present) that uploads to Codecov and fails the PR below threshold.
- Document the PatternKit adoption inventory in `docs/` — which steps use which PatternKit primitive, which are intentionally bespoke.

### Test files
~50–100 targeted "gap-filling" scenarios across already-existing test projects (no new projects expected at this point).

### Estimated test count
**~50–100 new scenarios** (gap-fillers).

### PatternKit adoptions
**None new** — this phase is consolidation.

### Dependencies
- Phases D, E, F, G, H all merged.

### Done criteria
- Project-wide line coverage **≥ 95%**, branch **≥ 88%** (the slightly lower branch threshold acknowledges hard-to-reach catch blocks in EF/cancellation paths).
- Coverage badge live on README and rendering green on `main`.
- `tests/WorkflowFramework.Tests.TinyBDD/README.md` documents the scenario template.
- `docs/patternkit-adoption.md` lists every adoption point.
- CI green; codecov gate enforced.

### Risk / rollback
- **Risk:** Coverage tools (coverlet) may not see all targets across multi-target `net8/9/10` matrix — counts diverge. **Mitigation:** publish per-TFM reports; if a TFM is consistently lower, treat as separate sub-task.
- **Rollback:** Coverage configuration is metadata; revert is trivial.

---

## Recommended Execution Order

1. **Quick Wins QW-1 through QW-5** (parallelizable, one PR each — ~1 week elapsed).
2. **Phase D** — Core engine BDD coverage (single largish PR or 2–3 sub-PRs by area: Engine/Builder, Internal-Steps, Pipeline+Registry+Triggers).
3. **Phase E** — Resurrect missing test projects (one PR per project; QW already started 3 of 4).
4. **Phase F** — PatternKit foundation + pilot (one PR, depends on D; option A recommended).
5. **Phase G** — EIP integration step replacement (5 sub-PRs grouped by pattern family).
6. **Phase H** — Long-tail extensions (sub-PRs H.1 → H.8 in priority order; H.1, H.2, H.3 can stack; H.4 and H.5 may block on container CI).
7. **Phase I** — Coverage tightening + docs (one PR).

**Parallelization windows:**
- Quick Wins are all parallelizable from each other.
- Within Phase D, Engine and Triggers can be split into parallel sub-PRs.
- Within Phase G, the 5 pattern-family sub-PRs are mutually independent (different folders, different test files).
- Within Phase H, H.6, H.7, H.8 are mutually independent of each other and of H.4/H.5.

---

## Summary Table

| Phase | New scenarios | New test projects | PatternKit adoptions | Blocks |
|-------|---------------|-------------------|----------------------|--------|
| QW-1..5 | ~41 | 3 | 1 (Specification), 1 (StateMachine advisory) | — |
| D | ~95 | 0 (extends TinyBDD) | 0 | QW (recommended) |
| E | ~35 (beyond QW) | 1 (Redis; rest extend QW) | 0 | QW |
| F | ~15 | 0 | 1 pilot (StateMachine OR RetryPolicy) | D |
| G | ~120–150 | 0 (extends TinyBDD) | ~12 EIP primitives | D, F |
| H | ~300–400 | ~25 | 2–4 (Plugins Strategy, Agents TypeDispatcher, etc.) | D, F |
| I | ~50–100 | 0 | 0 | D, E, F, G, H |
| **Total** | **~650–840** | **~29** | **~16–19** | — |
