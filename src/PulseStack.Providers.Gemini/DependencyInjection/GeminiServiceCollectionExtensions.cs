using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulseStack.Providers.Gemini.Options;

namespace PulseStack.Providers.Gemini.DependencyInjection;

public static class GeminiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Google Gemini AI provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Your Google AI Studio API key.</param>
    /// <param name="model">
    /// The Gemini model to use
    /// (e.g. "gemini-2.0-flash").
    /// </param>
    /// <returns>
    /// The service collection for chaining.
    /// </returns>
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

        RegisterChatClient(services);

        return services;
    }

    /// <summary>
    /// Adds Google Gemini AI provider using advanced options.
    /// </summary>
    /// <param name="services">
    /// The service collection.
    /// </param>
    /// <param name="configureOptions">
    /// Delegate used to configure Gemini options.
    /// </param>
    /// <returns>
    /// The service collection for chaining.
    /// </returns>
    public static IServiceCollection UseGemini(
        this IServiceCollection services,
        Action<GeminiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);

        RegisterChatClient(services);

        return services;
    }

   
    // Shared registration
    private static void RegisterChatClient(
        IServiceCollection services)
    {
        // Singleton:
        // Google.GenAI.Client internally manages HttpClient.
        // Creating multiple instances may exhaust sockets.

        services.TryAddSingleton<IChatClient>(provider =>
        {
            var options = provider
                .GetRequiredService<IOptions<GeminiOptions>>()
                .Value;

            return BuildChatClient(
                provider,
                options);
        });
    }

   private static IChatClient BuildChatClient(
        IServiceProvider provider,
        GeminiOptions options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            options.ApiKey,
            nameof(options.ApiKey));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            options.Model,
            nameof(options.Model));

        var rawClient = new Client(
            apiKey: options.ApiKey)
            .AsIChatClient(options.Model);

        var builder = rawClient.AsBuilder();

        if (options.UseFunctionInvocation)
        {
            //builder.UseFunctionInvocation();
        }

        if (options.UseOpenTelemetry)
        {
            builder.UseOpenTelemetry();
        }

        if (options.UseLogging)
        {
            builder.UseLogging();
        }

        return builder.Build(provider);
    }
}