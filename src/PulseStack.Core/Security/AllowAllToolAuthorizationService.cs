using PulseStack.Abstractions.Security;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Core.Security;

public sealed class AllowAllToolAuthorizationService
    : IToolAuthorizationService
{
    public Task<bool> AuthorizeAsync(
        ToolDescriptor tool,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
