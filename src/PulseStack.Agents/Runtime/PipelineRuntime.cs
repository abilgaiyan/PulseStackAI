using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime;

internal sealed class PipelineRuntime
{
    private readonly IRuntimeEventDispatcher _eventDispatcher;

    public PipelineRuntime(
        IRuntimeEventDispatcher? eventDispatcher = null)
    {
        _eventDispatcher = eventDispatcher ?? new RuntimeEventDispatcher();
    }

    public async Task<PipelineExecutionResult> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        IPipelineExecutionStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategy);

        if (agents.Count == 0)
        {
            throw new InvalidOperationException(
                "Pipeline contains no agents.");
        }

        var startedAt = DateTimeOffset.UtcNow;

        var executionContext = new AgentExecutionContext(
            context,
            new List<ChatMessage>(),
            cancellationToken,
            eventDispatcher: _eventDispatcher,
            startedAt: startedAt);

        PrepareRuntimeMetadata(
            context,
            executionContext);

        _eventDispatcher.Dispatch(
            new PipelineStartedEvent(
                executionContext.ExecutionId,
                startedAt,
                pipelineName,
                agents.Count,
                SnapshotMetadata(context.Items)));

        try
        {
            var state = await strategy.ExecuteAsync(
                pipelineName,
                agents,
                context,
                executionContext,
                cancellationToken);

            var completedAt = DateTimeOffset.UtcNow;

            var result = new PipelineExecutionResult(
                state.Errors.Count == 0,
                executionContext.ExecutionId,
                state.FinalOutput,
                state.Steps,
                state.Errors,
                completedAt - startedAt,
                startedAt,
                completedAt);

            _eventDispatcher.Dispatch(
                new PipelineCompletedEvent(
                    executionContext.ExecutionId,
                    completedAt,
                    pipelineName,
                    agents.Count,
                    agents.Count - state.Errors.Count,
                    state.Errors.Count,
                    SnapshotMetadata(context.Items)));

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var completedAt = DateTimeOffset.UtcNow;

            _eventDispatcher.Dispatch(
                new PipelineCompletedEvent(
                    executionContext.ExecutionId,
                    completedAt,
                    pipelineName,
                    agents.Count,
                    0,
                    agents.Count,
                    SnapshotMetadata(context.Items)));

            _ = ex;
            throw;
        }
    }

    private static void PrepareRuntimeMetadata(
        PipelineContext context,
        AgentExecutionContext executionContext)
    {
        context.Items[PipelineContextKeys.RuntimeExecutionId] =
            executionContext.ExecutionId;
        context.Items[PipelineContextKeys.RuntimeEventDispatcher] =
            executionContext.EventDispatcher;
    }

    private static IReadOnlyDictionary<string, object?> SnapshotMetadata(
        IDictionary<string, object?> metadata)
    {
        var snapshot = new Dictionary<string, object?>();

        foreach (var item in metadata)
        {
            if (item.Key == PipelineContextKeys.RuntimeEventDispatcher)
            {
                continue;
            }

            snapshot[item.Key] = item.Value;
        }

        return snapshot;
    }
}
