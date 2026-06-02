using System.Diagnostics.Metrics;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Observability;

public sealed class OpenTelemetryMetricsObserver
    : IRuntimeObserver
{
    private readonly Counter<long> _pipelineExecutions;
    private readonly Counter<long> _pipelineCompleted;
    private readonly Counter<long> _pipelineFailed;
    private readonly Counter<long> _pipelinePartialSuccess;
    private readonly Counter<long> _pipelineCancelled;
    private readonly Histogram<double> _pipelineDuration;
    
    private readonly Counter<long> _agentExecutions;
    private readonly Counter<long> _agentCompleted;
    private readonly Counter<long> _agentFailed;
    private readonly Counter<long> _agentRetries;
    private readonly Histogram<double> _agentDuration;

    private readonly Counter<long> _toolExecutions;
    private readonly Counter<long> _toolCompleted;
    private readonly Counter<long> _toolFailed;
    private readonly Histogram<double> _toolDuration;

    private readonly Counter<long> _promptTokens;
    private readonly Counter<long> _completionTokens;
    private readonly Counter<long> _totalTokens;

    public OpenTelemetryMetricsObserver()
    {
        _pipelineExecutions =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PipelineExecutions);

        _pipelineCompleted =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PipelineCompleted);

        _pipelineFailed =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PipelineFailed);

        _pipelinePartialSuccess =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PipelinePartialSuccess);

        _pipelineCancelled =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PipelineCancelled);  

        _pipelineDuration =
            PulseStackMeter.Meter.CreateHistogram<double>(
                PulseStackMetrics.PipelineDuration);    

        _agentExecutions =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.AgentExecutions);

        _agentCompleted =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.AgentCompleted);

        _agentFailed =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.AgentFailed);

        _agentRetries =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.AgentRetries);

        _agentDuration =
            PulseStackMeter.Meter.CreateHistogram<double>(
                PulseStackMetrics.AgentDuration); 

        _toolExecutions =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.ToolExecutions);

        _toolCompleted =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.ToolCompleted);

        _toolFailed =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.ToolFailed);

        _toolDuration =
            PulseStackMeter.Meter.CreateHistogram<double>(
                PulseStackMetrics.ToolDuration);  

        _promptTokens =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.PromptTokens);

        _completionTokens =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.CompletionTokens);

        _totalTokens =
            PulseStackMeter.Meter.CreateCounter<long>(
                PulseStackMetrics.TotalTokens);                                                                      
    }

    public Task OnEventAsync(IRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        switch (runtimeEvent)
        {
            case PipelineStartedEvent pipelineStarted:
                Handle(pipelineStarted);
                break;

            case PipelineCompletedEvent pipelineCompleted:
                Handle(pipelineCompleted);
                break;    

            case AgentStartedEvent agentStarted:
                Handle(agentStarted);
                break;

            case AgentCompletedEvent agentCompleted:
                Handle(agentCompleted);
                break;

            case AgentRetryEvent agentRetry:
                Handle(agentRetry);
                break;

            case ToolExecutingEvent toolExecuting:
                Handle(toolExecuting);
                break;

            case ToolExecutedEvent toolExecuted:
                Handle(toolExecuted);
                break;
                                    
        }

        return Task.CompletedTask;
    }

    private void Handle(
        PipelineStartedEvent started)
    {
        _pipelineExecutions.Add(
            1,
            new KeyValuePair<string, object?>(
                "pipeline.name",
                started.PipelineName));
    }

    private void Handle(PipelineCompletedEvent completed)
    {
        _pipelineCompleted.Add(1);

        _pipelineDuration.Record(
            completed.Duration.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "pipeline.name",
                completed.PipelineName));

        RecordUsageMetrics(
                completed.TotalUsage);                

        if (completed.FailedAgentCount > 0 &&
            completed.SuccessfulAgentCount > 0)
        {
            _pipelinePartialSuccess.Add(1,
                new KeyValuePair<string, object?>(
                    "pipeline.name",
                    completed.PipelineName));
        }

        if (completed.FailedAgentCount > 0 &&
            completed.SuccessfulAgentCount == 0)
        {
            _pipelineFailed.Add(1,
                new KeyValuePair<string, object?>(
                    "pipeline.name",
                    completed.PipelineName));
        }
    }
    private void Handle(
        AgentStartedEvent started)
    {
        _agentExecutions.Add(
            1,
            new KeyValuePair<string, object?>(
                "agent.name",
                started.AgentName));
    }
    
    private void Handle(
        AgentRetryEvent retry)
    {
        _agentRetries.Add(
            1,
            new KeyValuePair<string, object?>(
                "agent.name",
                retry.AgentName));
    }

    private void Handle(
        AgentCompletedEvent completed)
    {
        if (completed.IsSuccess)
        {
           _agentCompleted.Add(
                1,
                new KeyValuePair<string, object?>(
                    "agent.name",
                    completed.AgentName));
        }
        else
        {
            _agentFailed.Add(
                1,
                new KeyValuePair<string, object?>(
                    "agent.name",
                    completed.AgentName));
        }

        _agentDuration.Record(
            completed.Duration.TotalMilliseconds);
    }

    private void Handle(ToolExecutingEvent executing)
    {
        _toolExecutions.Add(
            1,
            new KeyValuePair<string, object?>(
                "tool.name",
                executing.ToolName));
    }

    private void Handle(ToolExecutedEvent executed)
    {
        if (executed.IsSuccess)
        {
           _toolCompleted.Add(
                1,
                new KeyValuePair<string, object?>(
                    "tool.name",
                    executed.ToolName));
        }
        else
        {
            _toolFailed.Add(
                1,
                new KeyValuePair<string, object?>(
                    "tool.name",
                    executed.ToolName));
        }

       if (executed.Duration.HasValue)
        {
            _toolDuration.Record(
                executed.Duration.Value.TotalMilliseconds);
        }
    }

    private void RecordUsageMetrics(
        AIUsage usage)
    {
        if (usage.TotalTokens == 0)
        {
            return;
        }

        _promptTokens.Add(
            usage.PromptTokens,
            new KeyValuePair<string, object?>(
                "provider",
                usage.Provider),
            new KeyValuePair<string, object?>(
                "model",
                usage.Model));

        _completionTokens.Add(
            usage.CompletionTokens,
            new KeyValuePair<string, object?>(
                "provider",
                usage.Provider),
            new KeyValuePair<string, object?>(
                "model",
                usage.Model));

        _totalTokens.Add(
            usage.TotalTokens,
            new KeyValuePair<string, object?>(
                "provider",
                usage.Provider),
            new KeyValuePair<string, object?>(
                "model",
                usage.Model));
    }
}
