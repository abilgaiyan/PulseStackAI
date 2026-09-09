using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class LibraryAssetDocumentValidationTests
{
    private readonly AIAssetDocumentValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCanonicalEmptyLibrary()
    {
        var result = await validator.ValidateAsync(CreateLibrary());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCanonicalPopulatedLibrary()
    {
        var workflow = Reference(AIAssetDocumentType.Workflow);
        var agent = Reference(AIAssetDocumentType.Agent);
        var model = Reference(AIAssetDocumentType.Model);

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [workflow, agent, model],
            references: [workflow, agent, model]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectNullMember_AndSuppressProjectionMismatch()
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var result = await validator.ValidateAsync(CreateLibrary(
            members: new AIAssetReferenceDocument[] { member, null! },
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.MissingLibraryMember
            && e.Path == "$.members[1]");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldUseCommonDiagnosticsForMalformedMember_AndSuppressProjectionMismatch()
    {
        var malformed = Reference(
            AIAssetDocumentType.Agent,
            assetId: "bad",
            urn: " ",
            version: " ");

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [malformed],
            references: []));

        result.Errors.Select(e => e.Code).Should().Contain(new[]
        {
            AIAssetDocumentValidationCodes.InvalidReferenceAssetId,
            AIAssetDocumentValidationCodes.MissingReferenceUrn,
            AIAssetDocumentValidationCodes.MissingReferenceVersion
        });
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch);
    }

    [Theory]
    [InlineData(AIAssetDocumentType.Project)]
    [InlineData(AIAssetDocumentType.Library)]
    [InlineData(AIAssetDocumentType.Package)]
    [InlineData(AIAssetDocumentType.Provider)]
    public async Task ValidateAsync_ShouldRejectProhibitedLibraryMemberTypes(AIAssetDocumentType assetType)
    {
        var prohibited = Reference(assetType);

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [prohibited],
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.InvalidLibraryMemberType
            && e.Path == "$.members[0].assetType");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterDuplicateMember_AndSuppressProjectionMismatch()
    {
        var member = Reference(AIAssetDocumentType.Agent);

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member, Clone(member)],
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.DuplicateLibraryMember
            && e.Path == "$.members[1]");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterConflictingMemberUrn_AndSuppressProjectionMismatch()
    {
        var member = Reference(AIAssetDocumentType.Agent, urn: "urn:pulsestack:agent:first");
        var conflicting = Clone(member, urn: "urn:pulsestack:agent:second");

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member, conflicting],
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingLibraryMemberUrn
            && e.Path == "$.members[1].urn");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldTreatVersionsAsDistinctDefinitionIdentities()
    {
        var id = Guid.NewGuid().ToString();
        var first = Reference(AIAssetDocumentType.Agent, assetId: id, version: "1.0.0");
        var second = Reference(AIAssetDocumentType.Agent, assetId: id, version: "2.0.0");

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [first, second],
            references: [first, second]));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateAsync_ShouldRejectMemberDependencyOverlapIgnoringRequired(bool required)
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var dependency = new AIAssetDependencyDocument
        {
            Reference = Clone(member),
            Required = required
        };

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member],
            references: [member],
            dependencies: [dependency]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryMemberDependencyOverlap
            && e.Path == "$.dependencies[0].reference");
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectOverlapWhenDependencyHasDifferentButStructurallyValidUrn()
    {
        var member = Reference(AIAssetDocumentType.Agent, urn: "urn:pulsestack:agent:member");
        var dependency = new AIAssetDependencyDocument
        {
            Reference = Clone(member, urn: "urn:pulsestack:agent:external"),
            Required = true
        };

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member],
            references: [member],
            dependencies: [dependency]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryMemberDependencyOverlap
            && e.Path == "$.dependencies[0].reference");
    }

    [Fact]
    public async Task ValidateAsync_ShouldExcludeMalformedDependencyFromOverlapChecks()
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var dependency = new AIAssetDependencyDocument
        {
            Reference = Clone(member, urn: " "),
            Required = true
        };

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member],
            references: [member],
            dependencies: [dependency]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.MissingReferenceUrn
            && e.Path == "$.dependencies[0].reference.urn");
        result.Errors.Should().NotContain(e => e.Code == AIAssetDocumentValidationCodes.LibraryMemberDependencyOverlap);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectNonCanonicalReferenceProjection()
    {
        var agent = Reference(AIAssetDocumentType.Agent);
        var prompt = Reference(AIAssetDocumentType.Prompt);

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [agent, prompt],
            references: [prompt, agent]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch
            && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowOverlapAndProjectionMismatchToCoexist()
    {
        var agent = Reference(AIAssetDocumentType.Agent);
        var prompt = Reference(AIAssetDocumentType.Prompt);
        var dependency = new AIAssetDependencyDocument
        {
            Reference = Clone(agent, urn: "urn:pulsestack:agent:external"),
            Required = false
        };

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [agent, prompt],
            references: [prompt, agent],
            dependencies: [dependency]));

        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryMemberDependencyOverlap
            && e.Path == "$.dependencies[0].reference");
        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch
            && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowMalformedEnvelopeReferenceAndProjectionMismatchToCoexist()
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var malformedEnvelope = Reference(AIAssetDocumentType.Prompt, assetId: "not-a-guid");

        var result = await validator.ValidateAsync(CreateLibrary(
            members: [member],
            references: [member, malformedEnvelope]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.InvalidReferenceAssetId
            && e.Path == "$.references[1].assetId");
        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.LibraryReferenceProjectionMismatch
            && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationDuringMemberTraversal()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var action = async () => await validator.ValidateAsync(CreateLibrary(
            members: [Reference(AIAssetDocumentType.Agent)]), source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationDuringLibraryDependencyTraversal()
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var document = CreateLibrary(
            members: [member],
            references: [member],
            dependencies: [new AIAssetDependencyDocument { Reference = Reference(AIAssetDocumentType.Model) }]);
        using var source = new CancelAfterFirstEnumerationTokenSource();

        var action = async () => await validator.ValidateAsync(document, source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static LibraryAssetDocument CreateLibrary(
        IEnumerable<AIAssetReferenceDocument>? members = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        var memberItems = members?.ToArray() ?? [];
        return new LibraryAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = Guid.NewGuid().ToString(),
                Urn = "urn:pulsestack:library:test",
                Version = "1.0.0"
            },
            new AIAssetMetadataDocument("Library", "Reusable library."),
            AIAssetLifecycleDocument.Draft,
            memberItems,
            references?.ToArray() ?? memberItems,
            dependencies);
    }

    private static AIAssetReferenceDocument Reference(
        AIAssetDocumentType type,
        string? assetId = null,
        string? urn = null,
        string version = "1.0.0") => new()
    {
        AssetType = type,
        AssetId = assetId ?? Guid.NewGuid().ToString(),
        Urn = urn ?? $"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test:{Guid.NewGuid():N}",
        Version = version
    };

    private static AIAssetReferenceDocument Clone(AIAssetReferenceDocument source, string? urn = null) => new()
    {
        AssetType = source.AssetType,
        AssetId = source.AssetId,
        Urn = urn ?? source.Urn,
        Version = source.Version
    };

    private sealed class CancelAfterFirstEnumerationTokenSource : IDisposable
    {
        private readonly CancellationTokenSource source = new();

        public CancellationToken Token
        {
            get
            {
                source.Cancel();
                return source.Token;
            }
        }

        public void Dispose() => source.Dispose();
    }
}
