namespace PulseStack.Abstractions.Common.Identity;

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
