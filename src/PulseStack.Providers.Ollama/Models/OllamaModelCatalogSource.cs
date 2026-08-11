using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Ollama.Options;

namespace PulseStack.Providers.Ollama.Models;

public sealed class OllamaModelCatalogSource : IModelCatalogSource
{
    private const string ProviderName = "Ollama";

    private readonly OllamaOptions _options;

    public OllamaModelCatalogSource(
        IOptions<OllamaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
    {
        if (_options.AvailableModels.Count == 0)
        {
            return
            [
                new ProviderModelDescriptor(
                    ProviderName,
                    _options.Model)
            ];
        }

        return _options.AvailableModels
            .Where(model =>
                !string.IsNullOrWhiteSpace(model))
            .Select(model =>
                new ProviderModelDescriptor(
                    ProviderName,
                    model))
            .ToArray();
    }
}
