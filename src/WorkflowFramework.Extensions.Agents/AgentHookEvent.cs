namespace WorkflowFramework.Extensions.Agents;

public enum AgentHookEvent
{
    WorkflowStarting,
    PreAgentPrompt,
    PreToolCall,
    PostToolCall,
    PostToolCallFailure,
    StepCompleted,
    SubWorkflowStarting,
    SubWorkflowCompleted,
    PreCompact,
    PostCompact,
    Checkpoint,
    Notification,
    WorkflowCompleted
}
