using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class LoopStep
    : IWorkflowStep
{
    public string Name { get; }

    public Func<PipelineContext, IEnumerable<object>> Items { get; }

    public IWorkflowStep Step { get; }

    public IReadOnlyList<IWorkflowStep> Children => [Step];

    public LoopStep(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IWorkflowStep step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(step);

        Name = name;
        Items = items;
        Step = step;
    }
}
