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
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromSeconds(5);

    protected abstract ValueTask<AIAssetCatalogProviderConformanceFixture> CreateFixtureAsync();

    [Fact]
    public async Task FreshAuthority_ShouldReportExactAndLineageAbsenceAcrossProviderParticipants()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        (await fixture.CreateProvider().FindExactAsync(record.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        (await fixture.CreateProvider().FindLineageAsync(record.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task PublishNewDefinition_ShouldBeVisibleAcrossIndependentProviderParticipants()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        (await fixture.CreateProvider().PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);
        AssertExact(await fixture.CreateProvider().FindExactAsync(record.DefinitionKey), record);
        AssertLineage(
            await fixture.CreateProvider().FindLineageAsync(record.Urn),
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
    }

    [Fact]
    public async Task ProviderInstanceRecreation_ShouldRemainAttachedToSameNamespaceAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        (await fixture.CreateProvider().PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);

        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(record.DefinitionKey), record);
        AssertLineage(
            await reader.FindLineageAsync(record.Urn),
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
    }

    [Fact]
    public async Task DurableCapability_ShouldExposeAuthorityRecreationAndRetainCommittedRecords()
    {
        await using var fixture = await CreateFixtureAsync();
        if (fixture.CapabilityProfile.Durability != AIAssetAuthorityDurability.Durable)
        {
            return;
        }

        fixture.SupportsAuthorityRecreation.Should().BeTrue(
            "a durable catalog capability must provide an authority-recreation proof hook");

        var record = CreateRecord();
        (await fixture.CreateProvider().PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);
        await fixture.RecreateAuthorityAsync();

        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(record.DefinitionKey), record);
        AssertLineage(
            await reader.FindLineageAsync(record.Urn),
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
    }

    [Fact]
    public async Task RepublishEquivalentDefinition_ShouldConvergeAcrossParticipantsWithoutChangingAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();

        (await fixture.CreateProvider().PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.CreateProvider().PublishAsync(new CatalogRecord(record.DefinitionKey, record.Urn)))
            .Should().Be(CatalogPublicationResult.AlreadyPresent);

        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(record.DefinitionKey), record);
        AssertLineage(
            await reader.FindLineageAsync(record.Urn),
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
    }

    [Fact]
    public async Task PublishAnotherVersionForSameLineage_ShouldExtendMembershipWithoutOrderingPolicy()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = CreateRecord("1.0");
        var second = new CatalogRecord(
            new AssetDefinitionKey(first.DefinitionKey.Type, first.DefinitionKey.Id, new AssetVersion("2.0")),
            first.Urn);

        (await fixture.CreateProvider().PublishAsync(first)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.CreateProvider().PublishAsync(second)).Should().Be(CatalogPublicationResult.Created);

        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(first.DefinitionKey), first);
        AssertExact(await reader.FindExactAsync(second.DefinitionKey), second);
        AssertLineage(
            await reader.FindLineageAsync(first.Urn),
            first.DefinitionKey.Type,
            first.DefinitionKey.Id,
            first.Urn,
            first.DefinitionKey.Version,
            second.DefinitionKey.Version);
    }

    [Fact]
    public async Task ReturnedLineage_ShouldRemainImmutableSnapshotAfterLaterPublication()
    {
        await using var fixture = await CreateFixtureAsync();
        var first = CreateRecord("1.0");
        var second = new CatalogRecord(
            new AssetDefinitionKey(first.DefinitionKey.Type, first.DefinitionKey.Id, new AssetVersion("2.0")),
            first.Urn);

        (await fixture.CreateProvider().PublishAsync(first)).Should().Be(CatalogPublicationResult.Created);
        var firstSnapshot = (await fixture.CreateProvider().FindLineageAsync(first.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;

        (await fixture.CreateProvider().PublishAsync(second)).Should().Be(CatalogPublicationResult.Created);
        var laterSnapshot = (await fixture.CreateProvider().FindLineageAsync(first.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.Found>().Subject.Lineage;

        firstSnapshot.PublishedVersions.Should().BeEquivalentTo(new[] { first.DefinitionKey.Version });
        laterSnapshot.PublishedVersions.Should().BeEquivalentTo(
            new[] { first.DefinitionKey.Version, second.DefinitionKey.Version });
    }

    [Fact]
    public async Task SequentialIdentityConflicts_ShouldPreserveCommittedAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var committed = CreateRecord("1.0");
        var sameKeyDifferentUrn = new CatalogRecord(
            committed.DefinitionKey,
            new AssetUrn("urn:pulsestack:prompt:key-conflict"));
        var sameLineageDifferentUrn = new CatalogRecord(
            new AssetDefinitionKey(committed.DefinitionKey.Type, committed.DefinitionKey.Id, new AssetVersion("2.0")),
            new AssetUrn("urn:pulsestack:prompt:lineage-conflict"));
        var sameUrnDifferentLineage = new CatalogRecord(
            new AssetDefinitionKey(AssetType.Agent, AssetId.New(), committed.DefinitionKey.Version),
            committed.Urn);

        (await fixture.CreateProvider().PublishAsync(committed)).Should().Be(CatalogPublicationResult.Created);
        (await fixture.CreateProvider().PublishAsync(sameKeyDifferentUrn)).Should().Be(CatalogPublicationResult.Conflict);
        (await fixture.CreateProvider().PublishAsync(sameLineageDifferentUrn)).Should().Be(CatalogPublicationResult.Conflict);
        (await fixture.CreateProvider().PublishAsync(sameUrnDifferentLineage)).Should().Be(CatalogPublicationResult.Conflict);

        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(committed.DefinitionKey), committed);
        (await reader.FindExactAsync(sameLineageDifferentUrn.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        (await reader.FindExactAsync(sameUrnDifferentLineage.DefinitionKey))
            .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        (await reader.FindLineageAsync(sameKeyDifferentUrn.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
        (await reader.FindLineageAsync(sameLineageDifferentUrn.Urn))
            .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task ConcurrentEquivalentPublication_ShouldConvergeAcrossIndependentParticipants()
    {
        await using var fixture = await CreateFixtureAsync();
        var template = CreateRecord();
        var participants = Enumerable.Range(0, 8).Select(_ => fixture.CreateProvider()).ToArray();
        var records = Enumerable.Range(0, 8)
            .Select(_ => new CatalogRecord(template.DefinitionKey, template.Urn))
            .ToArray();

        var results = await RunCoordinatedAsync(
            participants.Select((provider, index) =>
                (Func<Task<CatalogPublicationResult>>)(() => provider.PublishAsync(records[index]).AsTask())));

        results.Count(result => result == CatalogPublicationResult.Created).Should().Be(1);
        results.Count(result => result == CatalogPublicationResult.AlreadyPresent).Should().Be(7);
        results.Should().NotContain(CatalogPublicationResult.Conflict);
        AssertExact(await fixture.CreateProvider().FindExactAsync(template.DefinitionKey), template);
    }

    [Fact]
    public async Task ConcurrentSameExactKeyDifferentUrn_ShouldCommitOneAndConflictTheOther()
    {
        await using var fixture = await CreateFixtureAsync();
        var key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0"));
        await AssertConcurrentConflictAsync(
            fixture,
            [
                new CatalogRecord(key, new AssetUrn("urn:pulsestack:prompt:left")),
                new CatalogRecord(key, new AssetUrn("urn:pulsestack:prompt:right"))
            ]);
    }

    [Fact]
    public async Task ConcurrentSameTypeAndIdDifferentVersionsDifferentUrns_ShouldCommitOneAndConflictTheOther()
    {
        await using var fixture = await CreateFixtureAsync();
        var id = AssetId.New();
        await AssertConcurrentConflictAsync(
            fixture,
            [
                new CatalogRecord(
                    new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("1.0")),
                    new AssetUrn("urn:pulsestack:prompt:left")),
                new CatalogRecord(
                    new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("2.0")),
                    new AssetUrn("urn:pulsestack:prompt:right"))
            ]);
    }

    [Fact]
    public async Task ConcurrentSameUrnDifferentLineageIdentity_ShouldCommitOneAndConflictTheOther()
    {
        await using var fixture = await CreateFixtureAsync();
        var urn = new AssetUrn("urn:pulsestack:shared-lineage");
        await AssertConcurrentConflictAsync(
            fixture,
            [
                new CatalogRecord(
                    new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0")),
                    urn),
                new CatalogRecord(
                    new AssetDefinitionKey(AssetType.Agent, AssetId.New(), new AssetVersion("1.0")),
                    urn)
            ]);
    }

    [Fact]
    public async Task ActiveReaders_ShouldObserveCoherentAuthorityWithinBoundedVisibilityDeadline()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();
        var publisher = fixture.CreateProvider();
        var exactReader = fixture.CreateProvider();
        var lineageReader = fixture.CreateProvider();
        using var startPublication = new ManualResetEventSlim(false);
        using var readersActive = new CountdownEvent(2);
        using var deadline = new CancellationTokenSource(VisibilityTimeout);

        var exactObservation = Task.Run(async () =>
        {
            (await exactReader.FindExactAsync(record.DefinitionKey))
                .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
            readersActive.Signal();
            startPublication.Wait();

            while (!deadline.IsCancellationRequested)
            {
                var exact = await exactReader.FindExactAsync(record.DefinitionKey);
                if (exact is ExactCatalogLookupResult.Found)
                {
                    return await exactReader.FindLineageAsync(record.Urn);
                }

                await Task.Yield();
            }

            throw new TimeoutException(
                "Exact reader did not observe the committed definition within the conformance visibility deadline.");
        });

        var lineageObservation = Task.Run(async () =>
        {
            (await lineageReader.FindLineageAsync(record.Urn))
                .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
            readersActive.Signal();
            startPublication.Wait();

            while (!deadline.IsCancellationRequested)
            {
                var lineage = await lineageReader.FindLineageAsync(record.Urn);
                if (LineageContains(lineage, record.DefinitionKey.Version))
                {
                    return await lineageReader.FindExactAsync(record.DefinitionKey);
                }

                await Task.Yield();
            }

            throw new TimeoutException(
                "Lineage reader did not observe the committed definition within the conformance visibility deadline.");
        });

        readersActive.Wait();
        startPublication.Set();
        (await publisher.PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);

        var exactCompleted = await Task.WhenAny(exactObservation, Task.Delay(VisibilityTimeout));
        exactCompleted.Should().BeSameAs(
            exactObservation,
            "the exact participant must observe publication within the bounded conformance deadline");
        var lineageAfterExact = await exactObservation;

        var lineageCompleted = await Task.WhenAny(lineageObservation, Task.Delay(VisibilityTimeout));
        lineageCompleted.Should().BeSameAs(
            lineageObservation,
            "the lineage participant must observe publication within the bounded conformance deadline");
        var exactAfterLineage = await lineageObservation;

        AssertLineage(
            lineageAfterExact,
            record.DefinitionKey.Type,
            record.DefinitionKey.Id,
            record.Urn,
            record.DefinitionKey.Version);
        AssertExact(exactAfterLineage, record);
    }

    [Fact]
    public async Task InvalidArguments_ShouldWinBeforePreCancellation()
    {
        await using var fixture = await CreateFixtureAsync();
        var provider = fixture.CreateProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var invalidKey = new AssetDefinitionKey(AssetType.Provider, AssetId.New(), AssetVersion.Initial);
        var invalidUrn = new AssetUrn(" ");

        var exact = async () => await provider.FindExactAsync(invalidKey, cancellation.Token);
        var lineage = async () => await provider.FindLineageAsync(invalidUrn, cancellation.Token);
        var publish = async () => await provider.PublishAsync(null!, cancellation.Token);

        await exact.Should().ThrowAsync<ArgumentException>();
        await lineage.Should().ThrowAsync<ArgumentException>();
        await publish.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PreCanceledOperations_ShouldObserveCancellationWithoutCreatingAuthority()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = CreateRecord();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exact = async () => await fixture.CreateProvider().FindExactAsync(record.DefinitionKey, cancellation.Token);
        var lineage = async () => await fixture.CreateProvider().FindLineageAsync(record.Urn, cancellation.Token);
        var publish = async () => await fixture.CreateProvider().PublishAsync(record, cancellation.Token);

        await exact.Should().ThrowAsync<OperationCanceledException>();
        await lineage.Should().ThrowAsync<OperationCanceledException>();
        await publish.Should().ThrowAsync<OperationCanceledException>();

        var reader = fixture.CreateProvider();
        (await reader.FindExactAsync(record.DefinitionKey)).Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        (await reader.FindLineageAsync(record.Urn)).Should().BeOfType<CatalogLineageLookupResult.NotFound>();
    }

    [Fact]
    public async Task CallerTokenIdentity_ShouldBeAssertedByPortableSuite()
    {
        await using var fixture = await CreateFixtureAsync();
        var participant = fixture.CreateProvider();
        var record = CreateRecord();
        using var cancellation = new CancellationTokenSource();

        await participant.FindExactAsync(record.DefinitionKey, cancellation.Token);
        await participant.FindLineageAsync(record.Urn, cancellation.Token);
        await participant.PublishAsync(record, cancellation.Token);

        fixture.TokenObservation.ReadExactLookupToken()
            .Should().Be(cancellation.Token);
        fixture.TokenObservation.ReadLineageLookupToken()
            .Should().Be(cancellation.Token);
        fixture.TokenObservation.ReadPublicationToken()
            .Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ProviderFailureAndInconsistentState_ShouldBeAssertedByPortableSuite()
    {
        await using var fixture = await CreateFixtureAsync();
        var participant = fixture.CreateProvider();

        var providerFailure = await fixture.FailureScenario.ObserveProviderFailureAsync(participant);
        var inconsistentState = await fixture.FailureScenario.ObserveInconsistentStateAsync(participant);

        var providerException = providerFailure.Should().BeOfType<AIAssetCatalogException>().Subject;
        providerException.Category.Should().Be(AIAssetCatalogFailureCategory.ProviderFailure);

        var inconsistentException = inconsistentState.Should().BeOfType<AIAssetCatalogException>().Subject;
        inconsistentException.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
    }

    private static async Task AssertConcurrentConflictAsync(
        AIAssetCatalogProviderConformanceFixture fixture,
        IReadOnlyList<CatalogRecord> candidates)
    {
        candidates.Should().HaveCount(2);
        var participants = new[] { fixture.CreateProvider(), fixture.CreateProvider() };
        var results = await RunCoordinatedAsync(
            participants.Select((provider, index) =>
                (Func<Task<CatalogPublicationResult>>)(() => provider.PublishAsync(candidates[index]).AsTask())));

        results.Count(result => result == CatalogPublicationResult.Created).Should().Be(1);
        results.Count(result => result == CatalogPublicationResult.Conflict).Should().Be(1);
        results.Should().NotContain(CatalogPublicationResult.AlreadyPresent);

        var winnerIndex = Array.FindIndex(results, result => result == CatalogPublicationResult.Created);
        var loserIndex = 1 - winnerIndex;
        var reader = fixture.CreateProvider();
        AssertExact(await reader.FindExactAsync(candidates[winnerIndex].DefinitionKey), candidates[winnerIndex]);

        if (candidates[winnerIndex].DefinitionKey != candidates[loserIndex].DefinitionKey)
        {
            (await reader.FindExactAsync(candidates[loserIndex].DefinitionKey))
                .Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        }

        if (!Equals(candidates[winnerIndex].Urn, candidates[loserIndex].Urn))
        {
            (await reader.FindLineageAsync(candidates[loserIndex].Urn))
                .Should().BeOfType<CatalogLineageLookupResult.NotFound>();
        }
    }

    private static async Task<CatalogPublicationResult[]> RunCoordinatedAsync(
        IEnumerable<Func<Task<CatalogPublicationResult>>> operations)
    {
        var operationArray = operations.ToArray();
        using var release = new ManualResetEventSlim(false);
        using var ready = new CountdownEvent(operationArray.Length);

        var workers = operationArray.Select(operation => Task.Run(async () =>
        {
            ready.Signal();
            release.Wait();
            return await operation();
        })).ToArray();

        ready.Wait();
        release.Set();
        return await Task.WhenAll(workers);
    }

    private static CatalogRecord CreateRecord(string version = "1.0") =>
        new(
            new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion(version)),
            new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}"));

    private static bool LineageContains(CatalogLineageLookupResult result, AssetVersion version) =>
        result is CatalogLineageLookupResult.Found found
        && found.Lineage.PublishedVersions.Contains(version);

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
