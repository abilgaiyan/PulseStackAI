using Microsoft.Extensions.DependencyInjection;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Showcase.Shared;

namespace PulseStack.Showcase.Scenarios;

internal static class OpenTelemetryScenario
{
    public static Task RunAsync(
        IServiceProvider services)
    {
        ConsoleSection.Print(
            "OpenTelemetry Runtime Observability");

        var observers =
            services.GetServices<IRuntimeObserver>()
                .Select(observer => observer.GetType().Name)
                .ToArray();

        Console.WriteLine(
            "OpenTelemetry runtime observer is registered.");

        Console.WriteLine(
            $"ActivitySource : {PulseStackActivitySource.Source.Name}");

        Console.WriteLine(
            "Runtime spans are emitted through ActivitySource when an ActivityListener or exporter is configured.");

        Console.WriteLine(
            "No exporter is required for this showcase.");

        Console.WriteLine();
        Console.WriteLine(
            "Registered observers:");

        foreach (var observer in observers)
        {
            Console.WriteLine(
                $"- {observer}");
        }

        return Task.CompletedTask;
    }
}
