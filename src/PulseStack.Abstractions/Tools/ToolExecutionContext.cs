using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Tools;
public sealed class ToolExecutionContext
{
    public Guid ExecutionId { get; init; }
        = Guid.NewGuid();

    public string ToolName { get; init; }
        = string.Empty;

    public object? Input { get; init; }

    public IServiceProvider? Services { get; init; }

    public PipelineContext? PipelineContext { get; init; }

    public IDictionary<string, object> Properties { get; }
        = new Dictionary<string, object>();
}
