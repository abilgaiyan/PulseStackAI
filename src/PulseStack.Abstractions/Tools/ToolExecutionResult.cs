namespace PulseStack.Abstractions.Tools;

public sealed record ToolExecutionResult(
    bool Success,
    string Output,
    string? Error = null);