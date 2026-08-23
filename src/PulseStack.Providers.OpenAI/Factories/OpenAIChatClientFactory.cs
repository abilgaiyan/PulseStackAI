using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.OpenAI.Options;

namespace PulseStack.Providers.OpenAI.Factories;

public sealed class OpenAIChatClientFactory : IChatClientFactory
{
    private readonly OpenAIOptions _options;

    public OpenAIChatClientFactory(
        IOptions<OpenAIOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IChatClient Create(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ApiKey);

        var client = new OpenAIClient(_options.ApiKey);

        return client
            .GetChatClient(model)
            .AsIChatClient();
    }
}
