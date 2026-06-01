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

        var startedAt =
            DateTimeOffset.UtcNow;

        var authorized = await _authorization.AuthorizeAsync(
            tool.Descriptor,
            context,
            cancellationToken);

        if (!authorized)
        {
            return WithMetadata(
                ToolExecutionResult.Forbidden(
                    $"Access denied for tool '{tool.Name}'."),
                tool,
                context,
                startedAt,
                DateTimeOffset.UtcNow);
        }

        var result = await tool.ExecuteAsync(
            context,
            cancellationToken);

        return WithMetadata(
            result,
            tool,
            context,
            startedAt,
            DateTimeOffset.UtcNow);
    }

    private static IToolExecutionResult WithMetadata(
        IToolExecutionResult result,
        ITool tool,
        ToolExecutionContext context,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        var metadata =
            new ToolExecutionMetadata
            {
                ExecutionId =
                    context.ExecutionId,

                StartedAt =
                    startedAt,

                CompletedAt =
                    completedAt,

                Duration =
                    completedAt - startedAt,

                Success =
                    result.IsSuccess,

                ToolName =
                    tool.Name,

                Category =
                    tool.Category
            };

        if (result is ToolExecutionResult toolExecutionResult)
        {
            toolExecutionResult.Metadata = metadata;

            return toolExecutionResult;
        }

        return new ToolExecutionResult
        {
            IsSuccess =
                result.IsSuccess,

            ErrorMessage =
                result.ErrorMessage,

            Value =
                result.Value,

            Metadata =
                metadata,

            Artifacts =
                result.Artifacts
        };
    }
}
