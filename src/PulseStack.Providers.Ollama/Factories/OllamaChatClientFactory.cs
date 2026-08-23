using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using PulseStack.Abstractions.Chat;
using PulseStack.Providers.Ollama.Options;

namespace PulseStack.Providers.Ollama.Factories;

public sealed class OllamaChatClientFactory : IChatClientFactory
{
    private readonly OllamaOptions _options;

    public OllamaChatClientFactory(
        IOptions<OllamaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IChatClient Create(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.Endpoint)
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    _options.ApiKey);
        }

        var client = new OllamaApiClient(httpClient)
        {
            SelectedModel = model
        };

        return client;
    }
}
