using FluentAssertions;
using WorkflowFramework.Tests.Common;
using WorkflowFramework.Validation;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class ValidationCoreTests
{
    [Fact]
    public async Task DefaultValidator_NullWorkflow_Throws()
    {
        var validator = new DefaultWorkflowValidator();
        var act = () => validator.ValidateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DefaultValidator_NoSteps_ReturnsInvalid()
    {
        var validator = new DefaultWorkflowValidator();
        var wf = Workflow.Create("empty").Build();
        var result = await validator.ValidateAsync(wf);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("at least one step"));
    }

    [Fact]
    public async Task DefaultValidator_ValidWorkflow_ReturnsValid()
    {
        var validator = new DefaultWorkflowValidator();
        var wf = Workflow.Create("valid").Step(new TrackingStep("A")).Build();
        var result = await validator.ValidateAsync(wf);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task DefaultValidator_DuplicateStepNames_ReturnsInvalid()
    {
        var validator = new DefaultWorkflowValidator();
        var wf = Workflow.Create("dup")
            .Step(new TrackingStep("Same"))
            .Step(new TrackingStep("Same"))
            .Build();
        var result = await validator.ValidateAsync(wf);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public void ValidationResult_Success()
    {
        var result = ValidationResult.Success();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_Failure_Params()
    {
        var result = ValidationResult.Failure(new ValidationError("err1"), new ValidationError("err2"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void ValidationResult_Failure_List()
    {
        var errors = new List<ValidationError> { new("msg", "step1") };
        var result = ValidationResult.Failure(errors);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void ValidationError_ToString_WithStepName()
    {
        var error = new ValidationError("some error", "Step1");
        error.ToString().Should().Be("[Step1] some error");
    }

    [Fact]
    public void ValidationError_ToString_WithoutStepName()
    {
        var error = new ValidationError("some error");
        error.ToString().Should().Be("some error");
        error.StepName.Should().BeNull();
    }
}
