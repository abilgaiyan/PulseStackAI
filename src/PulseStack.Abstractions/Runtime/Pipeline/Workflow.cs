namespace PulseStack.Abstractions.Runtime.Pipeline;

public static class Workflow
{
    public static WorkflowBuilder Create(
        string name)
    {
        return new WorkflowBuilder(
            name);
    }
}
