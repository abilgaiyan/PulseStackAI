using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record AgentAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<string> responsibilities;
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> tools;
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> knowledge;
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> policies;

    public AgentAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        string goal,
        string role,
        IEnumerable<string>? responsibilities = null,
        AIAssetReferenceDocument? model = null,
        AIAssetReferenceDocument? prompt = null,
        IEnumerable<AIAssetReferenceDocument>? tools = null,
        IEnumerable<AIAssetReferenceDocument>? knowledge = null,
        AIAssetReferenceDocument? memory = null,
        IEnumerable<AIAssetReferenceDocument>? policies = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Agent,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        Goal = goal;
        Role = role;
        this.responsibilities = new StructuralReadOnlyList<string>(responsibilities);
        Model = model;
        Prompt = prompt;
        this.tools = new StructuralReadOnlyList<AIAssetReferenceDocument>(tools);
        this.knowledge = new StructuralReadOnlyList<AIAssetReferenceDocument>(knowledge);
        Memory = memory;
        this.policies = new StructuralReadOnlyList<AIAssetReferenceDocument>(policies);
    }

    public string Goal { get; }

    public string Role { get; }

    public IReadOnlyList<string> Responsibilities => responsibilities;

    public AIAssetReferenceDocument? Model { get; }

    public AIAssetReferenceDocument? Prompt { get; }

    public IReadOnlyList<AIAssetReferenceDocument> Tools => tools;

    public IReadOnlyList<AIAssetReferenceDocument> Knowledge => knowledge;

    public AIAssetReferenceDocument? Memory { get; }

    public IReadOnlyList<AIAssetReferenceDocument> Policies => policies;
}
