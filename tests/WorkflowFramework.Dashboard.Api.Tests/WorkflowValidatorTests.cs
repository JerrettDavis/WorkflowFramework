using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
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

    [Fact]
    public void CanvasWithMissingSourceNode_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Canvas Flow",
            Steps = [new StepDefinitionDto { Name = "Step 1", Type = "action" }],
            Canvas = new WorkflowCanvasDto
            {
                Nodes =
                [
                    new WorkflowCanvasNodeDto { Id = "node_1", Type = "Action", Label = "Step 1" }
                ],
                Edges =
                [
                    new WorkflowCanvasEdgeDto { Id = "edge_1", Source = "missing", Target = "node_1" }
                ]
            }
        };

        var result = _validator.Validate(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("missing source node", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanvasWithMultipleIncomingEdgesToRealStep_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Canvas Flow",
            Steps =
            [
                new StepDefinitionDto { Name = "Step 1", Type = "action" },
                new StepDefinitionDto { Name = "Step 2", Type = "action" },
                new StepDefinitionDto { Name = "Step 3", Type = "action" }
            ],
            Canvas = new WorkflowCanvasDto
            {
                Nodes =
                [
                    new WorkflowCanvasNodeDto { Id = "node_1", Type = "Action", Label = "Step 1" },
                    new WorkflowCanvasNodeDto { Id = "node_2", Type = "Action", Label = "Step 2" },
                    new WorkflowCanvasNodeDto { Id = "node_3", Type = "Action", Label = "Step 3" }
                ],
                Edges =
                [
                    new WorkflowCanvasEdgeDto { Id = "edge_1", Source = "node_1", Target = "node_3" },
                    new WorkflowCanvasEdgeDto { Id = "edge_2", Source = "node_2", Target = "node_3" }
                ]
            }
        };

        var result = _validator.Validate(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("multiple incoming edges", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanvasWithUnsupportedConditionalHandle_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Canvas Flow",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Decide",
                    Type = "conditional",
                    Then = new StepDefinitionDto { Name = "Then Step", Type = "action" }
                }
            ],
            Canvas = new WorkflowCanvasDto
            {
                Nodes =
                [
                    new WorkflowCanvasNodeDto { Id = "node_1", Type = "Conditional", Label = "Decide" },
                    new WorkflowCanvasNodeDto { Id = "node_2", Type = "Action", Label = "Then Step" }
                ],
                Edges =
                [
                    new WorkflowCanvasEdgeDto { Id = "edge_1", Source = "node_1", Target = "node_2", Kind = "body" }
                ]
            }
        };

        var result = _validator.Validate(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("unsupported 'body' output", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanvasWithDuplicateHandleConnections_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Canvas Flow",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Decide",
                    Type = "conditional",
                    Then = new StepDefinitionDto { Name = "Approve", Type = "action" }
                },
                new StepDefinitionDto { Name = "Reject", Type = "action" }
            ],
            Canvas = new WorkflowCanvasDto
            {
                Nodes =
                [
                    new WorkflowCanvasNodeDto { Id = "node_1", Type = "Conditional", Label = "Decide" },
                    new WorkflowCanvasNodeDto { Id = "node_2", Type = "Action", Label = "Approve" },
                    new WorkflowCanvasNodeDto { Id = "node_3", Type = "Action", Label = "Reject" }
                ],
                Edges =
                [
                    new WorkflowCanvasEdgeDto { Id = "edge_1", Source = "node_1", Target = "node_2", Kind = "then" },
                    new WorkflowCanvasEdgeDto { Id = "edge_2", Source = "node_1", Target = "node_3", Kind = "then" }
                ]
            }
        };

        var result = _validator.Validate(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("multiple connections on its 'then' output", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SubWorkflowReferencingCurrentWorkflow_ReturnsError()
    {
        var def = new WorkflowDefinitionDto
        {
            Name = "Parent Flow",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Run Child",
                    Type = "SubWorkflow",
                    SubWorkflowName = "Parent Flow"
                }
            ]
        };

        var result = _validator.Validate(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("cannot reference the current workflow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StoredSubWorkflowReferenceCycle_ReturnsError()
    {
        var parent = new WorkflowDefinitionDto
        {
            Name = "Parent Flow",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Run Child",
                    Type = "SubWorkflow",
                    SubWorkflowName = "Child Flow"
                }
            ]
        };

        var knownWorkflows = new List<SavedWorkflowDefinition>
        {
            new()
            {
                Id = "parent-id",
                Definition = parent
            },
            new()
            {
                Id = "child-id",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Child Flow",
                    Steps =
                    [
                        new StepDefinitionDto
                        {
                            Name = "Loop Back",
                            Type = "SubWorkflow",
                            SubWorkflowName = "Parent Flow"
                        }
                    ]
                }
            }
        };

        var result = _validator.Validate(parent, knownWorkflows, "parent-id");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("reference cycle detected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StoredMissingSubWorkflowReference_ReturnsWarning()
    {
        var parent = new WorkflowDefinitionDto
        {
            Name = "Parent Flow",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Run Child",
                    Type = "SubWorkflow",
                    SubWorkflowName = "Missing Flow"
                }
            ]
        };

        var knownWorkflows = new List<SavedWorkflowDefinition>
        {
            new()
            {
                Id = "parent-id",
                Definition = parent
            }
        };

        var result = _validator.Validate(parent, knownWorkflows, "parent-id");

        result.IsValid.Should().BeTrue();
        result.WarningCount.Should().BeGreaterThan(0);
        result.Errors.Should().Contain(e => e.Message.Contains("references missing workflow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RenamedDraftStillDetectsReachableSubWorkflowCycle()
    {
        var renamedDraft = new WorkflowDefinitionDto
        {
            Name = "Renamed Parent",
            Steps =
            [
                new StepDefinitionDto
                {
                    Name = "Run Child One",
                    Type = "SubWorkflow",
                    SubWorkflowName = "Child One"
                }
            ]
        };

        var knownWorkflows = new List<SavedWorkflowDefinition>
        {
            new()
            {
                Id = "parent-id",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Original Parent",
                    Steps =
                    [
                        new StepDefinitionDto
                        {
                            Name = "Old Child",
                            Type = "SubWorkflow",
                            SubWorkflowName = "Child One"
                        }
                    ]
                }
            },
            new()
            {
                Id = "child-one-id",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Child One",
                    Steps =
                    [
                        new StepDefinitionDto
                        {
                            Name = "Run Child Two",
                            Type = "SubWorkflow",
                            SubWorkflowName = "Child Two"
                        }
                    ]
                }
            },
            new()
            {
                Id = "child-two-id",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Child Two",
                    Steps =
                    [
                        new StepDefinitionDto
                        {
                            Name = "Loop Child One",
                            Type = "SubWorkflow",
                            SubWorkflowName = "Child One"
                        }
                    ]
                }
            }
        };

        var result = _validator.Validate(renamedDraft, knownWorkflows, "parent-id");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("reference cycle detected", StringComparison.OrdinalIgnoreCase));
    }
}
