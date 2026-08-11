using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.OpenAI.Options;

namespace PulseStack.Providers.OpenAI.Models;

public sealed class OpenAIModelCatalogSource : IModelCatalogSource
{
    private const string ProviderName = "OpenAI";

    private readonly OpenAIOptions _options;

    public OpenAIModelCatalogSource(
        IOptions<OpenAIOptions> options)
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
