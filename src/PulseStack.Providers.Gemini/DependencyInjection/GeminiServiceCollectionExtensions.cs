using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Gemini.Factories;
using PulseStack.Providers.Gemini.Models;
using PulseStack.Providers.Gemini.Options;

namespace PulseStack.Providers.Gemini.DependencyInjection;

public static class GeminiServiceCollectionExtensions
{
    public static IServiceCollection UseGemini(
        this IServiceCollection services,
        string apiKey,
        string model = "gemini-2.0-flash")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.Configure<GeminiOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        RegisterServices(services);

        return services;
    }

    public static IServiceCollection UseGemini(
        this IServiceCollection services,
        Action<GeminiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        RegisterServices(services);

        return services;
    }

    private static void RegisterServices(
        IServiceCollection services)
    {
        services.TryAddSingleton<GeminiChatClientFactory>();

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>()
                .Value;

            return provider
                .GetRequiredService<GeminiChatClientFactory>()
                .Create(options.Model);
        });

        services.AddSingleton<ChatClientFactoryRegistration>(sp =>
            new ChatClientFactoryRegistration(
                "Gemini",
                sp.GetRequiredService<GeminiChatClientFactory>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IModelCatalogSource,
                GeminiModelCatalogSource>());
    }
}
