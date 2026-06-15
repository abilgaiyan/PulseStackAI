
namespace PulseStack.Abstractions.Agents.Routing;

public sealed record AgentSelectionResult(
    IAgent Agent,
    string? Reason = null);