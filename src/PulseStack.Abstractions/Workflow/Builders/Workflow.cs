
namespace PulseStack.Abstractions.Workflow.Builders;

public static class Workflow
{
    public static WorkflowBuilder Create(
        string name)
    {
        return new WorkflowBuilder(
            name);
    }
}
