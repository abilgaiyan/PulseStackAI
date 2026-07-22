using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows.Steps; 

public sealed class RunStep : IWorkflowStep
{
    public WorkflowStepId Id { get; }

    public string Name => Agent.Name;

    public IAgent Agent { get; }

    public IReadOnlyList<IWorkflowStep> Children => [];

    public RunStep(IAgent agent)
        : this(WorkflowStepId.New(), agent)
    {
    }

    public RunStep(
        WorkflowStepId id,
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        Id = id;
        Agent = agent;
    }
}