using PulseStack.Abstractions.Persistence.AIAssets.Catalog;

namespace PulseStack.Tests.Persistence.AIAssets;

/// <summary>
/// Provider-neutral lifetime wrapper used by the shared MS-009.8 catalog conformance suite.
/// Concrete provider slices supply an isolated provider instance and optional asynchronous cleanup.
/// </summary>
public sealed class AIAssetCatalogProviderConformanceFixture : IAsyncDisposable
{
    private readonly Func<ValueTask>? disposeAsync;

    public AIAssetCatalogProviderConformanceFixture(
        IAIAssetCatalogProvider provider,
        Func<ValueTask>? disposeAsync = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.disposeAsync = disposeAsync;
    }

    public IAIAssetCatalogProvider Provider { get; }

    public ValueTask DisposeAsync() =>
        disposeAsync is null ? ValueTask.CompletedTask : disposeAsync();
}
