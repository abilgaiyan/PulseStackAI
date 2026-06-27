using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class WorkflowBuilder
{
    private readonly WorkflowPipeline _workflow;

    internal WorkflowBuilder(
        string name)
    {
         ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _workflow =
            new WorkflowPipeline(
                name);
    }

    private WorkflowBuilder AddNode(
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _workflow.Add(node);

        return this;
    }

    public WorkflowBuilder Run(
        IAgent agent)
    {
       return AddNode(agent);
    }

    public WorkflowBuilder Workflow(
        WorkflowPipeline workflow)
    {
       return AddNode(workflow);
    }

    public WorkflowPipeline Build()
    {
        return _workflow;
    }
}
