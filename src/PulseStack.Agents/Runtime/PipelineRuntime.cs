using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Errors;
using PulseStack.Agents.Runtime.Execution;
using PulseStack.Agents.Runtime.Policies;

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
            PipelineExecutionState? state = null;

            var attempt = 0;

            while (true)
            {
                try
                {
                    attempt++;

                    state = await strategy.ExecuteAsync(
                        pipelineName,
                        agents,
                        context,
                        executionContext,
                        effectiveCancellationToken);

                    break;
                }
                catch when (attempt <= policy.MaxRetries)
                {
                    // TODO:
                    // Future runtime diagnostics may capture
                    // retry metadata and retry lineage.
                }
            }

            if (state is null)
            {
                throw new InvalidOperationException(
                    "Pipeline execution produced no execution state.");
            }

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
                    SnapshotMetadata(context.Items)));

            return result;
        }
        catch (OperationCanceledException)
        {
            var completedAt =
                DateTimeOffset.UtcNow;

            return new PipelineExecutionResult
            {
                Success = false,

                Status = ExecutionStatus.Cancelled,

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
                        Code = "pipeline_cancelled",

                        Message =
                            "Pipeline execution was cancelled."
                    }
                ],

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