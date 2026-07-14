using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflows.Steps;
public sealed class ParallelStep : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public string Name { get; }

    public IReadOnlyList<IWorkflowStep> Steps
        => _steps;

    public IReadOnlyList<IWorkflowStep> Children => Steps;

    public ParallelStep(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public ParallelStep Add(
        IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        _steps.Add(step);

        return this;
    }

    public ParallelStep Add(
        IAgent agent)
    {
        return Add(new RunStep(agent));
    }
}
