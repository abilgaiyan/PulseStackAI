using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.Gemini.Options;

namespace PulseStack.Providers.Gemini.Models;

public sealed class GeminiModelCatalogSource : IModelCatalogSource
{
    private const string ProviderName = "Gemini";

    private readonly GeminiOptions _options;

    public GeminiModelCatalogSource(
        IOptions<GeminiOptions> options)
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
