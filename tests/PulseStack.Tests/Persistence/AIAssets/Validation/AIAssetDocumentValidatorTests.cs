using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class AIAssetDocumentValidatorTests
{
    private readonly AIAssetDocumentValidator validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptValidDocument()
    {
        var result = await validator.ValidateAsync(CreateDocument());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectUnsupportedSchemaAndDocumentType()
    {
        var document = CreateDocument(
            schemaVersion: new AIAssetSchemaVersion("2.0"),
            assetType: (AIAssetDocumentType)999);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainEquivalentOf(
            new AIAssetDocumentValidationError(
                AIAssetDocumentValidationCodes.UnsupportedSchemaVersion,
                "The AI Asset document schema version is not supported.",
                "$.schemaVersion"));
        result.Errors.Should().ContainEquivalentOf(
            new AIAssetDocumentValidationError(
                AIAssetDocumentValidationCodes.UnsupportedAssetType,
                "The AI Asset document type is not supported by this schema.",
                "$.assetType"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateIdentityAndMetadataWithPaths()
    {
        var document = CreateDocument(
            identity: new AIAssetIdentityDocument
            {
                Id = Guid.Empty.ToString(),
                Urn = " ",
                Version = string.Empty
            },
            metadata: new AIAssetMetadataDocument(
                " ",
                tags: ["valid", " "]));

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidIdentityId
            && error.Path == "$.identity.id");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingIdentityUrn
            && error.Path == "$.identity.urn");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingIdentityVersion
            && error.Path == "$.identity.version");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingMetadataName
            && error.Path == "$.metadata.name");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidMetadataTag
            && error.Path == "$.metadata.tags[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateReferenceFieldsWithIndexedPaths()
    {
        var reference = new AIAssetReferenceDocument
        {
            AssetType = (AIAssetDocumentType)999,
            AssetId = "not-a-guid",
            Version = " "
        };
        var document = CreateDocument(references: [reference]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.UnsupportedReferenceAssetType
            && error.Path == "$.references[0].assetType");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidReferenceAssetId
            && error.Path == "$.references[0].assetId");
        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingReferenceVersion
            && error.Path == "$.references[0].version");
    }

    [Fact]
    public async Task ValidateAsync_ShouldDetectLogicalDuplicateReferences()
    {
        var id = Guid.NewGuid();
        var first = CreateReference(id.ToString("D"));
        var second = CreateReference(id.ToString("D").ToUpperInvariant());
        var document = CreateDocument(references: [first, second]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.DuplicateReference
            && error.Path == "$.references[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateDependenciesAndDetectDuplicates()
    {
        var id = Guid.NewGuid();
        var firstReference = CreateReference(id.ToString());
        var secondReference = CreateReference(id.ToString().ToUpperInvariant());
        var document = CreateDocument(
            dependencies:
            [
                new AIAssetDependencyDocument
                {
                    Reference = firstReference,
                    Required = true
                },
                new AIAssetDependencyDocument
                {
                    Reference = secondReference,
                    Required = false
                }
            ]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.DuplicateDependency
            && error.Path == "$.dependencies[1]");
    }

    [Fact]
    public async Task ValidateAsync_ShouldAggregateErrorsAcrossDocumentSections()
    {
        var invalidReference = new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Model,
            AssetId = Guid.Empty.ToString(),
            Version = string.Empty
        };
        var document = CreateDocument(
            schemaVersion: new AIAssetSchemaVersion("9.0"),
            identity: new AIAssetIdentityDocument
            {
                Id = "invalid",
                Urn = string.Empty,
                Version = string.Empty
            },
            metadata: new AIAssetMetadataDocument(string.Empty),
            references: [invalidReference],
            dependencies:
            [
                new AIAssetDependencyDocument
                {
                    Reference = invalidReference
                }
            ]);

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(5);
        result.Errors.Select(error => error.Code).Should().Contain(
            AIAssetDocumentValidationCodes.UnsupportedSchemaVersion,
            AIAssetDocumentValidationCodes.InvalidIdentityId,
            AIAssetDocumentValidationCodes.MissingMetadataName,
            AIAssetDocumentValidationCodes.InvalidReferenceAssetId,
            AIAssetDocumentValidationCodes.MissingReferenceVersion);
    }

    [Fact]
    public void ValidationResult_ShouldSnapshotIncomingErrors()
    {
        var errors = new List<AIAssetDocumentValidationError>
        {
            new(
                AIAssetDocumentValidationCodes.InvalidIdentityId,
                "Invalid identity.",
                "$.identity.id")
        };

        var result = new AIAssetDocumentValidationResult(errors);
        errors.Clear();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    private static TestAssetDocument CreateDocument(
        AIAssetSchemaVersion? schemaVersion = null,
        AIAssetDocumentType assetType = AIAssetDocumentType.Agent,
        AIAssetIdentityDocument? identity = null,
        AIAssetMetadataDocument? metadata = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        return new TestAssetDocument(
            schemaVersion ?? AIAssetSchemaVersion.V1,
            assetType,
            identity ?? new AIAssetIdentityDocument
            {
                Id = Guid.NewGuid().ToString(),
                Urn = "urn:pulsestack:agent:test",
                Version = "1.0.0"
            },
            metadata ?? new AIAssetMetadataDocument("Test Agent"),
            AIAssetLifecycleDocument.Draft,
            references,
            dependencies);
    }

    private static AIAssetReferenceDocument CreateReference(string assetId)
    {
        return new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Model,
            AssetId = assetId,
            Version = "1.0.0"
        };
    }

    private sealed record TestAssetDocument : AIAssetDocument
    {
        public TestAssetDocument(
            AIAssetSchemaVersion schemaVersion,
            AIAssetDocumentType assetType,
            AIAssetIdentityDocument identity,
            AIAssetMetadataDocument metadata,
            AIAssetLifecycleDocument lifecycle,
            IEnumerable<AIAssetReferenceDocument>? references = null,
            IEnumerable<AIAssetDependencyDocument>? dependencies = null)
            : base(
                schemaVersion,
                assetType,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies)
        {
        }
    }
}
