using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Observability;

namespace PulseStack.Tests.Fakes;
internal sealed class ThrowingRuntimeObserver
        : IRuntimeObserver
    {
        public Task OnEventAsync(
            IRuntimeEvent runtimeEvent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer failed.");
    }