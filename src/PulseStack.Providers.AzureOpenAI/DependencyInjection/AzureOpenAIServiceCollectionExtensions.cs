using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);

        services.AddSingleton(new AzureOpenAIOptions
        {
            Endpoint = endpoint,
            ApiKey = apiKey,
            Deployment = deployment
        });

        services.AddSingleton<IChatClient>(_ =>
        {
            var client = new AzureOpenAIClient(
                new Uri(endpoint),
                new AzureKeyCredential(apiKey));

            return client
                .GetChatClient(deployment)
                .AsIChatClient();
        });

        return services;
    }
}
