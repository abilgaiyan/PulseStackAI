using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Tools;

namespace PulseStack.Agents.Runtime;

internal sealed class PipelineRuntime
{
    private readonly IRuntimeEventDispatcher _eventDispatcher;

    public PipelineRuntime(
        IRuntimeEventDispatcher? eventDispatcher = null)
    {
        _eventDispatcher =
            eventDispatcher
            ?? new RuntimeEventDispatcher();
    }

    public async Task<PipelineExecutionResult> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        IPipelineExecutionStrategy strategy,
        PipelineExecutionPolicy? policy = null,
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

        policy ??= new PipelineExecutionPolicy();

        using var timeoutCts =
            policy.Timeout.HasValue
                ? new CancellationTokenSource(policy.Timeout.Value)
                : null;

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts?.Token ?? CancellationToken.None);

        var effectiveCancellationToken =
            linkedCts.Token;

        var startedAt =
            DateTimeOffset.UtcNow;

        var executionContext =
            new AgentExecutionContext(
                context,
                new List<ChatMessage>(),
                effectiveCancellationToken,
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
                policy,
                effectiveCancellationToken);

            var completedAt =
                DateTimeOffset.UtcNow;

            var successfulAgents =
                agents.Count - state.Errors.Count;

            var status =
                state.Errors.Count switch
                {
                    0 => ExecutionStatus.Completed,

                    _ when successfulAgents > 0
                        => ExecutionStatus.PartialSuccess,

                    _ => ExecutionStatus.Failed
                };

            var result =
                new PipelineExecutionResult
                {
                    Success = state.Errors.Count == 0,

                    Status = status,

                    ExecutionId =
                        executionContext.ExecutionId,

                    FinalOutput =
                        state.FinalOutput,

                    Steps =
                        state.Steps,

                    Errors =
                        state.Errors,

                    TotalUsage =
                        state.TotalUsage,

                    ToolSummary =
                        new ToolExecutionAggregator()
                            .Aggregate(context.ToolResults),

                    StartedAt =
                        startedAt,

                    CompletedAt =
                        completedAt,

                    Duration =
                        completedAt - startedAt
                };

            _eventDispatcher.Dispatch(
                new PipelineCompletedEvent(
                    executionContext.ExecutionId,
                    completedAt,
                    pipelineName,
                    agents.Count,
                    successfulAgents,
                    state.Errors.Count,
                    completedAt - startedAt,
                    state.TotalUsage,
                    SnapshotMetadata(context.Items)));

            return result;
        }
        catch (OperationCanceledException)
        {
            var completedAt =
                DateTimeOffset.UtcNow;

            var timedOut =
                timeoutCts?.IsCancellationRequested == true &&
                !cancellationToken.IsCancellationRequested;    

            return new PipelineExecutionResult
            {
                Success = false,

                Status = timedOut
                    ? ExecutionStatus.TimedOut
                    : ExecutionStatus.Cancelled,

                ExecutionId =
                    executionContext.ExecutionId,

                FinalOutput =
                    context.CurrentOutput
                    ?? string.Empty,

                Steps =
                    context.Steps.ToList(),

                Errors =
                [
                    new PipelineExecutionError
                    {
                        Code = timedOut
                            ? "pipeline_timeout"
                            : "pipeline_cancelled",

                        Message = timedOut
                            ? "Pipeline execution exceeded the configured timeout."
                            : "Pipeline execution was cancelled."
                    }
                ],

                ToolSummary =
                    new ToolExecutionAggregator()
                        .Aggregate(context.ToolResults),

                StartedAt =
                    startedAt,

                CompletedAt =
                    completedAt,

                Duration =
                    completedAt - startedAt
            };
        }
        catch (Exception ex)
        {
            var completedAt =
                DateTimeOffset.UtcNow;

            return new PipelineExecutionResult
            {
                Success = false,

                Status = ExecutionStatus.Failed,

                ExecutionId =
                    executionContext.ExecutionId,

                FinalOutput =
                    context.CurrentOutput
                    ?? string.Empty,

                Steps =
                    context.Steps.ToList(),

                Errors =
                [
                    new PipelineExecutionError
                    {
                        Code = "pipeline_execution_failed",

                        Message = ex.Message,

                        Exception = ex
                    }
                ],

                ToolSummary =
                    new ToolExecutionAggregator()
                        .Aggregate(context.ToolResults),

                StartedAt =
                    startedAt,

                CompletedAt =
                    completedAt,

                Duration =
                    completedAt - startedAt
            };
        }
    }

    private static void PrepareRuntimeMetadata(
        PipelineContext context,
        AgentExecutionContext executionContext)
    {
        context.Items[
            PipelineContextKeys.RuntimeExecutionId] =
                executionContext.ExecutionId;

        context.Items[
            PipelineContextKeys.RuntimeEventDispatcher] =
                executionContext.EventDispatcher;
    }

    private static IReadOnlyDictionary<string, object?> SnapshotMetadata(
        IDictionary<string, object?> metadata)
    {
        var snapshot =
            new Dictionary<string, object?>();

        foreach (var item in metadata)
        {
            if (item.Key ==
                PipelineContextKeys.RuntimeEventDispatcher)
            {
                continue;
            }

            snapshot[item.Key] = item.Value;
        }

        return snapshot;
    }
}
