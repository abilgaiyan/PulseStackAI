using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Mapping;

public interface IAIAssetDocumentMapper
{
    AIAssetDocument ToDocument(IAsset asset);

    IAsset FromDocument(AIAssetDocument document);
}
