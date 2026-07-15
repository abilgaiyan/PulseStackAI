using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Common.Identity;

namespace PulseStack.Abstractions.Workflows.Steps; 

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