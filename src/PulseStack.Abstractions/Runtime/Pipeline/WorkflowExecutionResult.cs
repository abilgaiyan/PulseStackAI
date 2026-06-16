namespace PulseStack.Abstractions.Runtime.Pipeline;
public sealed class WorkflowExecutionResult 
{
    public bool Success { get; init; }

    public string FinalOutput { get; init; } =
        string.Empty;

    public IReadOnlyList<NodeExecutionResult> Nodes
    {
        get;
        init;
    }
    = [];
}