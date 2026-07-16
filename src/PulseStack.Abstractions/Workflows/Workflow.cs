using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Builders;

namespace PulseStack.Abstractions.Workflows;

public sealed class Workflow : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public WorkflowIdentity Identity { get; }

    public WorkflowStepId Id { get; }

    public WorkflowDefinition Definition { get; }

    public string Name
        => Definition.Name;

    public IReadOnlyList<IWorkflowStep> Steps => _steps;

    IReadOnlyList<IWorkflowStep> IWorkflowStep.Children => Steps;

    public Workflow(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Identity = WorkflowIdentity.Create();

        Id = WorkflowStepId.New();

        Definition = new WorkflowDefinition(name);
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