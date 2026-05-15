using PulseStack.Abstractions.Tools;

namespace PulseStack.Abstractions.Agents;

public sealed class PipelineContext
{
    public string Input { get; set; }
        = string.Empty;

    public string CurrentOutput { get; set; }
        = string.Empty;

    public IDictionary<string, object?> Items
        { get; } =
            new Dictionary<string, object?>();

    public IList<PipelineStepResult> Steps
        { get; } =
            new List<PipelineStepResult>();    

    public IList<ToolExecutionRecord> ToolResults
        { get; } =
            new List<ToolExecutionRecord>();                    
}