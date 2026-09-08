using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class AgentAssetDocumentValidationTests
{
    private readonly AIAssetDocumentValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCompleteCanonicalAgentDocument()
    {
        var model = CreateReference(AIAssetDocumentType.Model, "model");
        var prompt = CreateReference(AIAssetDocumentType.Prompt, "prompt");
        var knowledge = CreateReference(AIAssetDocumentType.Knowledge, "knowledge");
        var tool = CreateReference(AIAssetDocumentType.Tool, "tool");
        var memory = CreateReference(AIAssetDocumentType.Memory, "memory");
        var policy = CreateReference(AIAssetDocumentType.Policy, "policy");
        var references = new[] { model, prompt, knowledge, tool, memory, policy };
        var document = CreateDocument(
            model: model,
            prompt: prompt,
            knowledge: [knowledge],
            tools: [tool],
            memory: memory,
            policies: [policy],
            references: references);

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        document.References.Should().Equal(references);
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowDraftAgentWithoutModelPromptOrMemory()
    {
        var document = CreateDocument();

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectMissingGoalRoleAndInvalidResponsibilities()
    {
        var document = CreateDocument(
            goal: " ",
            role: string.Empty,
            responsibilities: ["Valid responsibility", " "]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingAgentGoal
            && error.Path == "$.goal");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingAgentRole
            && error.Path == "$.role");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidAgentResponsibility
            && error.Path == "$.responsibilities[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldEnforceTypedReferenceKinds()
    {
        var wrongModel = CreateReference(AIAssetDocumentType.Tool, "wrong-model");
        var wrongPrompt = CreateReference(AIAssetDocumentType.Model, "wrong-prompt");
        var wrongKnowledge = CreateReference(AIAssetDocumentType.Tool, "wrong-knowledge");
        var wrongTool = CreateReference(AIAssetDocumentType.Knowledge, "wrong-tool");
        var wrongMemory = CreateReference(AIAssetDocumentType.Policy, "wrong-memory");
        var wrongPolicy = CreateReference(AIAssetDocumentType.Memory, "wrong-policy");
        var document = CreateDocument(
            model: wrongModel,
            prompt: wrongPrompt,
            knowledge: [wrongKnowledge],
            tools: [wrongTool],
            memory: wrongMemory,
            policies: [wrongPolicy],
            references:
            [
                wrongModel,
                wrongPrompt,
                wrongKnowledge,
                wrongTool,
                wrongMemory,
                wrongPolicy
            ]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Count(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidAgentReferenceType)
            .Should().Be(6);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectDuplicateTypedReferences()
    {
        var id = Guid.NewGuid();
        var first = CreateReference(AIAssetDocumentType.Tool, "tool", id);
        var second = first with { Urn = "urn:pulsestack:tool:conflict" };
        var document = CreateDocument(
            tools: [first, second],
            references: [first, second]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.DuplicateAgentReference
            && error.Path == "$.tools[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectEnvelopeReferenceProjectionMismatch()
    {
        var model = CreateReference(AIAssetDocumentType.Model, "model");
        var knowledge = CreateReference(AIAssetDocumentType.Knowledge, "knowledge");
        var tool = CreateReference(AIAssetDocumentType.Tool, "tool");
        var document = CreateDocument(
            model: model,
            knowledge: [knowledge],
            tools: [tool],
            references: [model, tool, knowledge]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.AgentReferenceProjectionMismatch
            && error.Path == "$.references");
    }

    [Fact]
    public void Constructor_ShouldSnapshotOrderedCollectionsAndProvideStructuralEquality()
    {
        var responsibilityInput = new List<string> { "Plan", "Execute" };
        var tool = CreateReference(AIAssetDocumentType.Tool, "tool");
        var toolInput = new List<AIAssetReferenceDocument> { tool };
        var referenceInput = new List<AIAssetReferenceDocument> { tool };
        var first = CreateDocument(
            responsibilities: responsibilityInput,
            tools: toolInput,
            references: referenceInput);

        responsibilityInput.Reverse();
        toolInput.Clear();
        referenceInput.Clear();

        var second = CreateDocument(
            identity: first.Identity,
            responsibilities: ["Plan", "Execute"],
            tools: [tool],
            references: [tool]);

        first.Responsibilities.Should().Equal("Plan", "Execute");
        first.Tools.Should().Equal(tool);
        first.References.Should().Equal(tool);
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    private static AgentAssetDocument CreateDocument(
        AIAssetIdentityDocument? identity = null,
        string goal = "Resolve customer requests",
        string role = "Customer support specialist",
        IEnumerable<string>? responsibilities = null,
        AIAssetReferenceDocument? model = null,
        AIAssetReferenceDocument? prompt = null,
        IEnumerable<AIAssetReferenceDocument>? knowledge = null,
        IEnumerable<AIAssetReferenceDocument>? tools = null,
        AIAssetReferenceDocument? memory = null,
        IEnumerable<AIAssetReferenceDocument>? policies = null,
        IEnumerable<AIAssetReferenceDocument>? references = null)
    {
        return new AgentAssetDocument(
            AIAssetSchemaVersion.V1,
            identity ?? CreateIdentity(),
            new AIAssetMetadataDocument("Support Agent"),
            AIAssetLifecycleDocument.Draft,
            goal,
            role,
            responsibilities ?? ["Answer accurately"],
            model,
            prompt,
            knowledge,
            tools,
            memory,
            policies,
            references);
    }

    private static AIAssetIdentityDocument CreateIdentity()
    {
        var id = Guid.NewGuid();
        return new AIAssetIdentityDocument
        {
            Id = id.ToString(),
            Urn = $"urn:pulsestack:agent:{id}",
            Version = "1.0.0"
        };
    }

    private static AIAssetReferenceDocument CreateReference(
        AIAssetDocumentType type,
        string name,
        Guid? id = null)
    {
        var assetId = id ?? Guid.NewGuid();
        return new AIAssetReferenceDocument
        {
            AssetType = type,
            AssetId = assetId.ToString(),
            Urn = $"urn:pulsestack:{name}:{assetId}",
            Version = "1.0.0"
        };
    }
}
