using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Observability;

namespace PulseStack.Tests.Fakes;

internal sealed class RecordingRuntimeObserver
    : IRuntimeObserver
{
    public List<IRuntimeEvent> Events { get; } = [];

    public Task OnEventAsync(
        IRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken = default)
    {
        Events.Add(runtimeEvent);

        return Task.CompletedTask;
    }
}