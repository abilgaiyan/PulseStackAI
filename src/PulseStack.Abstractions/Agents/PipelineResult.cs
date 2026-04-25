namespace PulseStack.Abstractions.Agents;

public sealed record PipelineResult(
    string FinalOutput,
    IReadOnlyList<PipelineStepResult> Steps);
