using PulseStack.Abstractions.Tools;

namespace PulseStack.Abstractions.Security;

public interface IToolAuthorizationService
{
    Task<bool> AuthorizeAsync(
        ToolDescriptor tool,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}