using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphContractTests
{
    [Fact]
    public void LoaderInterface_ShouldExposeExactlyOneLoadOperation()
    {
        var methods = typeof(IAIAssetGraphLoader).GetMethods();

        methods.Should().ContainSingle();
        var method = methods.Single();
        method.Name.Should().Be(nameof(IAIAssetGraphLoader.LoadAsync));
        method.ReturnType.Should().Be(typeof(ValueTask<AIAssetGraphLoadResult>));
        method.GetParameters().Select(static p => p.ParameterType)
            .Should().Equal(typeof(AssetDefinitionKey), typeof(CancellationToken));
        method.GetParameters()[1].HasDefaultValue.Should().BeTrue();
    }

    [Fact]
    public void DiagnosticCodes_ShouldBeExactAndStable()
    {
        AIAssetGraphDiagnosticCodes.RootDefinitionUnavailable.Should().Be("AAG001");
        AIAssetGraphDiagnosticCodes.RequiredDefinitionUnavailable.Should().Be("AAG002");
        AIAssetGraphDiagnosticCodes.ReferenceIdentityConflict.Should().Be("AAG003");
        AIAssetGraphDiagnosticCodes.LineageIdentityConflict.Should().Be("AAG004");
        AIAssetGraphDiagnosticCodes.RequiredMaterializationCycle.Should().Be("AAG005");
    }

    [Fact]
    public void PublicEnums_ShouldExposeOnlyFrozenVocabulary()
    {
        Enum.GetNames<AIAssetGraphRelationshipClass>().Should().Equal(
            "DistinguishedStructural",
            "InternalOwnership",
            "InternalMembership",
            "InternalDistribution",
            "DeclarativeReference",
            "ExplicitRequirement");

        Enum.GetNames<AIAssetGraphMaterializationAuthority>().Should().Equal("Required", "Excluded");
        Enum.GetNames<AIAssetGraphBoundaryRole>().Should().Equal("Structural", "Internal", "External", "NotApplicable");
        Enum.GetNames<AIAssetGraphPredecessorSemanticOutcome>().Should().Equal("DefinitionNotPublished", "ReferenceMismatch");
    }

    [Fact]
    public void GraphNode_ShouldRejectAssetIdentityMismatch()
    {
        var key = Key(AssetType.Project);
        var asset = Asset(AssetType.Project);

        Action act = () => _ = new AIAssetGraphNode(key, asset);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Relationship_ShouldRejectUnsupportedEnumValues()
    {
        var source = Key(AssetType.Project);
        var target = Reference(AssetType.Workflow);

        Action act = () => _ = new AIAssetGraphRelationship(
            source,
            target,
            (AIAssetGraphRelationshipClass)999,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.Structural,
            null,
            0,
            "$.entryWorkflow");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExplicitRequirement_ShouldRequireDependencyRequiredAndMatchingAuthority()
    {
        var source = Key(AssetType.Package);
        var target = Reference(AssetType.Prompt);

        Action missing = () => _ = Relationship(source, target, dependencyRequired: null);
        Action wrongAuthority = () => _ = new AIAssetGraphRelationship(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            false,
            0,
            "$.dependencies[0]");

        missing.Should().Throw<ArgumentException>();
        wrongAuthority.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OptionalRequirement_ShouldRemainExcludedEvenWhenTargetNodeIsPresent()
    {
        var rootAsset = Asset(AssetType.Package);
        var targetAsset = Asset(AssetType.Prompt);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var targetKey = AssetDefinitionKey.From(targetAsset);
        var relationship = Relationship(
            rootKey,
            Reference(targetAsset),
            dependencyRequired: false,
            authority: AIAssetGraphMaterializationAuthority.Excluded);

        var graph = new AIAssetGraph(
            rootKey,
            new[] { new AIAssetGraphNode(rootKey, rootAsset), new AIAssetGraphNode(targetKey, targetAsset) },
            new[] { relationship });

        graph.Relationships.Should().ContainSingle();
        graph.Relationships[0].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
        graph.Relationships[0].DependencyRequired.Should().BeFalse();
    }

    [Fact]
    public void OptionalRequirement_TargetMayBeAbsent()
    {
        var rootAsset = Asset(AssetType.Package);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var relationship = Relationship(
            rootKey,
            Reference(AssetType.Prompt),
            dependencyRequired: false,
            authority: AIAssetGraphMaterializationAuthority.Excluded);

        var graph = new AIAssetGraph(
            rootKey,
            new[] { new AIAssetGraphNode(rootKey, rootAsset) },
            new[] { relationship });

        graph.Relationships.Should().ContainSingle();
    }

    [Fact]
    public void RequiredRelationship_TargetMustBePresent()
    {
        var rootAsset = Asset(AssetType.Package);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var relationship = Relationship(rootKey, Reference(AssetType.Prompt), dependencyRequired: true);

        Action act = () => _ = new AIAssetGraph(
            rootKey,
            new[] { new AIAssetGraphNode(rootKey, rootAsset) },
            new[] { relationship });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MaterializedRequiredTarget_ShouldRejectAuthoredUrnMismatch()
    {
        var rootAsset = Asset(AssetType.Package);
        var targetAsset = Asset(AssetType.Prompt);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var targetKey = AssetDefinitionKey.From(targetAsset);
        var conflictingReference = new AssetReference(
            targetAsset.Type,
            targetAsset.Id,
            new AssetUrn("urn:pulsestack:prompt:conflicting"),
            targetAsset.Version);
        var relationship = Relationship(rootKey, conflictingReference, dependencyRequired: true);

        Action act = () => _ = new AIAssetGraph(
            rootKey,
            new[] { new AIAssetGraphNode(rootKey, rootAsset), new AIAssetGraphNode(targetKey, targetAsset) },
            new[] { relationship });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MaterializedOptionalTarget_ShouldRejectAuthoredUrnMismatch()
    {
        var rootAsset = Asset(AssetType.Package);
        var targetAsset = Asset(AssetType.Prompt);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var targetKey = AssetDefinitionKey.From(targetAsset);
        var conflictingReference = new AssetReference(
            targetAsset.Type,
            targetAsset.Id,
            new AssetUrn("urn:pulsestack:prompt:conflicting"),
            targetAsset.Version);
        var relationship = Relationship(
            rootKey,
            conflictingReference,
            dependencyRequired: false,
            authority: AIAssetGraphMaterializationAuthority.Excluded);

        Action act = () => _ = new AIAssetGraph(
            rootKey,
            new[] { new AIAssetGraphNode(rootKey, rootAsset), new AIAssetGraphNode(targetKey, targetAsset) },
            new[] { relationship });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Graph_ShouldRejectDuplicateNodeIdentity()
    {
        var rootAsset = Asset(AssetType.Project);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var node = new AIAssetGraphNode(rootKey, rootAsset);

        Action act = () => _ = new AIAssetGraph(rootKey, new[] { node, node }, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Graph_ShouldRejectDuplicateLocalOrdinalPerSource()
    {
        var rootAsset = Asset(AssetType.Package);
        var firstAsset = Asset(AssetType.Prompt);
        var secondAsset = Asset(AssetType.Tool);
        var rootKey = AssetDefinitionKey.From(rootAsset);

        var first = Relationship(rootKey, Reference(firstAsset), dependencyRequired: true, localOrdinal: 0, path: "$.dependencies[0]");
        var second = Relationship(rootKey, Reference(secondAsset), dependencyRequired: true, localOrdinal: 0, path: "$.dependencies[1]");

        Action act = () => _ = new AIAssetGraph(
            rootKey,
            new[]
            {
                new AIAssetGraphNode(rootKey, rootAsset),
                new AIAssetGraphNode(AssetDefinitionKey.From(firstAsset), firstAsset),
                new AIAssetGraphNode(AssetDefinitionKey.From(secondAsset), secondAsset)
            },
            new[] { first, second });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Graph_ShouldSnapshotAndNormalizeCollections()
    {
        var rootAsset = Asset(AssetType.Package);
        var promptAsset = Asset(AssetType.Prompt);
        var rootKey = AssetDefinitionKey.From(rootAsset);
        var promptKey = AssetDefinitionKey.From(promptAsset);
        var relationship = Relationship(rootKey, Reference(promptAsset), dependencyRequired: true);
        var nodes = new List<AIAssetGraphNode>
        {
            new(promptKey, promptAsset),
            new(rootKey, rootAsset)
        };
        var relationships = new List<AIAssetGraphRelationship> { relationship };

        var graph = new AIAssetGraph(rootKey, nodes, relationships);
        nodes.Clear();
        relationships.Clear();

        graph.Nodes.Should().HaveCount(2);
        graph.Nodes[0].DefinitionKey.Type.Should().Be(AssetType.Package);
        graph.Relationships.Should().ContainSingle();
        graph.Nodes.Should().NotBeAssignableTo<AIAssetGraphNode[]>();
        graph.Relationships.Should().NotBeAssignableTo<AIAssetGraphRelationship[]>();
    }

    [Fact]
    public void RootFailurePath_ShouldAlwaysBeEmpty()
    {
        var root = Key(AssetType.Project);
        var context = new AIAssetGraphRootDefinitionUnavailableContext(root);

        context.Code.Should().Be("AAG001");
        context.RootKey.Should().Be(root);
        context.CanonicalPath.Segments.Should().BeEmpty();
        context.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.DefinitionNotPublished);
    }

    [Fact]
    public void DescendantFailurePath_ShouldPreserveOrderedRelationshipSegments()
    {
        var root = Key(AssetType.Package);
        var firstRef = Reference(AssetType.Library);
        var first = new AIAssetGraphRelationship(
            root,
            firstRef,
            AIAssetGraphRelationshipClass.InternalDistribution,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.Internal,
            null,
            0,
            "$.members[0]");
        var second = Relationship(AssetDefinitionKey.From(firstRef), Reference(AssetType.Prompt), true, 1, "$.dependencies[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(first), new AIAssetGraphPathSegment(second) });

        path.Segments.Select(static segment => segment.LocalOrdinal).Should().Equal(0, 1);
        path.Segments[^1].AuthoredPath.Should().Be("$.dependencies[0]");
    }

    [Fact]
    public void Path_ShouldRejectDiscontinuousSegments()
    {
        var root = Key(AssetType.Package);
        var relationship = Relationship(Key(AssetType.Library), Reference(AssetType.Prompt), true);

        Action act = () => _ = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(relationship) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolverTranslatedContexts_ShouldPreserveSemanticOutcome()
    {
        var root = Key(AssetType.Package);
        var requiredRelationship = Relationship(root, Reference(AssetType.Prompt), true);
        var requiredPath = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(requiredRelationship) });
        var referenceRelationship = Relationship(root, Reference(AssetType.Tool), true, 1, "$.dependencies[1]");
        var referencePath = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(referenceRelationship) });

        var required = new AIAssetGraphRequiredDefinitionUnavailableContext(root, requiredPath, requiredRelationship);
        var conflict = new AIAssetGraphReferenceIdentityConflictContext(root, referencePath, referenceRelationship);

        required.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.DefinitionNotPublished);
        conflict.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.ReferenceMismatch);
    }

    [Fact]
    public void LineageIdentityConflict_ShouldPreserveFailingRelationshipOccurrence()
    {
        var root = Key(AssetType.Package);
        var conflictingReference = Reference(AssetType.Prompt);
        var conflictingIdentity = AssetDefinitionKey.From(conflictingReference);
        var establishedIdentity = new AssetDefinitionKey(
            AssetType.Tool,
            AssetId.New(),
            conflictingIdentity.Version);
        var relationship = Relationship(root, conflictingReference, true, 2, "$.dependencies[2]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(relationship) });

        var context = new AIAssetGraphLineageIdentityConflictContext(
            root,
            path,
            relationship,
            conflictingReference.Urn,
            establishedIdentity,
            conflictingIdentity);

        context.Code.Should().Be("AAG004");
        context.SourceKey.Should().Be(relationship.SourceKey);
        context.TargetReference.Should().Be(relationship.TargetReference);
        context.LocalOrdinal.Should().Be(2);
        context.AuthoredPath.Should().Be("$.dependencies[2]");
        context.Relationship.Should().BeSameAs(relationship);
        context.EstablishedIdentity.Should().Be(establishedIdentity);
        context.ConflictingIdentity.Should().Be(conflictingIdentity);
        context.ConflictingUrn.Should().Be(conflictingReference.Urn);
    }

    [Fact]
    public void LineageIdentityConflict_ShouldRejectPathWhoseFinalSegmentIsNotFailingOccurrence()
    {
        var root = Key(AssetType.Package);
        var conflictingReference = Reference(AssetType.Prompt);
        var conflictingIdentity = AssetDefinitionKey.From(conflictingReference);
        var establishedIdentity = new AssetDefinitionKey(AssetType.Tool, AssetId.New(), conflictingIdentity.Version);
        var relationship = Relationship(root, conflictingReference, true, 0, "$.dependencies[0]");
        var other = Relationship(root, Reference(AssetType.Tool), true, 1, "$.dependencies[1]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(other) });

        Action act = () => _ = new AIAssetGraphLineageIdentityConflictContext(
            root,
            path,
            relationship,
            conflictingReference.Urn,
            establishedIdentity,
            conflictingIdentity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LineageIdentityConflict_ShouldRejectConflictingIdentityNotIntroducedByRelationship()
    {
        var root = Key(AssetType.Package);
        var conflictingReference = Reference(AssetType.Prompt);
        var relationship = Relationship(root, conflictingReference, true, 0, "$.dependencies[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(relationship) });
        var relationshipIdentity = AssetDefinitionKey.From(conflictingReference);
        var establishedIdentity = new AssetDefinitionKey(AssetType.Tool, AssetId.New(), relationshipIdentity.Version);
        var differentConflictingIdentity = new AssetDefinitionKey(
            relationshipIdentity.Type,
            AssetId.New(),
            relationshipIdentity.Version);

        Action act = () => _ = new AIAssetGraphLineageIdentityConflictContext(
            root,
            path,
            relationship,
            conflictingReference.Urn,
            establishedIdentity,
            differentConflictingIdentity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LineageIdentityConflict_ShouldRejectConflictingUrnNotAssertedByRelationship()
    {
        var root = Key(AssetType.Package);
        var conflictingReference = Reference(AssetType.Prompt);
        var conflictingIdentity = AssetDefinitionKey.From(conflictingReference);
        var establishedIdentity = new AssetDefinitionKey(AssetType.Tool, AssetId.New(), conflictingIdentity.Version);
        var relationship = Relationship(root, conflictingReference, true, 0, "$.dependencies[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(relationship) });

        Action act = () => _ = new AIAssetGraphLineageIdentityConflictContext(
            root,
            path,
            relationship,
            new AssetUrn("urn:pulsestack:prompt:different"),
            establishedIdentity,
            conflictingIdentity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CycleContext_ShouldValidateClosingEdgeEntryAndStartIndex()
    {
        var root = Key(AssetType.Package);
        var childRef = Reference(AssetType.Library);
        var childKey = AssetDefinitionKey.From(childRef);
        var first = new AIAssetGraphRelationship(
            root,
            childRef,
            AIAssetGraphRelationshipClass.InternalDistribution,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.Internal,
            null,
            0,
            "$.members[0]");
        var closingRef = new AssetReference(root.Type, root.Id, new AssetUrn("urn:pulsestack:package:root"), root.Version);
        var closing = Relationship(childKey, closingRef, true, 0, "$.dependencies[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(first), new AIAssetGraphPathSegment(closing) });

        var context = new AIAssetGraphRequiredMaterializationCycleContext(root, path, closing, root, 0);

        context.Code.Should().Be("AAG005");
        context.CycleEntryKey.Should().Be(root);
        context.CycleStartSegmentIndex.Should().Be(0);
        context.CanonicalPath.Segments[^1].Relationship.Should().BeSameAs(closing);
    }

    [Fact]
    public void SelfCycle_ShouldUseSegmentZeroAsCycleStart()
    {
        var root = Key(AssetType.Package);
        var reference = new AssetReference(root.Type, root.Id, new AssetUrn("urn:pulsestack:package:self"), root.Version);
        var closing = Relationship(root, reference, true, 0, "$.dependencies[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(closing) });

        var context = new AIAssetGraphRequiredMaterializationCycleContext(root, path, closing, root, 0);

        context.CycleStartSegmentIndex.Should().Be(0);
    }

    [Fact]
    public void ResultAlgebra_ShouldExposeExactClosedVariantSet()
    {
        typeof(AIAssetGraphLoadResult).GetNestedTypes()
            .Select(static type => type.Name)
            .Should().BeEquivalentTo(
                "Success",
                "RootDefinitionUnavailable",
                "RequiredDefinitionUnavailable",
                "ReferenceIdentityConflict",
                "LineageIdentityConflict",
                "RequiredMaterializationCycle");
    }

    [Fact]
    public void OnlySuccessResult_ShouldExposeGraphProperty()
    {
        var variants = typeof(AIAssetGraphLoadResult).GetNestedTypes();
        var success = variants.Single(static type => type.Name == "Success");

        success.GetProperty(nameof(AIAssetGraphLoadResult.Success.Graph)).Should().NotBeNull();
        variants.Where(type => type != success)
            .Select(static type => type.GetProperty("Graph"))
            .Should().OnlyContain(static property => property == null);
    }

    [Fact]
    public void PublicGraphContracts_ShouldNotReferenceProviderOrRuntimeRealizationNamespaces()
    {
        var assembly = typeof(IAIAssetGraphLoader).Assembly;
        var graphTypes = assembly.GetExportedTypes()
            .Where(static type => type.Namespace == "PulseStack.Abstractions.Persistence.AIAssets.GraphLoading")
            .ToArray();

        graphTypes.Should().NotBeEmpty();
        graphTypes.SelectMany(static type => type.GetProperties())
            .Select(static property => property.PropertyType.FullName ?? string.Empty)
            .Should().NotContain(name => name.Contains("Provider", StringComparison.Ordinal)
                || name.Contains("Runtime", StringComparison.Ordinal));
    }

    private static AIAssetGraphRelationship Relationship(
        AssetDefinitionKey source,
        AssetReference target,
        bool? dependencyRequired,
        int localOrdinal = 0,
        string path = "$.dependencies[0]",
        AIAssetGraphMaterializationAuthority authority = AIAssetGraphMaterializationAuthority.Required) =>
        new(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            authority,
            AIAssetGraphBoundaryRole.External,
            dependencyRequired,
            localOrdinal,
            path);

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0"));

    private static AssetReference Reference(AssetType type) =>
        new(type, AssetId.New(), new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{Guid.NewGuid():N}"), new AssetVersion("1.0"));

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static TestAsset Asset(AssetType type) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{Guid.NewGuid():N}"),
            new AssetVersion("1.0"));

    private sealed class TestAsset : IAsset
    {
        public TestAsset(AssetType type, AssetId id, AssetUrn urn, AssetVersion version)
        {
            Type = type;
            Id = id;
            Urn = urn;
            Version = version;
        }

        public AssetId Id { get; }
        public AssetUrn Urn { get; }
        public AssetVersion Version { get; }
        public AssetMetadata Metadata { get; } = new() { Name = "test" };
        public AssetType Type { get; }
        public AssetLifecycle Lifecycle { get; } = AssetLifecycle.Published;
        public IReadOnlyCollection<AssetReference> References { get; } = Array.Empty<AssetReference>();
        public IReadOnlyCollection<AssetDependency> Dependencies { get; } = Array.Empty<AssetDependency>();
    }
}
