using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.Gemini.Options;

namespace PulseStack.Providers.Gemini.Factories;

public sealed class GeminiChatClientFactory : IChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GeminiOptions _options;

    public GeminiChatClientFactory(
        IServiceProvider serviceProvider,
        IOptions<GeminiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);

        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public IChatClient Create(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ApiKey);

        var rawClient = new Client(
            apiKey: _options.ApiKey)
            .AsIChatClient(model);

        var builder = rawClient.AsBuilder();

        if (_options.UseOpenTelemetry)
        {
            builder.UseOpenTelemetry();
        }

        if (_options.UseLogging)
        {
            builder.UseLogging();
        }

        return builder.Build(_serviceProvider);
    }
}
