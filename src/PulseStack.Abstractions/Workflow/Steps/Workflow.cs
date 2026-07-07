using PulseStack.Abstractions.Workflow.Builders;

namespace PulseStack.Abstractions.Workflow.Steps;
public sealed class Workflow : IWorkflowStep
{
    private readonly List<IWorkflowStep> _steps = [];

    public string Name { get; }

    public IReadOnlyList<IWorkflowStep> Steps => _steps;

    IReadOnlyList<IWorkflowStep> IWorkflowStep.Children => Steps;
        
    public Workflow(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }
    
    public Workflow Add(
        IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        _steps.Add(step);

        return this;
    }

    public static WorkflowBuilder Create(
        string name)
    {
        return new WorkflowBuilder(
            name);
    }
}
