using  PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Routing;

namespace PulseStack.Abstractions.Workflows;

public sealed class SwitchStep
    : IWorkflowStep
{
    public WorkflowStepId Id { get; } = WorkflowStepId.New();
    public string Name { get; }

    public Func<PipelineContext, string?> Selector { get; }

    public IReadOnlyList<SwitchCase> Cases { get; }

    public IWorkflowStep? DefaultStep { get; }

    public IReadOnlyList<IWorkflowStep> Children =>
        Cases
            .Select(c => c.Step)
            .Concat(DefaultStep is null
                ? Enumerable.Empty<IWorkflowStep>()
                : [DefaultStep])
            .ToList();

    public SwitchStep(
        string name,
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IWorkflowStep? defaultStep = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(cases);

        Name = name;
        Selector = selector;
        Cases = cases.ToList();
        DefaultStep = defaultStep;
    }
}