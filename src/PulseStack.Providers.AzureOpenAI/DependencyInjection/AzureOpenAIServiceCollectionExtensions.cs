using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.AzureOpenAI.Factories;
using PulseStack.Providers.AzureOpenAI.Models;
using PulseStack.Providers.AzureOpenAI.Options;

namespace PulseStack.Providers.AzureOpenAI.DependencyInjection;

public static class AzureOpenAIServiceCollectionExtensions
{
    public static IServiceCollection UseAzureOpenAI(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deployment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);

        services.Configure<AzureOpenAIOptions>(options =>
        {
            options.Endpoint = endpoint;
            options.ApiKey = apiKey;
            options.Deployment = deployment;
        });

        services.TryAddSingleton<AzureOpenAIChatClientFactory>();

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<AzureOpenAIOptions>>()
                .Value;

            return provider
                .GetRequiredService<AzureOpenAIChatClientFactory>()
                .Create(options.Deployment);
        });

        services.AddSingleton<ChatClientFactoryRegistration>(sp =>
            new ChatClientFactoryRegistration(
                "AzureOpenAI",
                sp.GetRequiredService<AzureOpenAIChatClientFactory>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IModelCatalogSource,
                AzureOpenAIModelCatalogSource>());

        return services;
    }
}
