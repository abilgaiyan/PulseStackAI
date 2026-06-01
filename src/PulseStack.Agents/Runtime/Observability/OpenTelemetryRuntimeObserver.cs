using System.Collections.Concurrent;
using System.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime.Observability;

public sealed class OpenTelemetryRuntimeObserver
    : IRuntimeObserver
{
    private readonly ConcurrentDictionary<Guid, ActivityScope> _pipelines = [];
    private readonly ConcurrentDictionary<AgentActivityKey, ActivityScope> _agents = [];
    private readonly ConcurrentDictionary<ToolActivityKey, ActivityScope> _tools = [];

    public Task OnEventAsync(
        IRuntimeEvent runtimeEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        switch (runtimeEvent)
        {
            case PipelineStartedEvent pipelineStarted:
                OnPipelineStarted(pipelineStarted);
                break;

            case PipelineCompletedEvent pipelineCompleted:
                OnPipelineCompleted(pipelineCompleted);
                break;

            case AgentStartedEvent agentStarted:
                OnAgentStarted(agentStarted);
                break;

            case AgentRetryEvent agentRetry:
                OnAgentRetry(agentRetry);
                break;

            case AgentCompletedEvent agentCompleted:
                OnAgentCompleted(agentCompleted);
                break;

            case ToolExecutingEvent toolExecuting:
                OnToolExecuting(toolExecuting);
                break;

            case ToolExecutedEvent toolExecuted:
                OnToolExecuted(toolExecuted);
                break;
        }

        return Task.CompletedTask;
    }

    private void OnPipelineStarted(
        PipelineStartedEvent runtimeEvent)
    {
        var activity = StartActivity(
            "pipeline.execute",
            runtimeEvent.Timestamp,
            default,
            new ActivityTagsCollection
            {
                ["pipeline.name"] = runtimeEvent.PipelineName,
                ["execution.id"] = runtimeEvent.ExecutionId.ToString(),
                ["agent.count"] = runtimeEvent.AgentCount
            });

        _pipelines[runtimeEvent.ExecutionId] =
            new ActivityScope(
                activity,
                runtimeEvent.Timestamp);
    }

    private void OnPipelineCompleted(
        PipelineCompletedEvent runtimeEvent)
    {
        if (!_pipelines.TryRemove(
                runtimeEvent.ExecutionId,
                out var scope))
        {
            return;
        }

        var failedAgents =
            runtimeEvent.FailedAgentCount;

        var successfulAgents =
            runtimeEvent.SuccessfulAgentCount;

        scope.Activity?.SetTag(
            "execution.status",
            failedAgents switch
            {
                0 => "completed",
                _ when successfulAgents > 0 => "partial_success",
                _ => "failed"
            });

        scope.Activity?.SetTag(
            "duration.ms",
            DurationMilliseconds(scope.StartedAt, runtimeEvent.Timestamp));

        scope.Activity?.SetTag(
            "successful.agents",
            successfulAgents);

        scope.Activity?.SetTag(
            "failed.agents",
            failedAgents);

        if (failedAgents > 0)
        {
            scope.Activity?.SetStatus(
                ActivityStatusCode.Error,
                "One or more agents failed.");
        }

        scope.Activity?.Stop();
    }

    private void OnAgentStarted(
        AgentStartedEvent runtimeEvent)
    {
        var parentContext =
            GetPipelineContext(runtimeEvent.ExecutionId);

        var activity = StartActivity(
            "agent.execute",
            runtimeEvent.Timestamp,
            parentContext,
            new ActivityTagsCollection
            {
                ["agent.name"] = runtimeEvent.AgentName,
                ["model"] = runtimeEvent.Model,
                ["execution.id"] = runtimeEvent.ExecutionId.ToString(),
                ["branch.id"] = runtimeEvent.BranchId?.ToString()
            });

        _agents[AgentActivityKey.From(runtimeEvent)] =
            new ActivityScope(
                activity,
                runtimeEvent.Timestamp);
    }

    private void OnAgentRetry(
        AgentRetryEvent runtimeEvent)
    {
        var activity =
            ResolveAgentActivity(
                runtimeEvent.ExecutionId,
                runtimeEvent.AgentName,
                branchId: null);

        activity?.AddEvent(
            new ActivityEvent(
                "agent.retry",
                runtimeEvent.Timestamp,
                new ActivityTagsCollection
                {
                    ["attempt"] = runtimeEvent.Attempt,
                    ["error"] = runtimeEvent.Error
                }));
    }

    private void OnAgentCompleted(
        AgentCompletedEvent runtimeEvent)
    {
        var key =
            AgentActivityKey.From(runtimeEvent);

        if (!_agents.TryRemove(
                key,
                out var scope))
        {
            return;
        }

        scope.Activity?.SetTag(
            "success",
            runtimeEvent.IsSuccess);

        scope.Activity?.SetTag(
            "duration.ms",
            DurationMilliseconds(scope.StartedAt, runtimeEvent.Timestamp));

        if (!runtimeEvent.IsSuccess)
        {
            RecordException(
                scope.Activity,
                runtimeEvent.ErrorMessage);
        }

        scope.Activity?.Stop();
    }

    private void OnToolExecuting(
        ToolExecutingEvent runtimeEvent)
    {
        var parentContext =
            GetAgentContext(
                runtimeEvent.ExecutionId,
                runtimeEvent.AgentName,
                runtimeEvent.BranchId)
            ?? GetPipelineContext(runtimeEvent.ExecutionId);

        var activity = StartActivity(
            "tool.execute",
            runtimeEvent.Timestamp,
            parentContext,
            new ActivityTagsCollection
            {
                ["tool.name"] = runtimeEvent.ToolName,
                ["tool.category"] = runtimeEvent.Category,
                ["agent.name"] = runtimeEvent.AgentName,
                ["execution.id"] = runtimeEvent.ExecutionId.ToString(),
                ["branch.id"] = runtimeEvent.BranchId?.ToString()
            });

        _tools[ToolActivityKey.From(runtimeEvent)] =
            new ActivityScope(
                activity,
                runtimeEvent.Timestamp);
    }

    private void OnToolExecuted(
        ToolExecutedEvent runtimeEvent)
    {
        var key =
            ToolActivityKey.From(runtimeEvent);

        if (!_tools.TryRemove(
                key,
                out var scope))
        {
            return;
        }

        scope.Activity?.SetTag(
            "tool.name",
            runtimeEvent.ToolName);

        scope.Activity?.SetTag(
            "tool.category",
            runtimeEvent.Category);

        scope.Activity?.SetTag(
            "tool.success",
            runtimeEvent.IsSuccess);

        scope.Activity?.SetTag(
            "tool.duration.ms",
            runtimeEvent.Duration?.TotalMilliseconds
            ?? DurationMilliseconds(scope.StartedAt, runtimeEvent.Timestamp));

        scope.Activity?.SetTag(
            "success",
            runtimeEvent.IsSuccess);

        scope.Activity?.SetTag(
            "duration.ms",
            runtimeEvent.Duration?.TotalMilliseconds
            ?? DurationMilliseconds(scope.StartedAt, runtimeEvent.Timestamp));

        if (!runtimeEvent.IsSuccess)
        {
            RecordException(
                scope.Activity,
                runtimeEvent.ErrorMessage);
        }

        scope.Activity?.Stop();
    }

    private ActivityContext GetPipelineContext(
        Guid executionId)
        => _pipelines.TryGetValue(
                executionId,
                out var scope)
            && scope.Activity is not null
                ? scope.Activity.Context
                : default;

    private ActivityContext? GetAgentContext(
        Guid executionId,
        string? agentName,
        Guid? branchId)
    {
        var activity =
            ResolveAgentActivity(
                executionId,
                agentName,
                branchId);

        return activity?.Context;
    }

    private Activity? ResolveAgentActivity(
        Guid executionId,
        string? agentName,
        Guid? branchId)
    {
        if (Activity.Current?.OperationName == "agent.execute")
        {
            return Activity.Current;
        }

        if (_agents.TryGetValue(
                new AgentActivityKey(executionId, agentName, branchId),
                out var exactScope))
        {
            return exactScope.Activity;
        }

        var matches = _agents
            .Where(item =>
                item.Key.ExecutionId == executionId
                && item.Key.AgentName == agentName)
            .Select(item => item.Value.Activity)
            .Where(activity => activity is not null)
            .ToList();

        return matches.Count == 1
            ? matches[0]
            : null;
    }

    private static Activity? StartActivity(
        string name,
        DateTimeOffset startedAt,
        ActivityContext parentContext,
        ActivityTagsCollection tags)
        => PulseStackActivitySource.Source.StartActivity(
            name,
            ActivityKind.Internal,
            parentContext,
            tags,
            links: null,
            startTime: startedAt);

    private static double DurationMilliseconds(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
        => Math.Max(
            0,
            (completedAt - startedAt).TotalMilliseconds);

    private static void RecordException(
        Activity? activity,
        string? errorMessage)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(
            ActivityStatusCode.Error,
            errorMessage);

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return;
        }

        activity.AddEvent(
            new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.message"] = errorMessage
                }));
    }

    private sealed record ActivityScope(
        Activity? Activity,
        DateTimeOffset StartedAt);

    private sealed record AgentActivityKey(
        Guid ExecutionId,
        string? AgentName,
        Guid? BranchId)
    {
        public static AgentActivityKey From(
            AgentStartedEvent runtimeEvent)
            => new(
                runtimeEvent.ExecutionId,
                runtimeEvent.AgentName,
                runtimeEvent.BranchId);

        public static AgentActivityKey From(
            AgentCompletedEvent runtimeEvent)
            => new(
                runtimeEvent.ExecutionId,
                runtimeEvent.AgentName,
                runtimeEvent.BranchId);
    }

    private sealed record ToolActivityKey(
        Guid ExecutionId,
        string ToolName,
        string Input,
        string? AgentName,
        Guid? BranchId)
    {
        public static ToolActivityKey From(
            ToolExecutingEvent runtimeEvent)
            => new(
                runtimeEvent.ExecutionId,
                runtimeEvent.ToolName,
                runtimeEvent.Input,
                runtimeEvent.AgentName,
                runtimeEvent.BranchId);

        public static ToolActivityKey From(
            ToolExecutedEvent runtimeEvent)
            => new(
                runtimeEvent.ExecutionId,
                runtimeEvent.ToolName,
                runtimeEvent.Input,
                runtimeEvent.AgentName,
                runtimeEvent.BranchId);
    }
}
