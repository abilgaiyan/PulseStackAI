using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Common.Identity;
using PulseStack.Abstractions.Workflows.Builders;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows;

public sealed class Workflow : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public WorkflowIdentity Identity { get; }

    public WorkflowStepId Id { get; }

    public string Name { get; }

    public IReadOnlyList<IWorkflowStep> Steps => _steps;

    IReadOnlyList<IWorkflowStep> IWorkflowStep.Children => Steps;

    public Workflow(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Identity = WorkflowIdentity.Create();

        Id = WorkflowStepId.New();

        Name = name;
    }

    public Workflow Add(
        IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        _steps.Add(step);

        return this;
    }

    public Workflow Add(
        IAgent agent)
    {
        return Add(new RunStep(agent));
    }

    public static WorkflowBuilder Create(
        string name)
    {
        return new WorkflowBuilder(name);
    }
}