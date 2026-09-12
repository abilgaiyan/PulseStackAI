using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogContractTests
{
    [Fact]
    public void CatalogRecord_ShouldPreserveExactDefinitionIdentityAndUrn()
    {
        var key = CreateKey();
        var urn = new AssetUrn("urn:pulsestack:prompt:example");

        var record = new CatalogRecord(key, urn);

        record.DefinitionKey.Should().Be(key);
        record.Urn.Should().Be(urn);
    }

    [Fact]
    public void CatalogRecord_ShouldReuseMS0097ExactKeyValidityRules()
    {
        var providerKey = new AssetDefinitionKey(AssetType.Provider, AssetId.New(), AssetVersion.Initial);
        var emptyIdKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.Empty, AssetVersion.Initial);
        var emptyVersionKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion(" "));
        var urn = new AssetUrn("urn:pulsestack:prompt:example");

        Action provider = () => _ = new CatalogRecord(providerKey, urn);
        Action emptyId = () => _ = new CatalogRecord(emptyIdKey, urn);
        Action emptyVersion = () => _ = new CatalogRecord(emptyVersionKey, urn);

        provider.Should().Throw<ArgumentException>();
        emptyId.Should().Throw<ArgumentException>();
        emptyVersion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CatalogRecord_ShouldRejectMissingUrn()
    {
        var key = CreateKey();

        Action nullUrn = () => _ = new CatalogRecord(key, null!);
        Action blankUrn = () => _ = new CatalogRecord(key, new AssetUrn(" "));

        nullUrn.Should().Throw<ArgumentNullException>();
        blankUrn.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CatalogLineage_ShouldOwnANonEmptyExactVersionSet()
    {
        var first = new AssetVersion("1.0");
        var second = new AssetVersion("2.0");
        var input = new List<AssetVersion> { first, second, first };

        var lineage = new CatalogLineage(
            AssetType.Agent,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:agent:example"),
            input);

        input.Clear();

        lineage.PublishedVersions.Should().BeEquivalentTo(new[] { first, second });
        lineage.PublishedVersions.Should().HaveCount(2);
    }

    [Fact]
    public void CatalogLineage_ShouldNotPublishValueEqualitySemantics()
    {
        var id = AssetId.New();
        var urn = new AssetUrn("urn:pulsestack:agent:example");
        var version = new AssetVersion("1.0");

        var left = new CatalogLineage(AssetType.Agent, id, urn, new[] { version });
        var right = new CatalogLineage(AssetType.Agent, id, urn, new[] { version });

        left.Should().NotBeSameAs(right);
        left.Equals(right).Should().BeFalse();
        ReferenceEquals(left, right).Should().BeFalse();
    }

    [Fact]
    public void CatalogLineage_ShouldRejectUnsupportedOrIncompleteIdentity()
    {
        var urn = new AssetUrn("urn:pulsestack:agent:example");
        var versions = new[] { AssetVersion.Initial };

        Action provider = () => _ = new CatalogLineage(AssetType.Provider, AssetId.New(), urn, versions);
        Action emptyId = () => _ = new CatalogLineage(AssetType.Agent, AssetId.Empty, urn, versions);
        Action blankUrn = () => _ = new CatalogLineage(AssetType.Agent, AssetId.New(), new AssetUrn(" "), versions);
        Action noVersions = () => _ = new CatalogLineage(AssetType.Agent, AssetId.New(), urn, Array.Empty<AssetVersion>());
        Action invalidVersion = () => _ = new CatalogLineage(
            AssetType.Agent,
            AssetId.New(),
            urn,
            new[] { new AssetVersion(" ") });
        Action nullVersion = () => _ = new CatalogLineage(
            AssetType.Agent,
            AssetId.New(),
            urn,
            new AssetVersion[] { null! });

        provider.Should().Throw<ArgumentException>();
        emptyId.Should().Throw<ArgumentException>();
        blankUrn.Should().Throw<ArgumentException>();
        noVersions.Should().Throw<ArgumentException>();
        invalidVersion.Should().Throw<ArgumentException>();
        nullVersion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LookupResults_ShouldCarryValuesOnlyForFoundCases()
    {
        var record = new CatalogRecord(CreateKey(), new AssetUrn("urn:pulsestack:prompt:example"));
        var lineage = new CatalogLineage(
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            new[] { record.DefinitionKey.Version });

        var exactFound = new ExactCatalogLookupResult.Found(record);
        var lineageFound = new CatalogLineageLookupResult.Found(lineage);

        exactFound.Record.Should().BeSameAs(record);
        lineageFound.Lineage.Should().BeSameAs(lineage);
        new ExactCatalogLookupResult.NotFound().Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        new CatalogLineageLookupResult.NotFound().Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public void LookupResults_ShouldRejectNullFoundValues()
    {
        Action exact = () => _ = new ExactCatalogLookupResult.Found(null!);
        Action lineage = () => _ = new CatalogLineageLookupResult.Found(null!);

        exact.Should().Throw<ArgumentNullException>();
        lineage.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PortableResultAlgebra_ShouldRemainClosedAndDistinguishable()
    {
        Enum.GetValues<CatalogPublicationResult>().Should().BeEquivalentTo(new[]
        {
            CatalogPublicationResult.Created,
            CatalogPublicationResult.AlreadyPresent,
            CatalogPublicationResult.Conflict
        });

        Enum.GetValues<AIAssetPublicationResult>().Should().BeEquivalentTo(new[]
        {
            AIAssetPublicationResult.Published,
            AIAssetPublicationResult.AlreadyPublished,
            AIAssetPublicationResult.DefinitionNotStored,
            AIAssetPublicationResult.IdentityConflict
        });

        Enum.GetValues<AIAssetCatalogFailureCategory>().Should().BeEquivalentTo(new[]
        {
            AIAssetCatalogFailureCategory.CompositionConfiguration,
            AIAssetCatalogFailureCategory.ProviderFailure,
            AIAssetCatalogFailureCategory.InconsistentState
        });

        Enum.GetValues<AIAssetCatalogBoundaryFailureCategory>().Should().BeEquivalentTo(new[]
        {
            AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable,
            AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch
        });
    }

    [Fact]
    public void CapabilityProfiles_ShouldKeepStorageAndCatalogEvidenceDistinct()
    {
        var storage = new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Durable);
        var catalog = new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Transient);

        storage.Durability.Should().Be(AIAssetAuthorityDurability.Durable);
        catalog.Durability.Should().Be(AIAssetAuthorityDurability.Transient);
        storage.GetType().Should().NotBe(catalog.GetType());
    }

    [Fact]
    public void PortableContracts_ShouldExposeOnlyFrozenOperationFamilies()
    {
        typeof(IAIAssetCatalogProvider).GetMethods().Select(method => method.Name).Should().BeEquivalentTo(
            nameof(IAIAssetCatalogProvider.FindExactAsync),
            nameof(IAIAssetCatalogProvider.FindLineageAsync),
            nameof(IAIAssetCatalogProvider.PublishAsync));

        typeof(IAIAssetPublisher).GetMethods().Select(method => method.Name).Should().Equal(
            nameof(IAIAssetPublisher.PublishAsync));

        typeof(IPersistentAIAssetResolver).GetMethods().Select(method => method.Name).Should().OnlyContain(name =>
            name == nameof(IPersistentAIAssetResolver.ResolveAsync)
            || name == nameof(IPersistentAIAssetResolver.DiscoverLineageAsync));

        var allNames = new[]
            {
                typeof(IAIAssetCatalogProvider),
                typeof(IAIAssetPublisher),
                typeof(IPersistentAIAssetResolver)
            }
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToArray();

        allNames.Should().NotContain(name =>
            name.Contains("List", StringComparison.Ordinal)
            || name.Contains("Search", StringComparison.Ordinal)
            || name.Contains("Exists", StringComparison.Ordinal)
            || name.Contains("Latest", StringComparison.Ordinal)
            || name.Contains("Delete", StringComparison.Ordinal)
            || name.Contains("Remove", StringComparison.Ordinal)
            || name.Contains("Update", StringComparison.Ordinal)
            || name.Contains("Replace", StringComparison.Ordinal)
            || name.Contains("Repair", StringComparison.Ordinal));
    }

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0"));
}
