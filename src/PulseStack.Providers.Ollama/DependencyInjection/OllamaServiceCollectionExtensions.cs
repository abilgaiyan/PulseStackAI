using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using PulseStack.Providers.Ollama.Options;

namespace PulseStack.Providers.Ollama.DependencyInjection;

public static class OllamaServiceCollectionExtensions
{
    public static IServiceCollection UseOllama(
        this IServiceCollection services,
        string endpoint,
        string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.AddSingleton(new OllamaOptions
        {
            Endpoint = endpoint,
            Model = model
        });

        services.AddSingleton<IChatClient>(_ =>
        {
            var ollama = new OllamaApiClient(
                endpoint,
                model);

            return ollama;
        });

        return services;
    }
}