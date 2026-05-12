using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenAI;
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

        // Store configuration
        services.Configure<GroqOptions>(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });

        // Register IChatClient
        services.TryAddSingleton<IChatClient>(provider =>
        {
            var groqOptions = provider.GetRequiredService<IOptions<GroqOptions>>().Value;
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(groqOptions.Endpoint ?? "https://api.groq.com/openai/v1")
            };
            
            // Create the OpenAIClient with API key first, then options
            var client = new OpenAIClient(
                 new System.ClientModel.ApiKeyCredential(groqOptions.ApiKey), options);
            
            // Get the chat client and convert to IChatClient
            return client.GetChatClient(groqOptions.Model).AsIChatClient();
        });

        return services;
    }
}