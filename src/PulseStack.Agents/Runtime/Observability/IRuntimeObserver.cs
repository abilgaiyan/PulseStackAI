using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Observability;

public interface IRuntimeObserver
{
    Task OnEventAsync(
        IRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken = default);
}
