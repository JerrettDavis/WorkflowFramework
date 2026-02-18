using FluentAssertions;
using WorkflowFramework.Validation;
using Xunit;

namespace WorkflowFramework.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidationResult_Success()
    {
        var result = ValidationResult.Success();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_Failure()
    {
        var result = ValidationResult.Failure(
            new ValidationError("Field required", "StepA"),
            new ValidationError("Invalid type"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].StepName.Should().Be("StepA");
        result.Errors[0].Message.Should().Be("Field required");
        result.Errors[1].StepName.Should().BeNull();
    }

    [Fact]
    public void ValidationError_ToString_IncludesStepName()
    {
        var error = new ValidationError("Bad input", "MyStep");
        error.ToString().Should().Be("[MyStep] Bad input");
    }

    [Fact]
    public void ValidationError_ToString_NoStepName()
    {
        var error = new ValidationError("Bad input");
        error.ToString().Should().Be("Bad input");
    }
}
