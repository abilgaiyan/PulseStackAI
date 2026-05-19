namespace PulseStack.Abstractions.Tools;
public interface ITypedTool<TInput, TResult>
    : ITool
{
    Task<ITypedToolExecutionResult<TResult>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
