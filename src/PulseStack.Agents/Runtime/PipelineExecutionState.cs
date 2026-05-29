using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Agents.Runtime;

internal sealed class PipelineExecutionState
{
    public string FinalOutput { get; init; }
        = string.Empty;

    public IReadOnlyList<PipelineStepResult> Steps { get; init; }
        = [];

    public IReadOnlyList<PipelineExecutionError> Errors { get; init; }
        = [];

    public AIUsage TotalUsage { get; init; }
        = new();
}
