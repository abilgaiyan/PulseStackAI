using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class ProjectAssetDocumentValidationTests
{
    private readonly AIAssetDocumentValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCanonicalProject()
    {
        var result = await validator.ValidateAsync(CreateProject());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRequireEntryWorkflow()
    {
        var result = await validator.ValidateAsync(CreateProject(entry: null, useNullEntry: true));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.MissingEntryWorkflow && e.Path == "$.entryWorkflow");
    }

    [Fact]
    public async Task ValidateAsync_ShouldUseCommonDiagnosticsForMalformedEntryAndSuppressRelations()
    {
        var malformed = Reference(AIAssetDocumentType.Workflow, assetId: "bad", urn: " ", version: " ");
        var result = await validator.ValidateAsync(CreateProject(entry: malformed));
        result.Errors.Select(e => e.Code).Should().Contain(new[] { AIAssetDocumentValidationCodes.InvalidReferenceAssetId, AIAssetDocumentValidationCodes.MissingReferenceUrn, AIAssetDocumentValidationCodes.MissingReferenceVersion });
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.EntryWorkflowNotOwned || e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectWrongEntryType()
    {
        var entry = Reference(AIAssetDocumentType.Agent);
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [entry], references: [entry]));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.InvalidEntryWorkflowType && e.Path == "$.entryWorkflow.assetType");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.EntryWorkflowNotOwned || e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectNullAndDisallowedOwnedItems()
    {
        var entry = Reference(AIAssetDocumentType.Workflow);
        var disallowed = Reference(AIAssetDocumentType.Project);
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: new AIAssetReferenceDocument[] { entry, null!, disallowed }, references: [entry]));
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.MissingOwnedAsset && e.Path == "$.ownedAssets[1]");
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.InvalidOwnedAssetType && e.Path == "$.ownedAssets[2].assetType");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterDuplicateOwnedAsset()
    {
        var entry = Reference(AIAssetDocumentType.Workflow);
        var duplicate = Clone(entry);
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [entry, duplicate], references: [entry]));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.DuplicateOwnedAsset && e.Path == "$.ownedAssets[1]");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportUrnConflictAndEntryNotOwnedTogether()
    {
        var id = Guid.NewGuid().ToString();
        var entry = Reference(AIAssetDocumentType.Workflow, id, "urn:one");
        var first = Reference(AIAssetDocumentType.Workflow, id, "urn:two");
        var second = Reference(AIAssetDocumentType.Workflow, id, "urn:three");
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [first, second], references: []));
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.ConflictingOwnedAssetUrn && e.Path == "$.ownedAssets[1].urn");
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.EntryWorkflowNotOwned && e.Path == "$.entryWorkflow");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRequireExactEntryMembership()
    {
        var id = Guid.NewGuid().ToString();
        var entry = Reference(AIAssetDocumentType.Workflow, id, "urn:entry");
        var owned = Reference(AIAssetDocumentType.Workflow, id, "urn:different");
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [owned], references: []));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.EntryWorkflowNotOwned && e.Path == "$.entryWorkflow");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAsync_ShouldRejectOwnedDependencyOverlapIgnoringRequired(bool required)
    {
        var entry = Reference(AIAssetDocumentType.Workflow);
        var agent = Reference(AIAssetDocumentType.Agent);
        var dependency = new AIAssetDependencyDocument { Reference = Clone(agent, urn: "urn:external"), Required = required };
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [entry, agent], references: [entry, agent], dependencies: [dependency]));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.OwnedAssetDependencyOverlap && e.Path == "$.dependencies[0].reference");
    }

    [Fact]
    public async Task ValidateAsync_ShouldEvaluateProjectionWhenOnlyDependencyOverlapExists()
    {
        var entry = Reference(AIAssetDocumentType.Workflow);
        var agent = Reference(AIAssetDocumentType.Agent);
        var dependency = new AIAssetDependencyDocument { Reference = Clone(agent), Required = true };
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [entry, agent], references: [agent, entry], dependencies: [dependency]));
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.OwnedAssetDependencyOverlap);
        result.Errors.Should().Contain(e => e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectNonCanonicalReferenceProjection()
    {
        var entry = Reference(AIAssetDocumentType.Workflow);
        var agent = Reference(AIAssetDocumentType.Agent);
        var result = await validator.ValidateAsync(CreateProject(entry: entry, owned: [entry, agent], references: [agent, entry]));
        result.Errors.Should().ContainSingle(e => e.Code == AIAssetDocumentValidationCodes.ProjectReferenceProjectionMismatch && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationDuringOwnedTraversal()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var action = async () => await validator.ValidateAsync(CreateProject(), source.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ProjectAssetDocument CreateProject(
        AIAssetReferenceDocument? entry = null,
        bool useNullEntry = false,
        IEnumerable<AIAssetReferenceDocument>? owned = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        if (!useNullEntry && entry is null) entry = Reference(AIAssetDocumentType.Workflow);
        var ownedItems = owned?.ToArray() ?? (entry is null ? [] : [entry]);
        var projected = references?.ToArray() ?? (entry is null ? [] : ownedItems.Where(x => x is not null).ToArray());
        return new ProjectAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument { Id = Guid.NewGuid().ToString(), Urn = "urn:pulsestack:project:test", Version = "1.0.0" },
            new AIAssetMetadataDocument("Project"),
            AIAssetLifecycleDocument.Draft,
            entry,
            ownedItems,
            projected,
            dependencies);
    }

    private static AIAssetReferenceDocument Reference(AIAssetDocumentType type, string? assetId = null, string? urn = null, string version = "1.0.0") => new()
    {
        AssetType = type,
        AssetId = assetId ?? Guid.NewGuid().ToString(),
        Urn = urn ?? $"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test",
        Version = version
    };

    private static AIAssetReferenceDocument Clone(AIAssetReferenceDocument source, string? urn = null) => new()
    {
        AssetType = source.AssetType,
        AssetId = source.AssetId,
        Urn = urn ?? source.Urn,
        Version = source.Version
    };
}
