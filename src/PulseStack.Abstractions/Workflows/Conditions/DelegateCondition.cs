using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows.Conditions;

/// <summary>
/// A condition that delegates evaluation to a provided function.
/// </summary>
public sealed class DelegateCondition : ICondition
{
    private readonly Func<
        PipelineContext,
        CancellationToken,
        ValueTask<bool>> _predicate;
    public string Name { get; }

    public DelegateCondition(
        Func<
            PipelineContext,
            CancellationToken,
            ValueTask<bool>> predicate,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _predicate = predicate;

        Name =
            string.IsNullOrWhiteSpace(name)
                ? nameof(DelegateCondition)
                : name;
    }

    public DelegateCondition(
        Func<PipelineContext, bool> predicate,
        string? name = null)
        : this(
            (ctx, _) =>
                ValueTask.FromResult(
                    predicate(ctx)),
            name)
    {
    }

    public ValueTask<bool> EvaluateAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        return _predicate(
            context,
            cancellationToken);
    }
}