using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Groq.Factories;
using PulseStack.Providers.Groq.Models;
using PulseStack.Providers.Groq.Options;

namespace PulseStack.Providers.Groq.DependencyInjection;

public static class GroqServiceCollectionExtensions
{
    public static IServiceCollection UseGroq(
        this IServiceCollection services,
        string apiKey,
        string model = "llama-3.3-70b-versatile")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.Configure<GroqOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        services.TryAddSingleton<GroqChatClientFactory>();

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<GroqOptions>>()
                .Value;

            return provider
                .GetRequiredService<GroqChatClientFactory>()
                .Create(options.Model);
        });

        services.AddSingleton<ChatClientFactoryRegistration>(sp =>
            new ChatClientFactoryRegistration(
                "Groq",
                sp.GetRequiredService<GroqChatClientFactory>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IModelCatalogSource,
                GroqModelCatalogSource>());

        return services;
    }
}
