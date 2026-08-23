using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Models;

namespace PulseStack.Core.Assets;

public sealed class ModelAssetFactory
{
    private readonly IModelCatalog _modelCatalog;

    public ModelAssetFactory(IModelCatalog modelCatalog)
    {
        ArgumentNullException.ThrowIfNull(modelCatalog);

        _modelCatalog = modelCatalog;
    }

    public ModelAsset Create(ModelAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Model);

        if (!_modelCatalog.Contains(options.Provider, options.Model))
        {
            throw new ArgumentException(
                $"The model '{options.Model}' is not registered for provider '{options.Provider}'.",
                nameof(options));
        }

        return new ModelAsset(
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:model:{options.Provider}:{options.Model}"),
            options);
    }
}
