using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.OpenAI.Factories;
using PulseStack.Providers.OpenAI.Models;
using PulseStack.Providers.OpenAI.Options;

namespace PulseStack.Providers.OpenAI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseOpenAI(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o-mini")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.Configure<OpenAIOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        services.TryAddSingleton<OpenAIChatClientFactory>();

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<OpenAIOptions>>()
                .Value;

            return provider
                .GetRequiredService<OpenAIChatClientFactory>()
                .Create(options.Model);
        });

        services.AddSingleton<ChatClientFactoryRegistration>(sp =>
            new ChatClientFactoryRegistration(
                "OpenAI",
                sp.GetRequiredService<OpenAIChatClientFactory>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IModelCatalogSource,
                OpenAIModelCatalogSource>());

        return services;
    }
}
