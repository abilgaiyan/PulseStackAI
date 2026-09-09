using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class PackageAssetDocumentValidationTests
{
    private readonly AIAssetDocumentValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCanonicalPackageWithHeterogeneousMembers()
    {
        var members = Enum.GetValues<AIAssetDocumentType>()
            .Select(type => Reference(type))
            .ToArray();

        var result = await validator.ValidateAsync(CreatePackage(
            members: members,
            references: members));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectEmptyPackage_AndStillEvaluateProjection()
    {
        var strayReference = Reference(AIAssetDocumentType.Agent);

        var result = await validator.ValidateAsync(CreatePackage(
            members: [],
            references: [strayReference]));

        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.EmptyPackageMembers
            && e.Path == "$.members");
        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch
            && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldUsePackageMissingMemberDiagnostic_AndSuppressProjectionMismatch()
    {
        var valid = Reference(AIAssetDocumentType.Agent);

        var result = await validator.ValidateAsync(CreatePackage(
            members: new AIAssetReferenceDocument[] { valid, null! },
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.MissingPackageMember
            && e.Path == "$.members[1]");
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldUseCommonDiagnosticsForMalformedMember_AndSuppressProjectionMismatch()
    {
        var malformed = Reference(
            AIAssetDocumentType.Agent,
            assetId: "bad",
            urn: " ",
            version: " ");

        var result = await validator.ValidateAsync(CreatePackage(
            members: [malformed],
            references: []));

        result.Errors.Select(e => e.Code).Should().Contain(new[]
        {
            AIAssetDocumentValidationCodes.InvalidReferenceAssetId,
            AIAssetDocumentValidationCodes.MissingReferenceUrn,
            AIAssetDocumentValidationCodes.MissingReferenceVersion
        });
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterDuplicateMember()
    {
        var member = Reference(AIAssetDocumentType.Agent);

        var result = await validator.ValidateAsync(CreatePackage(
            members: [member, Clone(member)],
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.DuplicatePackageMember
            && e.Path == "$.members[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldGiveMemberUrnConflictPrecedenceOverDuplicate()
    {
        var member = Reference(AIAssetDocumentType.Agent, urn: "urn:pulsestack:agent:first");
        var conflicting = Clone(member, urn: "urn:pulsestack:agent:second");

        var result = await validator.ValidateAsync(CreatePackage(
            members: [member, conflicting],
            references: []));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageMemberUrn
            && e.Path == "$.members[1].urn");
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.DuplicatePackageMember);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectDirectExactSelfMember_ButAllowDifferentVersion()
    {
        var id = Guid.NewGuid().ToString();
        var identity = Identity(id, "2.0.0");
        var exactSelf = Reference(
            AIAssetDocumentType.Package,
            assetId: id,
            urn: identity.Urn,
            version: "2.0.0");
        var olderVersion = Reference(
            AIAssetDocumentType.Package,
            assetId: id,
            urn: identity.Urn,
            version: "1.0.0");

        var rejected = await validator.ValidateAsync(CreatePackage(
            identity: identity,
            members: [exactSelf],
            references: []));
        var accepted = await validator.ValidateAsync(CreatePackage(
            identity: identity,
            members: [olderVersion],
            references: [olderVersion]));

        rejected.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.DirectSelfPackageMember
            && e.Path == "$.members[0]");
        accepted.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldClassifyExactDuplicateDependencyWithCommonAD510Only()
    {
        var dependencyReference = Reference(AIAssetDocumentType.Model);
        var first = Dependency(dependencyReference, true);
        var duplicate = Dependency(Clone(dependencyReference), true);

        var result = await validator.ValidateAsync(CreatePackage(
            dependencies: [first, duplicate]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.DuplicateDependency
            && e.Path == "$.dependencies[1]");
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyUrn
            || e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness);
    }

    [Fact]
    public async Task ValidateAsync_ShouldGiveDependencyUrnConflictPrecedenceOverRequirednessAndAD510()
    {
        var reference = Reference(AIAssetDocumentType.Model, urn: "urn:pulsestack:model:first");
        var first = Dependency(reference, true);
        var conflicting = Dependency(Clone(reference, urn: "urn:pulsestack:model:second"), false);

        var result = await validator.ValidateAsync(CreatePackage(
            dependencies: [first, conflicting]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyUrn
            && e.Path == "$.dependencies[1].reference.urn");
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness
            || e.Code == AIAssetDocumentValidationCodes.DuplicateDependency);
    }

    [Fact]
    public async Task ValidateAsync_ShouldGiveDependencyRequirednessConflictPrecedenceOverAD510()
    {
        var reference = Reference(AIAssetDocumentType.Model);
        var first = Dependency(reference, true);
        var conflicting = Dependency(Clone(reference), false);

        var result = await validator.ValidateAsync(CreatePackage(
            dependencies: [first, conflicting]));

        result.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness
            && e.Path == "$.dependencies[1].required");
        result.Errors.Should().NotContain(e =>
            e.Code == AIAssetDocumentValidationCodes.DuplicateDependency);
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectDirectExactSelfDependency_ButAllowDifferentVersion()
    {
        var id = Guid.NewGuid().ToString();
        var identity = Identity(id, "2.0.0");
        var exactSelf = Reference(
            AIAssetDocumentType.Package,
            assetId: id,
            urn: identity.Urn,
            version: "2.0.0");
        var olderVersion = Reference(
            AIAssetDocumentType.Package,
            assetId: id,
            urn: identity.Urn,
            version: "1.0.0");

        var rejected = await validator.ValidateAsync(CreatePackage(
            identity: identity,
            dependencies: [Dependency(exactSelf, true)]));
        var accepted = await validator.ValidateAsync(CreatePackage(
            identity: identity,
            dependencies: [Dependency(olderVersion, true)]));

        rejected.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.DirectSelfPackageDependency
            && e.Path == "$.dependencies[0].reference");
        accepted.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldDistinguishCrossBoundaryUrnConflictFromBoundaryContradiction()
    {
        var member = Reference(AIAssetDocumentType.Agent, urn: "urn:pulsestack:agent:member");
        var sameUrnDependency = Dependency(Clone(member), true);
        var differentUrnDependency = Dependency(
            Clone(member, urn: "urn:pulsestack:agent:external"),
            false);

        var contradiction = await validator.ValidateAsync(CreatePackage(
            members: [member],
            references: [member],
            dependencies: [sameUrnDependency]));
        var urnConflict = await validator.ValidateAsync(CreatePackage(
            members: [member],
            references: [member],
            dependencies: [differentUrnDependency]));

        contradiction.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageMemberDependencyBoundaryContradiction
            && e.Path == "$.dependencies[0].reference");
        urnConflict.Errors.Should().ContainSingle(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageMemberDependencyUrnConflict
            && e.Path == "$.dependencies[0].reference.urn");
    }

    [Fact]
    public async Task ValidateAsync_ShouldLetDependencyDefectsAndProjectionMismatchCoexist()
    {
        var first = Reference(AIAssetDocumentType.Agent);
        var second = Reference(AIAssetDocumentType.Prompt);
        var dependencyReference = Reference(AIAssetDocumentType.Model);

        var result = await validator.ValidateAsync(CreatePackage(
            members: [first, second],
            references: [second, first],
            dependencies:
            [
                Dependency(dependencyReference, true),
                Dependency(Clone(dependencyReference), false)
            ]));

        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness
            && e.Path == "$.dependencies[1].required");
        result.Errors.Should().Contain(e =>
            e.Code == AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch
            && e.Path == "$.references");
    }

    [Fact]
    public async Task ValidateAsync_ShouldKeepPackageSpecificDiagnosticOrderDeterministic()
    {
        var member = Reference(AIAssetDocumentType.Agent);
        var dependencyReference = Reference(AIAssetDocumentType.Model);
        var crossDependency = Dependency(Clone(member), true);

        var result = await validator.ValidateAsync(CreatePackage(
            members: [member],
            references: [],
            dependencies:
            [
                Dependency(dependencyReference, true),
                Dependency(Clone(dependencyReference), false),
                crossDependency
            ]));

        result.Errors
            .Where(e => e.Code is
                AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness
                or AIAssetDocumentValidationCodes.PackageMemberDependencyBoundaryContradiction
                or AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch)
            .Select(e => e.Code)
            .Should().Equal(
                AIAssetDocumentValidationCodes.ConflictingPackageDependencyRequiredness,
                AIAssetDocumentValidationCodes.PackageMemberDependencyBoundaryContradiction,
                AIAssetDocumentValidationCodes.PackageReferenceProjectionMismatch);
    }

    [Fact]
    public void PackageStructuralValidation_ShouldHonorCancellationBeforeTraversal()
    {
        var document = CreatePackage(
            members: [Reference(AIAssetDocumentType.Agent)],
            dependencies: [Dependency(Reference(AIAssetDocumentType.Model), true)]);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var errors = new List<AIAssetDocumentValidationError>();

        var action = () => PackageDocumentStructuralValidator.Validate(
            document,
            errors,
            source.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    private static PackageAssetDocument CreatePackage(
        AIAssetIdentityDocument? identity = null,
        IEnumerable<AIAssetReferenceDocument>? members = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        var memberItems = members?.ToArray()
            ?? [Reference(AIAssetDocumentType.Agent)];

        return new PackageAssetDocument(
            AIAssetSchemaVersion.V1,
            identity ?? Identity(Guid.NewGuid().ToString(), "1.0.0"),
            new AIAssetMetadataDocument("Package", "Distribution package."),
            AIAssetLifecycleDocument.Draft,
            memberItems,
            references?.ToArray() ?? memberItems,
            dependencies);
    }

    private static AIAssetIdentityDocument Identity(string id, string version) => new()
    {
        Id = id,
        Urn = $"urn:pulsestack:package:{id}",
        Version = version
    };

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

    private static AIAssetReferenceDocument Clone(
        AIAssetReferenceDocument source,
        string? urn = null) => new()
    {
        AssetType = source.AssetType,
        AssetId = source.AssetId,
        Urn = urn ?? source.Urn,
        Version = source.Version
    };

    private static AIAssetDependencyDocument Dependency(
        AIAssetReferenceDocument reference,
        bool required) => new()
    {
        Reference = reference,
        Required = required
    };
}
