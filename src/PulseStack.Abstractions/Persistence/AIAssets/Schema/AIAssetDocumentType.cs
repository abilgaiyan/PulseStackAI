namespace PulseStack.Abstractions.Persistence.AIAssets.Schema;

/// <summary>
/// Stable discriminator vocabulary for canonical AI Asset persistence documents.
/// </summary>
public enum AIAssetDocumentType
{
    Project,
    Library,
    Package,
    Workflow,
    Agent,
    Prompt,
    Tool,
    Knowledge,
    Memory,
    Policy,
    Provider,
    Model
}
