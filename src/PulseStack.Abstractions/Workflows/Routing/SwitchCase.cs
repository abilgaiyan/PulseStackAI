using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows.Routing;

public sealed class SwitchCase
{
    public string Value { get; }

    public IWorkflowStep Step { get; }

    public SwitchCase(
        string value,
        IWorkflowStep step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(step);

        Value = value;
        Step = step;
    }

    public SwitchCase(
        string value,
        IAgent agent)
        : this(value, new RunStep(agent))
    {
    }
}
