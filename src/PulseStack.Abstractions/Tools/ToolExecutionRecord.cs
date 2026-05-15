namespace PulseStack.Abstractions.Tools;

public sealed record ToolExecutionRecord(
    string ToolName,
    string Input,
    ToolExecutionResult Result);