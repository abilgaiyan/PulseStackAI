using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Abstractions.Runtime.Invocation.Application;

public interface IApplicationInvoker
{
    Task<ApplicationInvocationResult> InvokeAsync(
        RealizedApplication application,
        ApplicationInvocationRequest request,
        CancellationToken cancellationToken = default);
}
