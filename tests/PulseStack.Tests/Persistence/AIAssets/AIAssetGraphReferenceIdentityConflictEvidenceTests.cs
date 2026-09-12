using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphReferenceIdentityConflictEvidenceTests
{
    [Fact]
    public void RequiredPersistentResolverEvidence_ShouldExposeReferenceMismatchPredecessorOutcome()
    {
        var (root, relationship, path) = CreateRelationshipContext(required: true);

        var context = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);

        context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);
        context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Required);
        context.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.ReferenceMismatch);
    }

    [Fact]
    public void ExcludedPersistentResolverEvidence_ShouldBeRejected()
    {
        var (root, relationship, path) = CreateRelationshipContext(required: false);

        Action act = () => _ = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequiredOperationLocalIdentityEvidence_ShouldExposeNoPredecessorOutcome()
    {
        var (root, relationship, path) = CreateRelationshipContext(required: true);

        var context = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);

        context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);
        context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Required);
        context.PredecessorSemanticOutcome.Should().BeNull();
    }

    [Fact]
    public void ExcludedOperationLocalIdentityEvidence_ShouldBeAcceptedWithoutPredecessorOutcome()
    {
        var (root, relationship, path) = CreateRelationshipContext(required: false);

        var context = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);

        context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);
        context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
        context.DependencyRequired.Should().BeFalse();
        context.PredecessorSemanticOutcome.Should().BeNull();
    }

    [Fact]
    public void UnsupportedEvidence_ShouldBeRejected()
    {
        var (root, relationship, path) = CreateRelationshipContext(required: true);

        Action act = () => _ = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            (AIAssetGraphReferenceIdentityConflictEvidence)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Evidence_ShouldBeExplicitAndPredecessorOutcomeDerived_NotCallerSupplied()
    {
        Enum.GetNames<AIAssetGraphReferenceIdentityConflictEvidence>()
            .Should().Equal("PersistentResolver", "OperationLocalIdentity");

        var constructors = typeof(AIAssetGraphReferenceIdentityConflictContext).GetConstructors();
        constructors.Should().ContainSingle();
        constructors[0].GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(
                typeof(AssetDefinitionKey),
                typeof(AIAssetGraphPath),
                typeof(AIAssetGraphRelationship),
                typeof(AIAssetGraphReferenceIdentityConflictEvidence));
        constructors[0].GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().NotContain(typeof(AIAssetGraphPredecessorSemanticOutcome));
    }

    private static (
        AssetDefinitionKey Root,
        AIAssetGraphRelationship Relationship,
        AIAssetGraphPath Path) CreateRelationshipContext(bool required)
    {
        var root = new AssetDefinitionKey(
            AssetType.Package,
            AssetId.New(),
            new AssetVersion("1.0"));
        var target = new AssetReference(
            AssetType.Prompt,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}"),
            new AssetVersion("1.0"));
        var relationship = new AIAssetGraphRelationship(
            root,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            required
                ? AIAssetGraphMaterializationAuthority.Required
                : AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            required,
            0,
            "$.dependencies[0]");
        var path = new AIAssetGraphPath(
            root,
            new[] { new AIAssetGraphPathSegment(relationship) });

        return (root, relationship, path);
    }
}
