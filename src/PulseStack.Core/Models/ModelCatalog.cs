using PulseStack.Abstractions.Models;

namespace PulseStack.Core.Models;

public sealed class ModelCatalog : IModelCatalog
{
    private readonly IReadOnlyCollection<ProviderModelDescriptor> _models;

    public ModelCatalog(
        IEnumerable<IModelCatalogSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _models = sources
            .SelectMany(source => source.GetModels())
            .GroupBy(model => new ModelKey(model.Provider, model.Model))
            .Select(group => group.First())
            .ToArray();
    }

    public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
        => _models;

    public bool Contains(
        string provider,
        string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return _models.Any(candidate =>
            string.Equals(
                candidate.Provider,
                provider,
                StringComparison.OrdinalIgnoreCase)
            &&
            string.Equals(
                candidate.Model,
                model,
                StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct ModelKey(
        string Provider,
        string Model)
    {
        public bool Equals(ModelKey other)
            => string.Equals(
                Provider,
                other.Provider,
                StringComparison.OrdinalIgnoreCase)
            &&
            string.Equals(
                Model,
                other.Model,
                StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(Provider),
                StringComparer.OrdinalIgnoreCase.GetHashCode(Model));
    }
}
