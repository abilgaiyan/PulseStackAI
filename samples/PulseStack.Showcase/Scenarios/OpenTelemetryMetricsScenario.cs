using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class OpenTelemetryMetricsScenario
{
    public static Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "OpenTelemetry Metrics");

        Console.WriteLine(
            "OpenTelemetry metrics observer is registered.");

        Console.WriteLine();

        Console.WriteLine(
            "Meter");

        Console.WriteLine(
            "-----");

        Console.WriteLine(
            "PulseStack.Runtime");

        Console.WriteLine();

        Console.WriteLine(
            "Published Metrics");

        Console.WriteLine(
            "-----------------");

        PrintMetric(PulseStackMetrics.PipelineExecutions);
        PrintMetric(PulseStackMetrics.PipelineCompleted);
        PrintMetric(PulseStackMetrics.PipelineFailed);
        PrintMetric(PulseStackMetrics.PipelinePartialSuccess);
        PrintMetric(PulseStackMetrics.PipelineCancelled);
        PrintMetric(PulseStackMetrics.PipelineTimedOut);
        PrintMetric(PulseStackMetrics.PipelineDuration);

        PrintMetric(PulseStackMetrics.AgentExecutions);
        PrintMetric(PulseStackMetrics.AgentCompleted);
        PrintMetric(PulseStackMetrics.AgentFailed);
        PrintMetric(PulseStackMetrics.AgentRetries);
        PrintMetric(PulseStackMetrics.AgentDuration);

        PrintMetric(PulseStackMetrics.ToolExecutions);
        PrintMetric(PulseStackMetrics.ToolCompleted);
        PrintMetric(PulseStackMetrics.ToolFailed);
        PrintMetric(PulseStackMetrics.ToolDuration);

        PrintMetric(PulseStackMetrics.PromptTokens);
        PrintMetric(PulseStackMetrics.CompletionTokens);
        PrintMetric(PulseStackMetrics.TotalTokens);

        Console.WriteLine();

        Console.WriteLine(
            "Metric Dimensions");

        Console.WriteLine(
            "-----------------");

        Console.WriteLine("- pipeline.name");
        Console.WriteLine("- agent.name");
        Console.WriteLine("- tool.name");
        Console.WriteLine("- provider");
        Console.WriteLine("- model");

        Console.WriteLine();

        Console.WriteLine(
            "Supported Export Targets");

        Console.WriteLine(
            "------------------------");

        Console.WriteLine("- OpenTelemetry OTLP");
        Console.WriteLine("- Prometheus");
        Console.WriteLine("- Grafana");
        Console.WriteLine("- Azure Monitor");
        Console.WriteLine("- Jaeger");

        Console.WriteLine();

        Console.WriteLine(
            "Metrics become available when a MeterProvider");

        Console.WriteLine(
            "and exporter are configured.");

        return Task.CompletedTask;
    }

    private static void PrintMetric(
        string metricName)
    {
        Console.WriteLine($"✓ {metricName}");
    }
}