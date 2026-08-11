using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Ollama.Factories;
using PulseStack.Providers.Ollama.Models;
using PulseStack.Providers.Ollama.Options;

namespace PulseStack.Providers.Ollama.DependencyInjection;

public static class OllamaServiceCollectionExtensions
{
    public static IServiceCollection UseOllama(
        this IServiceCollection services,
        string endpoint,
        string model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.Configure<OllamaOptions>(options =>
        {
            options.Endpoint = endpoint;
            options.Model = model;
        });

        services.TryAddSingleton<OllamaChatClientFactory>();

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<OllamaOptions>>()
                .Value;

            return provider
                .GetRequiredService<OllamaChatClientFactory>()
                .Create(options.Model);
        });

        services.AddSingleton<ChatClientFactoryRegistration>(sp =>
            new ChatClientFactoryRegistration(
                "Ollama",
                sp.GetRequiredService<OllamaChatClientFactory>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IModelCatalogSource,
                OllamaModelCatalogSource>());

        return services;
    }
}
