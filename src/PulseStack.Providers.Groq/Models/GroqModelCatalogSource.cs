using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Groq.Options;

namespace PulseStack.Providers.Groq.Models;

public sealed class GroqModelCatalogSource : IModelCatalogSource
{
    private const string ProviderName = "Groq";

    private readonly GroqOptions _options;

    public GroqModelCatalogSource(
        IOptions<GroqOptions> options)
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
