using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Models;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime.Tools;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolExecutor _innerExecutor;

    public ToolExecutor(
        IToolExecutor innerExecutor)
    {
        _innerExecutor = innerExecutor ?? throw new ArgumentNullException(nameof(innerExecutor));
    }

    public Task<IToolExecutionResult> ExecuteAsync(
        ITool tool,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(context);

        return _innerExecutor.ExecuteAsync(
            tool,
            context,
            cancellationToken);
    }

    internal async Task<ChatMessage> ExecuteAsync(
        ToolCall toolCall,
        IToolRegistry tools,
        AgentExecutionContext context)
    {
        context.EventDispatcher.Dispatch(
            new ToolExecutingEvent(
                context.ExecutionId,
                DateTimeOffset.UtcNow,
                toolCall.Tool,
                toolCall.Input,
                context.Agent?.Name,
                context.BranchId,
                SnapshotMetadata(context.Metadata)));

        var tool = tools.GetByName(
            toolCall.Tool);

        if (tool is null)
        {
            context.EventDispatcher.Dispatch(
                new ToolExecutedEvent(
                    context.ExecutionId,
                    DateTimeOffset.UtcNow,
                    toolCall.Tool,
                    toolCall.Input,
                    context.Agent?.Name,
                    context.BranchId,
                    false,
                    $"Tool '{toolCall.Tool}' not found.",
                    SnapshotMetadata(context.Metadata)));

            return new ChatMessage(
                ChatRole.Tool,
                $"Tool '{toolCall.Tool}' not found.");
        }

        if (!tool.IsEnabled)
        {
            context.EventDispatcher.Dispatch(
                new ToolExecutedEvent(
                    context.ExecutionId,
                    DateTimeOffset.UtcNow,
                    tool.Name,
                    toolCall.Input,
                    context.Agent?.Name,
                    context.BranchId,
                    false,
                    $"Tool '{tool.Name}' is disabled.",
                    SnapshotMetadata(context.Metadata)));

            return new ChatMessage(
                ChatRole.Tool,
                $"Tool '{tool.Name}' is disabled.");
        }

        IToolExecutionResult result;

        try
        {
            var toolContext = new ToolExecutionContext
            {
                ToolName = tool.Name,
                Input = toolCall.Input,
                Services = context.Services,
                PipelineContext = context.PipelineContext
            };

            result = await ExecuteAsync(
                tool,
                toolContext,
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            result = ToolExecutionResult.Failure($"Tool '{tool.Name}' failed: {ex.Message}");
        }

        context.EventDispatcher.Dispatch(
            new ToolExecutedEvent(
                context.ExecutionId,
                DateTimeOffset.UtcNow,
                tool.Name,
                toolCall.Input,
                context.Agent?.Name,
                context.BranchId,
                result.IsSuccess,
                result.ErrorMessage,
                SnapshotMetadata(context.Metadata)));

        var formatted = FormatResult(result);
        var toolContent = result.IsSuccess
            ? $"Tool '{tool.Name}' result:\n{formatted}"
            : $"Tool '{tool.Name}' failed:\n{formatted}";

        return new ChatMessage(
            ChatRole.Tool,
            toolContent);
    }

    internal static string FormatResult(
        IToolExecutionResult result)
    {
        if (!result.IsSuccess)
        {
            return result.ErrorMessage
                ?? "Unknown tool failure.";
        }

        if (result.Value is null)
        {
            return "Tool executed successfully.";
        }

        if (result.Value is string text)
        {
            return text;
        }

        return System.Text.Json.JsonSerializer.Serialize(
            result.Value,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    private static IReadOnlyDictionary<string, object?> SnapshotMetadata(
        IDictionary<string, object?> metadata)
        => new Dictionary<string, object?>(metadata);
}
