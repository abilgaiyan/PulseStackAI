using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.OpenRouter.Options;

namespace PulseStack.Providers.OpenRouter.Models;

public sealed class OpenRouterModelCatalogSource
    : IModelCatalogSource
{
    private const string ProviderName = "OpenRouter";

    private readonly OpenRouterOptions _options;

    public OpenRouterModelCatalogSource(
        IOptions<OpenRouterOptions> options)
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
