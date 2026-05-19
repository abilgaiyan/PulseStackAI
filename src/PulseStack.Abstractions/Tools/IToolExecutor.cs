namespace PulseStack.Abstractions.Tools;

public interface IToolExecutor
{
    Task<IToolExecutionResult> ExecuteAsync(
        ITool tool,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
