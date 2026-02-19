using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Tests;

public sealed class WorkflowValidatorTests
{
    private readonly WorkflowValidator _validator = new();

    [Fact]
    public void EmptyName_ReturnsError()
    {
        var def = new WorkflowDefinitionDto { Name = "", Steps = [new StepDefinitionDto { Name = "s1", Type = "action" }] };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("name"));
    }

    [Fact]
    public void NoSteps_ReturnsError()
    {
        var def = new WorkflowDefinitionDto { Name = "Test", Steps = [] };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("at least one step"));
    }

    [Fact]
    public void ValidWorkflow_Passes()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "Step1", Type = "action" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
    }

    [Fact]
    public void StepWithoutName_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "", Type = "action" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Step must have a name"));
    }

    [Fact]
    public void StepWithoutType_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "s1", Type = "" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UnknownStepType_ReturnsWarning()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "s1", Type = "unknownType" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeTrue(); // warnings don't block
        result.WarningCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DuplicateStepNames_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps =
            [
                new StepDefinitionDto { Name = "Dup", Type = "action" },
                new StepDefinitionDto { Name = "Dup", Type = "action" }
            ]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void ConditionalWithoutThen_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "cond", Type = "conditional" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("then"));
    }

    [Fact]
    public void ConditionalWithoutElse_ReturnsWarning()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto
            {
                Name = "cond", Type = "conditional",
                Then = new StepDefinitionDto { Name = "thenStep", Type = "action" }
            }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeTrue();
        result.WarningCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RetryWithZeroAttempts_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "r", Type = "retry", MaxAttempts = 0 }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("maxAttempts"));
    }

    [Fact]
    public void TimeoutWithZeroSeconds_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "t", Type = "timeout", TimeoutSeconds = 0 }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("timeoutSeconds"));
    }

    [Fact]
    public void TimeoutWithoutInner_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "t", Type = "timeout", TimeoutSeconds = 30 }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("inner step"));
    }

    [Fact]
    public void TryCatchWithoutBody_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "tc", Type = "tryCatch" }]
        };
        var result = _validator.Validate(def);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("try body"));
    }

    [Fact]
    public void ParallelWithNoChildren_ReturnsWarning()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Test",
            Steps = [new StepDefinitionDto { Name = "p", Type = "parallel" }]
        };
        var result = _validator.Validate(def);
        result.WarningCount.Should().BeGreaterThan(0);
    }
}
