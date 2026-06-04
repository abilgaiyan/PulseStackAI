using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Observability;
using Xunit;

namespace PulseStack.Tests.Agents;

public class OpenTelemetryMetricsTests
{
  
    [Fact]
    public async Task PipelineStartedEvent_Should_Increment_PipelineExecutions()
    {
        long executions = 0;

        using var listener =
            new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "PulseStack.Runtime")
            {
                meterListener.EnableMeasurementEvents(
                    instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name ==
                    PulseStackMetrics.PipelineExecutions)
                {
                    executions += measurement;
                }
            });

        listener.Start();

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new PipelineStartedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "TestPipeline",
                2,
                new Dictionary<string, object?>()));

        executions.Should().Be(1);
    }

    [Fact]
    public async Task AgentRetryEvent_Should_Increment_RetryCounter()
    {
        long retries = 0;

        using var listener =
            CreateListener(
                PulseStackMetrics.AgentRetries,
                value => retries += value);

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new AgentRetryEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Researcher",
                1,
                "Transient failure"));

        retries.Should().Be(1);
    }    

    [Fact]
    public async Task AgentCompletedEvent_Should_Record_AgentCompletion()
    {
        long completed = 0;

        using var listener =
            CreateListener(
                PulseStackMetrics.AgentCompleted,
                value => completed += value);

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new AgentCompletedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Researcher",
                "openai/gpt-4o-mini",
                null,
                true,
                null,
                TimeSpan.FromSeconds(2),
                new Dictionary<string, object?>()));

        completed.Should().Be(1);
    }
   
    [Fact]
    public async Task AgentCompletedEvent_Should_Record_Duration()
    {
        double duration = 0;

        using var listener =
            new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name ==
                PulseStackMetrics.AgentDuration)
            {
                meterListener.EnableMeasurementEvents(
                    instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, state) =>
            {
                duration = measurement;
            });

        listener.Start();

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new AgentCompletedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Researcher",
                "demo",
                null,
                true,
                null,
                TimeSpan.FromMilliseconds(1500),
                new Dictionary<string, object?>()));

        duration.Should().Be(1500);
    }

    [Fact]
    public async Task PipelineCompletedEvent_Should_Record_TokenMetrics()
    {
        long promptTokens = 0;
        long completionTokens = 0;
        long totalTokens = 0;

        using var listener =
            CreateMultiMetricListener(
                (name, value) =>
                {
                    switch (name)
                    {
                        case PulseStackMetrics.PromptTokens:
                            promptTokens += value;
                            break;

                        case PulseStackMetrics.CompletionTokens:
                            completionTokens += value;
                            break;

                        case PulseStackMetrics.TotalTokens:
                            totalTokens += value;
                            break;
                    }
                });

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new PipelineCompletedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "TestPipeline",
                2,
                2,
                0,
                TimeSpan.FromSeconds(5),
                new AIUsage
                {
                    Provider = "OpenAI",
                    Model = "openai/gpt-4o-mini",
                    PromptTokens = 100,
                    CompletionTokens = 50
                },
                new Dictionary<string, object?>()));

        promptTokens.Should().Be(100);
        completionTokens.Should().Be(50);
        totalTokens.Should().Be(150);
    }    

    [Fact]
    public async Task PipelineCompletedEvent_WithFailures_Should_Record_PartialSuccess()
    {
        long partialSuccess = 0;

        using var listener =
            CreateListener(
                PulseStackMetrics.PipelinePartialSuccess,
                value => partialSuccess += value);

        var observer =
            new OpenTelemetryMetricsObserver();

        await observer.OnEventAsync(
            new PipelineCompletedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "TestPipeline",
            3,
            2,
            1,
            TimeSpan.FromSeconds(5),
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "openai/gpt-4o-mini",
                PromptTokens = 100,
                CompletionTokens = 50
            },
            new Dictionary<string, object?>()));

        partialSuccess.Should().Be(1);
    }

 private static MeterListener CreateListener(
        string metricName,
        Action<long> callback)
    {
        var listener =
            new MeterListener();

        listener.InstrumentPublished =
            (instrument, meterListener) =>
            {
                if (instrument.Name == metricName)
                {
                    meterListener.EnableMeasurementEvents(
                        instrument);
                }
            };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                callback(measurement);
            });

        listener.Start();

        return listener;
    }

    private static MeterListener CreateMultiMetricListener(
        Action<string, long> callback)
    {
        var listener =
            new MeterListener();

        listener.InstrumentPublished =
            (instrument, meterListener) =>
            {
                if (instrument.Meter.Name ==
                    PulseStackMeter.Meter.Name)
                {
                    meterListener.EnableMeasurementEvents(
                        instrument);
                }
            };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                callback(
                    instrument.Name,
                    measurement);
            });

        listener.Start();

        return listener;
    }    
    
}