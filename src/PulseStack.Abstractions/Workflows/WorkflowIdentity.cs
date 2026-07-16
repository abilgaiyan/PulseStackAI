namespace PulseStack.Abstractions.Workflows;

public sealed record WorkflowIdentity(
    WorkflowId Id,
    string Version)
{
    public static WorkflowIdentity Create(
        string version = "1.0.0")
        => new(
            WorkflowId.New(),
            version);
}
