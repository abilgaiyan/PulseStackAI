using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows; 

public sealed class RunStep : IWorkflowStep
{
    public WorkflowStepId Id { get; } = WorkflowStepId.New();
    public string Name => Agent.Name;

    public IAgent Agent { get; }

    public IReadOnlyList<IWorkflowStep> Children =>
        [];

    public RunStep(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        Agent = agent;
    }
}