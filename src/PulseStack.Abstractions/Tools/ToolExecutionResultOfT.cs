namespace PulseStack.Abstractions.Tools;

public sealed class ToolExecutionResult<TValue>
    : ToolExecutionResult,
      ITypedToolExecutionResult<TValue>
{
    public new TValue? Value { get; init; }

    object? IToolExecutionResult.Value
        => Value;

    public static ToolExecutionResult<TValue> Success(
        TValue value)
        => new()
        {
            IsSuccess = true,
            Value = value
        };

    public new static ToolExecutionResult<TValue> Failure(
        string error)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = error
        };
}
