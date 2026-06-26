using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class WorkflowBuilder
{
    private readonly WorkflowPipeline _workflow;

    internal WorkflowBuilder(
        string name)
    {
        _workflow =
            new WorkflowPipeline(
                name);
    }

    public WorkflowBuilder Run(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _workflow.Add(agent);

        return this;
    }

    public WorkflowBuilder Workflow(
        WorkflowPipeline workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        _workflow.Add(workflow);

        return this;
    }

    public WorkflowPipeline Build()
    {
        return _workflow;
    }
}
