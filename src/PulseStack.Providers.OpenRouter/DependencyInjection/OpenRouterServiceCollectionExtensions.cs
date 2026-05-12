using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenAI;
using PulseStack.Providers.OpenRouter.Options;

namespace PulseStack.Providers.OpenRouter.DependencyInjection;

public static class OpenRouterServiceCollectionExtensions
{
    public static IServiceCollection UseOpenRouter(
        this IServiceCollection services,
        string apiKey,
        string model = "openai/gpt-4.1-mini")
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.Configure<OpenRouterOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<OpenRouterOptions>>()
                .Value;

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(options.Endpoint)
            };

            var client = new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(
                    options.ApiKey),
                clientOptions);

            return client
                .GetChatClient(options.Model)
                .AsIChatClient();
        });

        return services;
    }
}