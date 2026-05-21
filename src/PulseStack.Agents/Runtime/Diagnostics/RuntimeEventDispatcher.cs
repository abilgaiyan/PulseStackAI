namespace PulseStack.Agents.Runtime.Diagnostics;

internal sealed class RuntimeEventDispatcher
    : IRuntimeEventDispatcher
{
    private readonly object _sync = new();
    private readonly List<IRuntimeEvent> _events = [];

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
    }
}
