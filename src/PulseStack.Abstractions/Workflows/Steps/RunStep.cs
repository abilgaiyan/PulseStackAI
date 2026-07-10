using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows.Steps; 

public sealed class RunStep : IWorkflowStep
{
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