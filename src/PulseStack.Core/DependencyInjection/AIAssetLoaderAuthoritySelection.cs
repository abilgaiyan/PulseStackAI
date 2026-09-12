using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.DependencyInjection;

internal sealed class AIAssetLoaderAuthoritySelection
{
    public AIAssetLoaderAuthoritySelection(IAIAssetLoader loader)
    {
        Loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public IAIAssetLoader Loader { get; }
}
