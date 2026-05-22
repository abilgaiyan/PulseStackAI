using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Errors;


namespace PulseStack.Agents.Runtime;

internal sealed class PipelineExecutionState
{
    public string FinalOutput { get; init; }
        = string.Empty;

    public IReadOnlyList<PipelineStepResult> Steps { get; init; }
        = [];

    public IReadOnlyList<PipelineExecutionError> Errors { get; init; }
        = [];
}
