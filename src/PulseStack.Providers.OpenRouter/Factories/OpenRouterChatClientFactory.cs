using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.OpenRouter.Options;

namespace PulseStack.Providers.OpenRouter.Factories;

public sealed class OpenRouterChatClientFactory
    : IChatClientFactory
{
    private readonly OpenRouterOptions _options;

    public OpenRouterChatClientFactory(
        IOptions<OpenRouterOptions> options)
    {
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
            new System.ClientModel.ApiKeyCredential(
                _options.ApiKey),
            clientOptions);

        return client
            .GetChatClient(model)
            .AsIChatClient();
    }
}