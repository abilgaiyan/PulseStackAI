using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphReferenceIdentityConflictEvidenceTests
{
    [Fact]
    public void PersistentResolverEvidence_ShouldExposeReferenceMismatchPredecessorOutcome()
    {
        var (root, relationship, path) = CreateRelationshipContext();

        var context = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);

        context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);
        context.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.ReferenceMismatch);
    }

    [Fact]
    public void OperationLocalIdentityEvidence_ShouldExposeNoPredecessorOutcome()
    {
        var (root, relationship, path) = CreateRelationshipContext();

        var context = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);

        context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);
        context.PredecessorSemanticOutcome.Should().BeNull();
    }

    [Fact]
    public void UnsupportedEvidence_ShouldBeRejected()
    {
        var (root, relationship, path) = CreateRelationshipContext();

        Action act = () => _ = new AIAssetGraphReferenceIdentityConflictContext(
            root,
            path,
            relationship,
            (AIAssetGraphReferenceIdentityConflictEvidence)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PredecessorOutcome_ShouldBeDerivedFromEvidence_NotCallerSupplied()
    {
        Enum.GetNames<AIAssetGraphReferenceIdentityConflictEvidence>()
            .Should().Equal("PersistentResolver", "OperationLocalIdentity");

        typeof(AIAssetGraphReferenceIdentityConflictContext)
            .GetConstructors()
            .SelectMany(static constructor => constructor.GetParameters())
            .Select(static parameter => parameter.ParameterType)
            .Should().NotContain(typeof(AIAssetGraphPredecessorSemanticOutcome));
    }

    private static (
        AssetDefinitionKey Root,
        AIAssetGraphRelationship Relationship,
        AIAssetGraphPath Path) CreateRelationshipContext()
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
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            0,
            "$.dependencies[0]");
        var path = new AIAssetGraphPath(
            root,
            new[] { new AIAssetGraphPathSegment(relationship) });

        return (root, relationship, path);
    }
}
