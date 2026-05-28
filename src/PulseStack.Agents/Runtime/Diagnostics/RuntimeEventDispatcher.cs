using PulseStack.Agents.Runtime.Observability;

namespace PulseStack.Agents.Runtime.Diagnostics;

public sealed class RuntimeEventDispatcher
    : IRuntimeEventDispatcher
{
    private readonly object _sync = new();
    private readonly List<IRuntimeEvent> _events = [];
    private readonly IRuntimeObserver? _observer;

    public RuntimeEventDispatcher(
        IRuntimeObserver? observer = null)
    {
        _observer = observer;
    }

    public IReadOnlyList<IRuntimeEvent> Events
    {
        get
        {
            lock (_sync)
            {
                return _events.ToList();
            }
        }
    }

    public void Dispatch(
        IRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        lock (_sync)
        {
            _events.Add(runtimeEvent);
        }

        try
        {
            _observer?
                .OnEventAsync(runtimeEvent)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // Runtime observation must never break execution.
        }
    }
}
