namespace PulseStack.Abstractions.Tools;

public interface ITypedToolExecutionResult<out TValue>
    : IToolExecutionResult
{
    new TValue? Value { get; }
}