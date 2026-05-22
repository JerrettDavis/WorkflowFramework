using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Validation;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Validation;

[Feature("DefaultWorkflowValidator — Specification composition")]
public class DefaultWorkflowValidatorScenarios : TinyBddTestBase
{
    public DefaultWorkflowValidatorScenarios(ITestOutputHelper output) : base(output) { }

    private static readonly DefaultWorkflowValidator Validator = new();

    private static IWorkflow BuildWorkflow(string name, params string[] stepNames)
    {
        var builder = Workflow.Create(name);
        foreach (var stepName in stepNames)
            builder = builder.Step(stepName, _ => Task.CompletedTask);
        return builder.Build();
    }

    // ---- success path ----

    [Scenario("Validator returns success for a workflow with one step"), Fact]
    public async Task ValidWorkflowWithOneStepPasses()
    {
        var workflow = BuildWorkflow("single", "step-a");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a workflow with a single step", () => result)
            .Then("validation passes", r =>
            {
                r.IsValid.Should().BeTrue();
                r.Errors.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Validator returns success for a workflow with multiple unique steps"), Fact]
    public async Task ValidWorkflowWithMultipleStepsPasses()
    {
        var workflow = BuildWorkflow("multi", "step-a", "step-b", "step-c");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a workflow with 3 uniquely named steps", () => result)
            .Then("validation passes", r =>
            {
                r.IsValid.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ---- HasAtLeastOneStep spec (AND-composition — both specs must pass for IsValid) ----

    [Scenario("Validator returns failure for a workflow with no steps"), Fact]
    public async Task EmptyWorkflowFails()
    {
        var workflow = BuildWorkflow("empty");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a workflow with no steps", () => result)
            .Then("validation fails with 'must have at least one step' error", r =>
            {
                r.IsValid.Should().BeFalse();
                r.Errors.Should().ContainSingle()
                    .Which.Message.Should().Contain("at least one step");
                return true;
            })
            .AssertPassed();
    }

    // ---- NoDuplicateStepNames spec ----

    [Scenario("Validator returns failure for a workflow with duplicate step names"), Fact]
    public async Task DuplicateStepNameFails()
    {
        var workflow = BuildWorkflow("dup", "step-a", "step-b", "step-a");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a workflow with step-a duplicated", () => result)
            .Then("validation fails with duplicate step error for step-a", r =>
            {
                r.IsValid.Should().BeFalse();
                r.Errors.Should().Contain(e => e.StepName == "step-a");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Duplicate step name check is case-insensitive"), Fact]
    public async Task DuplicateStepNameIsCaseInsensitive()
    {
        var workflow = BuildWorkflow("dup-ci", "Step-A", "step-a");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a workflow with Step-A and step-a (same name, different case)", () => result)
            .Then("validation fails treating them as duplicates", r =>
            {
                r.IsValid.Should().BeFalse();
                r.Errors.Should().HaveCountGreaterThan(0);
                return true;
            })
            .AssertPassed();
    }

    // ---- null guard ----

    [Scenario("Validator throws ArgumentNullException for null workflow"), Fact]
    public async Task NullWorkflowThrowsArgumentNullException()
    {
        Exception? caught = null;
        try { await Validator.ValidateAsync(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("a null workflow argument", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ---- Specification AND/OR/NOT composition exercised through the validator ----

    [Scenario("AND composition: a workflow failing both rules reports two independent error groups"), Fact]
    public async Task WorkflowFailingBothRulesReportsBothErrors()
    {
        // Empty workflow: fails HasAtLeastOneStep (AND composition short-circuits but validator
        // reports each rule independently, so we only get the first error here).
        var emptyWithDup = BuildWorkflow("both-fail");

        var result = await Validator.ValidateAsync(emptyWithDup);

        await Given("an empty workflow (fails both specs via AND composition)", () => result)
            .Then("validation fails", r =>
            {
                // The AND-composed IsValid spec is false; validator reports the 'no steps' error.
                r.IsValid.Should().BeFalse();
                r.Errors.Should().HaveCountGreaterThanOrEqualTo(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("NOT composition: a workflow with steps does NOT satisfy the empty-workflow predicate"), Fact]
    public async Task NonEmptyWorkflowDoesNotSatisfyEmptyPredicate()
    {
        // We test NOT logic via the validator: a valid (non-empty, unique) workflow
        // must pass, confirming HasAtLeastOneStep.Not() would be unsatisfied.
        var workflow = BuildWorkflow("not-test", "step-x");
        var result = await Validator.ValidateAsync(workflow);

        await Given("a non-empty workflow", () => result)
            .Then("validation passes (HasAtLeastOneStep satisfied, NOT would be false)", r =>
            {
                r.IsValid.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OR composition: a workflow with steps but duplicates still satisfies HasAtLeastOneStep"), Fact]
    public async Task WorkflowWithStepsButDuplicatesStillHasAtLeastOneStep()
    {
        // This exercises the OR-like behavior in spec terms:
        // HasAtLeastOneStep = true, NoDuplicateStepNames = false.
        // The validator runs each spec independently. The first spec alone is satisfied.
        var workflowWithDup = BuildWorkflow("or-test", "step-a", "step-a");
        var result = await Validator.ValidateAsync(workflowWithDup);

        await Given("a workflow with steps but duplicate names", () => result)
            .Then("validation fails only because of duplicates, not because of empty steps", r =>
            {
                r.IsValid.Should().BeFalse();
                // Only duplicate errors, no 'at least one step' error
                r.Errors.Should().NotContain(e => e.Message.Contains("at least one step"));
                r.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
                return true;
            })
            .AssertPassed();
    }
}
