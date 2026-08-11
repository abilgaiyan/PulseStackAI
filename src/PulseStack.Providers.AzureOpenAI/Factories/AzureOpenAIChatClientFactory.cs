using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.AzureOpenAI.Options;

namespace PulseStack.Providers.AzureOpenAI.Factories;

public sealed class AzureOpenAIChatClientFactory : IChatClientFactory
{
    private readonly AzureOpenAIOptions _options;

    public AzureOpenAIChatClientFactory(
        IOptions<AzureOpenAIOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IChatClient Create(string deployment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ApiKey);

        var client = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));

        return client
            .GetChatClient(deployment)
            .AsIChatClient();
    }
}
