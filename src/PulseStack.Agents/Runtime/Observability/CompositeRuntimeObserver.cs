using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Observability;

public sealed class CompositeRuntimeObserver
    : IRuntimeObserver
{
    private readonly IReadOnlyList<IRuntimeObserver> _observers;

    public CompositeRuntimeObserver(
        IEnumerable<IRuntimeObserver> observers)
    {
        ArgumentNullException.ThrowIfNull(observers);

        _observers = observers.ToList();
    }

    public async Task OnEventAsync(
        IRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnEventAsync(
                    runtimeEvent,
                    cancellationToken);
            }
            catch
            {
                // Observability must never break runtime execution.
            }
        }
    }
}
