using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

/// <summary>
/// Shared portable behavioral proof for every MS-009.8 catalog provider.
/// This suite deliberately contains no concrete provider implementation.
/// B.4 and B.6 bind provider-specific fixtures to this contract.
/// </summary>
public abstract class AIAssetCatalogProviderConformanceTests
{
    protected abstract ValueTask<AIAssetCatalogProviderConformanceFixture> CreateFixtureAsync();

    [Fact]
    public async Task FreshAuthority_ShouldReportExactAndLineageAbsence()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        var exact = await fixture.Provider.FindExactAsync(record.DefinitionKey);
        var lineage = await fixture.Provider.FindLineageAsync(record.Urn);

        exact.Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        lineage.Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task PublishNewDefinition_ShouldAtomicallyExposeExactAndLineageAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        var publication = await fixture.Provider.PublishAsync(record);
        var exact = await fixture.Provider.FindExactAsync(record.DefinitionKey);
        var lineage = await fixture.Provider.FindLineageAsync(record.Urn);

        publication.Should().Be(CatalogPublicationResult.Created);
        AssertExact(exact, record);
        AssertLineage(lineage, record.DefinitionKey.Type, record.DefinitionKey.Id, record.Urn, record.DefinitionKey.Version);
    }

    [Fact]
    public async Task RepublishEquivalentDefinition_ShouldConvergeToAlreadyPresentWithoutChangingAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        (await fixture.Provider.PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.Provider.PublishAsync(new CatalogRecord(record.DefinitionKey, record.Urn)))
            .Should().Be(CatalogPublicationResult.AlreadyPresent);

        AssertExact(await fixture.Provider.FindExactAsync(record.DefinitionKey), record);
        AssertLineage(
            await fixture.Provider.FindLineageAsync(record.Urn),
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
    }

    [Fact]
    public async Task PublishAnotherVersionForSameLineage_ShouldExtendExactMembershipWithoutVersionOrderingPolicy()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = CreateRecord(version: "1.0");
        var second = new CatalogRecord(
            new AssetDefinitionKey(first.DefinitionKey.Type, first.DefinitionKey.Id, new AssetVersion("2.0")),
            first.Urn);

        (await fixture.Provider.PublishAsync(first)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.Provider.PublishAsync(second)).Should().Be(CatalogPublicationResult.Created);

        AssertExact(await fixture.Provider.FindExactAsync(first.DefinitionKey), first);
        AssertExact(await fixture.Provider.FindExactAsync(second.DefinitionKey), second);
        AssertLineage(
            await fixture.Provider.FindLineageAsync(first.Urn),
            first.DefinitionKey.Type,
            first.DefinitionKey.Id,
            first.Urn,
            first.DefinitionKey.Version,
            second.DefinitionKey.Version);
    }

    [Fact]
    public async Task ReturnedLineage_ShouldRemainAnImmutableSnapshotAfterLaterPublication()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = CreateRecord(version: "1.0");
        var second = new CatalogRecord(
            new AssetDefinitionKey(first.DefinitionKey.Type, first.DefinitionKey.Id, new AssetVersion("2.0")),
            first.Urn);

        (await fixture.Provider.PublishAsync(first)).Should().Be(CatalogPublicationResult.Created);
        var firstSnapshot = (await fixture.Provider.FindLineageAsync(first.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;

        (await fixture.Provider.PublishAsync(second)).Should().Be(CatalogPublicationResult.Created);
        var laterSnapshot = (await fixture.Provider.FindLineageAsync(first.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;

        firstSnapshot.PublishedVersions.Should().BeEquivalentTo(new[] { first.DefinitionKey.Version });
        laterSnapshot.PublishedVersions.Should().BeEquivalentTo(
            new[] { first.DefinitionKey.Version, second.DefinitionKey.Version });
    }

    [Fact]
    public async Task PublishSameExactKeyWithDifferentUrn_ShouldConflictAndPreserveCommittedAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var committed = CreateRecord();
        var conflictingUrn = new AssetUrn("urn:pulsestack:prompt:conflict");
        var candidate = new CatalogRecord(committed.DefinitionKey, conflictingUrn);

        (await fixture.Provider.PublishAsync(committed)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.Provider.PublishAsync(candidate)).Should().Be(CatalogPublicationResult.Conflict);

        AssertExact(await fixture.Provider.FindExactAsync(committed.DefinitionKey), committed);
        (await fixture.Provider.FindLineageAsync(conflictingUrn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task PublishSameTypeAndIdWithDifferentUrn_ShouldConflictAndNotCreateCandidateExactRecord()
    {
        await using var fixture = await CreateFixtureAsync();
        var committed = CreateRecord(version: "1.0");
        var candidate = new CatalogRecord(
            new AssetDefinitionKey(committed.DefinitionKey.Type, committed.DefinitionKey.Id, new AssetVersion("2.0")),
            new AssetUrn("urn:pulsestack:prompt:other-lineage"));

        (await fixture.Provider.PublishAsync(committed)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.Provider.PublishAsync(candidate)).Should().Be(CatalogPublicationResult.Conflict);

        (await fixture.Provider.FindExactAsync(candidate.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        AssertLineage(
            await fixture.Provider.FindLineageAsync(committed.Urn),
            committed.DefinitionKey.Type,
            committed.DefinitionKey.Id,
            committed.Urn,
            committed.DefinitionKey.Version);
        (await fixture.Provider.FindLineageAsync(candidate.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task PublishSameUrnForDifferentLineageIdentity_ShouldConflictAndPreserveOriginalLineage()
    {
        await using var fixture = await CreateFixtureAsync();
        var committed = CreateRecord();
        var candidate = new CatalogRecord(
            new AssetDefinitionKey(AssetType.Agent, AssetId.New(), committed.DefinitionKey.Version),
            committed.Urn);

        (await fixture.Provider.PublishAsync(committed)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.Provider.PublishAsync(candidate)).Should().Be(CatalogPublicationResult.Conflict);

        (await fixture.Provider.FindExactAsync(candidate.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        AssertLineage(
            await fixture.Provider.FindLineageAsync(committed.Urn),
            committed.DefinitionKey.Type,
            committed.DefinitionKey.Id,
            committed.Urn,
            committed.DefinitionKey.Version);
    }

    [Fact]
    public async Task ConcurrentEquivalentPublication_ShouldConvergeToOneCreatedAndRemainingAlreadyPresent()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => fixture.Provider.PublishAsync(record).AsTask()));

        results.Count(result => result == CatalogPublicationResult.Created).Should().Be(1);
        results.Count(result => result == CatalogPublicationResult.AlreadyPresent).Should().Be(7);
        results.Should().NotContain(CatalogPublicationResult.Conflict);
        AssertExact(await fixture.Provider.FindExactAsync(record.DefinitionKey), record);
    }

    [Fact]
    public async Task ConcurrentConflictingPublication_ShouldCommitOneCandidateAndRejectTheOther()
    {
        await using var fixture = await CreateFixtureAsync();
        var key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0"));
        var candidates = new[]
        {
            new CatalogRecord(key, new AssetUrn("urn:pulsestack:prompt:left")),
            new CatalogRecord(key, new AssetUrn("urn:pulsestack:prompt:right"))
        };

        var results = await Task.WhenAll(candidates.Select(candidate => fixture.Provider.PublishAsync(candidate).AsTask()));

        results.Count(result => result == CatalogPublicationResult.Created).Should().Be(1);
        results.Count(result => result == CatalogPublicationResult.Conflict).Should().Be(1);
        results.Should().NotContain(CatalogPublicationResult.AlreadyPresent);

        var winnerIndex = Array.FindIndex(results, result => result == CatalogPublicationResult.Created);
        var loserIndex = 1 - winnerIndex;
        AssertExact(await fixture.Provider.FindExactAsync(key), candidates[winnerIndex]);
        (await fixture.Provider.FindLineageAsync(candidates[loserIndex].Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task PreCanceledLookups_ShouldObserveCancellation()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exact = async () => await fixture.Provider.FindExactAsync(record.DefinitionKey, cancellation.Token);
        var lineage = async () => await fixture.Provider.FindLineageAsync(record.Urn, cancellation.Token);

        await exact.Should().ThrowAsync<OperationCanceledException>();
        await lineage.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PreCanceledPublication_ShouldNotCreateAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var publish = async () => await fixture.Provider.PublishAsync(record, cancellation.Token);

        await publish.Should().ThrowAsync<OperationCanceledException>();
        (await fixture.Provider.FindExactAsync(record.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        (await fixture.Provider.FindLineageAsync(record.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    private static CatalogRecord CreateRecord(string version = "1.0") =>
        new(
            new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion(version)),
            new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}"));

    private static void AssertExact(ExactCatalogLookupResult result, CatalogRecord expected)
    {
        var found = result.Should().BeOfType<ExactCatalogLookupResult.Found>().Subject;
        found.Record.DefinitionKey.Should().Be(expected.DefinitionKey);
        found.Record.Urn.Should().Be(expected.Urn);
    }

    private static void AssertLineage(
        CatalogLineageLookupResult result,
        AssetType expectedType,
        AssetId expectedId,
        AssetUrn expectedUrn,
        params AssetVersion[] expectedVersions)
    {
        var found = result.Should().BeOfType<CatalogLineageLookupResult.Found>().Subject;
        found.Lineage.Type.Should().Be(expectedType);
        found.Lineage.Id.Should().Be(expectedId);
        found.Lineage.Urn.Should().Be(expectedUrn);
        found.Lineage.PublishedVersions.Should().BeEquivalentTo(expectedVersions);
    }
}
