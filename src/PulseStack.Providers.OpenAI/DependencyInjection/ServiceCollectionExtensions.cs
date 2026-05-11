using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;
using PulseStack.Providers.OpenAI.Options;

namespace PulseStack.Providers.OpenAI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseOpenAI(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o-mini")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        services.Configure<OpenAIOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        services.TryAddSingleton<IChatClient>(sp =>
        {
            var client = new OpenAIClient(apiKey);
            return client
                .GetChatClient(model)
                .AsIChatClient();
        });

        return services;
    }
}