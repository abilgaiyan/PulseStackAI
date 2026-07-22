using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Builders;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows;

public sealed class Workflow : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public WorkflowIdentity Identity { get; init; }

    public WorkflowStepId Id { get; init; }

    public WorkflowDefinition Definition { get; init; }

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

    public Workflow(
        WorkflowIdentity identity,
        WorkflowStepId id,
        WorkflowDefinition definition)
    {
        Identity = identity;
        Id = id;
        Definition = definition;
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