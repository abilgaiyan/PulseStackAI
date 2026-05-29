using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenRouter.DependencyInjection;

namespace PulseStack.Showcase.Infrastructure;

internal static class ServiceConfiguration
{
    public static IServiceProvider Configure()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var apiKey = configuration["OpenRouter:ApiKey"];

        var services =
            new ServiceCollection();

        // services.AddPulseStack()
        //     .UseOpenRouter(
        //         apiKey: Environment.GetEnvironmentVariable(
        //             "OPENROUTER_API_KEY")!,
        //         model: "openai/gpt-4o-mini");
        services.AddPulseStack()
            .AddOpenTelemetryRuntimeObserver()
            .AddConsoleRuntimeObserver()
            .UseOpenRouter(
                apiKey: apiKey!,
                model: "openai/gpt-4o-mini");

        return services.BuildServiceProvider();
    }
}
