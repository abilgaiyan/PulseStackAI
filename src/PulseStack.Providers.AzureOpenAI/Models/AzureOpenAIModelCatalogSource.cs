using Microsoft.Extensions.Options;
using PulseStack.Abstractions.Models;
using PulseStack.Providers.AzureOpenAI.Options;

namespace PulseStack.Providers.AzureOpenAI.Models;

public sealed class AzureOpenAIModelCatalogSource : IModelCatalogSource
{
    private const string ProviderName = "AzureOpenAI";

    private readonly AzureOpenAIOptions _options;

    public AzureOpenAIModelCatalogSource(
        IOptions<AzureOpenAIOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
    {
        if (_options.AvailableDeployments.Count == 0)
        {
            return
            [
                new ProviderModelDescriptor(
                    ProviderName,
                    _options.Deployment)
            ];
        }

        return _options.AvailableDeployments
            .Where(deployment =>
                !string.IsNullOrWhiteSpace(deployment))
            .Select(deployment =>
                new ProviderModelDescriptor(
                    ProviderName,
                    deployment))
            .ToArray();
    }
}
