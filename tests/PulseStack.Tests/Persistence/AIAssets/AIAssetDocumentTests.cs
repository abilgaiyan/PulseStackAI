using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentTests
{
    private const string AssetId = "11111111-1111-1111-1111-111111111111";
    private const string ReferenceId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public void Constructor_ShouldKeepSchemaVersionSeparateFromAssetVersion()
    {
        var document = CreateDocument(assetVersion: "2.1.0");

        document.SchemaVersion.Should().Be(AIAssetSchemaVersion.V1);
        document.Identity.Version.Should().Be("2.1.0");
    }

    [Fact]
    public void Constructor_ShouldSnapshotReferencesAndDependencies()
    {
        var reference = CreateReference();
        var references = new List<AIAssetReferenceDocument> { reference };
        var dependencies = new List<AIAssetDependencyDocument>
        {
            new()
            {
                Reference = reference,
                Required = true
            }
        };

        var document = CreateDocument(
            references: references,
            dependencies: dependencies);

        references.Clear();
        dependencies.Clear();

        document.References.Should().ContainSingle().Which.Should().Be(reference);
        document.Dependencies.Should().ContainSingle().Which.Reference.Should().Be(reference);
    }

    [Fact]
    public void ReferenceAndDependencyEquality_ShouldBeValueBased()
    {
        var firstReference = CreateReference();
        var secondReference = CreateReference();
        var firstDependency = new AIAssetDependencyDocument
        {
            Reference = firstReference,
            Required = true
        };
        var secondDependency = new AIAssetDependencyDocument
        {
            Reference = secondReference,
            Required = true
        };

        firstReference.Should().Be(secondReference);
        firstDependency.Should().Be(secondDependency);
    }

    [Fact]
    public void Equality_ShouldCompareCompleteDocumentContents()
    {
        var first = CreateEquivalentDocument();
        var second = CreateEquivalentDocument();

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    private static TestAssetDocument CreateEquivalentDocument()
    {
        var reference = CreateReference();

        return new TestAssetDocument(
            AIAssetSchemaVersion.V1,
            AIAssetDocumentType.Agent,
            new AIAssetIdentityDocument
            {
                Id = AssetId,
                Urn = "urn:pulsestack:agent:test",
                Version = "2.1.0"
            },
            new AIAssetMetadataDocument(
                "Test Agent",
                "Equivalent document",
                "PulseStackAI",
                ["agent", "test"],
                "Agent"),
            AIAssetLifecycleDocument.Published,
            [reference],
            [new AIAssetDependencyDocument
            {
                Reference = reference,
                Required = true
            }]);
    }

    private static TestAssetDocument CreateDocument(
        string assetVersion = "1.0.0",
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        return new TestAssetDocument(
            AIAssetSchemaVersion.V1,
            AIAssetDocumentType.Agent,
            new AIAssetIdentityDocument
            {
                Id = Guid.NewGuid().ToString(),
                Urn = "urn:pulsestack:agent:test",
                Version = assetVersion
            },
            new AIAssetMetadataDocument("Test Agent"),
            AIAssetLifecycleDocument.Draft,
            references,
            dependencies);
    }

    private static AIAssetReferenceDocument CreateReference()
    {
        return new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Model,
            AssetId = ReferenceId,
            Urn = "urn:pulsestack:model:test",
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
