using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public sealed class NodeExecutionResult
{
    public string NodeName { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? Output { get; init; }

    public AIUsage? Usage { get; init; }
}