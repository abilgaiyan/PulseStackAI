using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Mapping;

/// <summary>
/// Maps between immutable AI Asset definitions and their canonical persistence documents.
/// </summary>
/// <remarks>
/// This contract does not perform aggregate document validation. Documents originating from
/// deserialization, storage, packages, or other external boundaries must be validated before
/// <see cref="FromDocument"/> is invoked.
/// </remarks>
public interface IAIAssetDocumentMapper
{
    /// <summary>
    /// Creates the canonical persistence document for a supported AI Asset definition.
    /// </summary>
    /// <param name="asset">The source AI Asset definition.</param>
    /// <returns>The canonical persistence document.</returns>
    AIAssetDocument ToDocument(IAsset asset);

    /// <summary>
    /// Reconstructs an AI Asset from a structurally valid document using a supported persistence schema.
    /// </summary>
    /// <param name="document">A structurally valid persistence document.</param>
    /// <returns>The reconstructed AI Asset definition.</returns>
    /// <remarks>
    /// Callers must validate externally sourced documents before invoking this method. The mapper
    /// retains defensive reconstruction guards for unsupported schemas, unsupported concrete document
    /// types, unsupported enum values, and malformed primitive identity/reference values, but it does
    /// not replace <c>IAIAssetDocumentValidator</c> or duplicate aggregate document validation.
    /// </remarks>
    IAsset FromDocument(AIAssetDocument document);
}
