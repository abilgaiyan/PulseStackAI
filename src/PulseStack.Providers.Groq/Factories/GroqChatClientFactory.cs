using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.Groq.Options;

namespace PulseStack.Providers.Groq.Factories;

public sealed class GroqChatClientFactory : IChatClientFactory
{
    private readonly GroqOptions _options;

    public GroqChatClientFactory(
        IOptions<GroqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IChatClient Create(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_options.Endpoint)
        };

        var client = new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(_options.ApiKey),
            clientOptions);

        return client
            .GetChatClient(model)
            .AsIChatClient();
    }
}
