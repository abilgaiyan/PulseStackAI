using System.Threading;
using System.Threading.Tasks;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

/// <summary>
/// A condition that delegates evaluation to a provided function.
/// </summary>
public sealed class DelegateCondition : ICondition
{
    private readonly Func<PipelineContext, bool> _predicate;
    public string Name { get; }

    public DelegateCondition(
        Func<PipelineContext, bool> predicate,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _predicate = predicate;

        Name =
            string.IsNullOrWhiteSpace(name)
                ? nameof(DelegateCondition)
                : name;
    }

    public ValueTask<bool> EvaluateAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_predicate(context));
    }
}