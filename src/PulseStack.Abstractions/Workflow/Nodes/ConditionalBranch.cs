using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflow.Conditions;

namespace PulseStack.Abstractions.Workflow.Nodes;

public sealed class ConditionalBranch
{
    public required ICondition Condition { get; init; }

    public required IReadOnlyList<IAgent> TrueAgents { get; init; }

    public IReadOnlyList<IAgent> FalseAgents { get; init; }
        = [];
}
