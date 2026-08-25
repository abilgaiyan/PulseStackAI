using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class ParallelStep : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public WorkflowStepId Id { get; }

    public string Name { get; }

    public IReadOnlyList<IWorkflowStep> Steps
        => _steps;

    public IReadOnlyList<IWorkflowStep> Children => Steps;

    public ParallelStep(
        string name)
        : this(
            WorkflowStepId.New(),
            name)
    {
    }

    public ParallelStep(
        WorkflowStepId id,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
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
