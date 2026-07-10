using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows.Conditions;

public sealed class ConditionalBranch
{
    public required ICondition Condition { get; init; }

    public required IReadOnlyList<IAgent> TrueAgents { get; init; }

    public IReadOnlyList<IAgent> FalseAgents { get; init; }
        = [];
}
