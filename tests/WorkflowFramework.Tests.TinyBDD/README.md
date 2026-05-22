# WorkflowFramework.Tests.TinyBDD

Code-first fluent BDD scenarios via [TinyBDD](https://github.com/JerrettDavis/TinyBDD).

## Purpose

This project hosts Given/When/Then scenarios written in C# using TinyBDD's fluent API.
It coexists with Reqnroll (used for Gherkin file-based acceptance tests in
`WorkflowFramework.Extensions.Approvals.Acceptance`). Choose TinyBDD here when you want
code-first, strongly-typed scenarios without maintaining separate `.feature` files.

## Folder structure

Each top-level folder mirrors the source area under test:

| Folder | Source area |
|--------|------------|
| `Core/Checkpointing/` | `WorkflowFramework` — checkpoint/resume behaviour |
| `Core/Triggers/` | `WorkflowFramework` — trigger evaluation |
| `Core/Testing/` | `WorkflowFramework.Testing` — harness helpers |
| `DataMapping/` | `WorkflowFramework.Extensions.DataMapping` |
| `Agents/` | `WorkflowFramework.Extensions.Agents` |
| `Plugins/` | `WorkflowFramework.Extensions.Plugins` |
| `Scheduling/` | `WorkflowFramework.Extensions.Scheduling` |
| `DI/` | `WorkflowFramework.Extensions.DependencyInjection` |
| `Integration/` | `WorkflowFramework.Extensions.Integration` |
| `Smoke/` | Framework wiring smoke test (do not delete) |

## Writing scenarios

Inherit from `TinyBddTestBase` (in `Support/`) so the constructor boilerplate is
handled once:

```csharp
[Feature("My feature")]
public class MyFeatureTests : TinyBddTestBase
{
    public MyFeatureTests(ITestOutputHelper output) : base(output) { }

    [Scenario("does the thing"), Fact]
    public async Task DoesTheThing() =>
        await Given("starting state", () => CreateSut())
            .When("action", sut => sut.DoThing())
            .Then("result", result => result.IsSuccess)
            .AssertPassed();
}
```

For explicit context (e.g., when you need the `ScenarioContext` handle):

```csharp
var ctx = Bdd.CreateContext(this);
await Bdd.Given(ctx, "state", () => ...)
         .When(...)
         .Then(...)
         .AssertPassed();
```

Theory/InlineData works alongside `[Scenario]`:

```csharp
[Scenario("handles multiple inputs"), Theory]
[InlineData(1, 2, 3)]
[InlineData(5, 5, 10)]
public async Task HandlesMultipleInputs(int a, int b, int expected) =>
    await Given("inputs", () => (a, b))
        .When("sum", t => t.a + t.b)
        .Then("correct", sum => sum == expected)
        .AssertPassed();
```

## Phase brief for follow-up agents

- **B1 — Core** (`Core/Checkpointing/`, `Core/Triggers/`, `Core/Testing/`): scenarios
  covering checkpoint/resume, trigger evaluation, and WorkflowFramework.Testing helpers.
- **B2 — DataMapping** (`DataMapping/`): scenarios for mapping transforms, format
  converters, and schema validation from `WorkflowFramework.Extensions.DataMapping`.
- **B3 — Extensions** (`Agents/`, `Plugins/`, `Scheduling/`, `DI/`, `Integration/`):
  scenarios for agents, plugins, scheduling, DI registration helpers, and integration
  connectors.

All three phases use the same `TinyBddTestBase` pattern established here.
Add new source `ProjectReference` entries to the `.csproj` only if the source project
is not already referenced — avoid duplicates.
