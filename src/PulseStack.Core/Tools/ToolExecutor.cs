using PulseStack.Abstractions.Security;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Core.Tools;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolAuthorizationService _authorization;

    public ToolExecutor(
        IToolAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public async Task<IToolExecutionResult> ExecuteAsync(
        ITool tool,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(context);

        var authorized = await _authorization.AuthorizeAsync(
            tool.Descriptor,
            context,
            cancellationToken);

        if (!authorized)
        {
            return ToolExecutionResult.Forbidden(
                $"Access denied for tool '{tool.Name}'.");
        }

        return await tool.ExecuteAsync(
            context,
            cancellationToken);
    }
}