using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using PulseStack.Abstractions.Models;


namespace PulseStack.Showcase.Infrastructure;

internal static class ShowcaseAssets
{
    public static ModelAsset Model { get; } =
        CreateModel();

    public static AssetReference ModelReference =>
        new(
            Model.Type,
            Model.Id,
            Model.Urn,
            Model.Version);

    private static ModelAsset CreateModel()
    {
        var options =
            new ModelAssetOptions(
                "OpenRouter",
                "google/gemini-2.5-flash");

        var catalog =
            new ShowcaseModelCatalog(options);

        var factory =
            new ModelAssetFactory(catalog);

        return factory.Create(options);
    }

    private sealed class ShowcaseModelCatalog(
        ModelAssetOptions options) : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor>
            GetModels() =>
            [
                new ProviderModelDescriptor(
                    options.Provider,
                    options.Model)
            ];

        public bool Contains(
            string provider,
            string model) =>
            string.Equals(
                provider,
                options.Provider,
                StringComparison.OrdinalIgnoreCase)
            &&
            string.Equals(
                model,
                options.Model,
                StringComparison.OrdinalIgnoreCase);
    }
}