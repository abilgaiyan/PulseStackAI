using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class FileAIAssetCatalogProviderConformanceTests
    : AIAssetCatalogProviderConformanceTests
{
    protected override ValueTask<AIAssetCatalogProviderConformanceFixture> CreateFixtureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "PulseStack.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var faultInjector = new TestFileCatalogFaultInjector();

        var fixture = new AIAssetCatalogProviderConformanceFixture(
            createProvider: () => new FileAIAssetCatalogProvider(root, faultInjector),
            capabilityProfile: new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Durable),
            failureScenario: new AIAssetCatalogProviderFailureScenario(
                ObserveProviderFailureAsync,
                ObserveInconsistentStateAsync),
            tokenObservation: new AIAssetCatalogProviderTokenObservation(
                () => faultInjector.LastExactLookupToken,
                () => faultInjector.LastLineageLookupToken,
                () => faultInjector.LastPublicationToken),
            restartAuthorityAsync: () =>
            {
                FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(root);
                return ValueTask.CompletedTask;
            },
            disposeAsync: () =>
            {
                FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(root);
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch
                {
                }

                return ValueTask.CompletedTask;
            });

        return ValueTask.FromResult(fixture);

        async ValueTask<Exception> ObserveProviderFailureAsync(IAIAssetCatalogProvider provider)
        {
            faultInjector.NextFailure = new IOException("Injected file catalog provider failure.");
            try
            {
                await provider.FindExactAsync(CreateKey());
                return new InvalidOperationException("The injected provider failure was not observed.");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        async ValueTask<Exception> ObserveInconsistentStateAsync(IAIAssetCatalogProvider provider)
        {
            var key = CreateKey();
            var path = AIAssetCatalogPathModel.GetRecordPath(root, key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "not-json");

            try
            {
                await provider.FindExactAsync(key);
                return new InvalidOperationException("Malformed committed authority was not observed.");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public void Provider_ShouldDeclareDurableCapability()
    {
        using var directory = new TemporaryDirectory();
        var provider = new FileAIAssetCatalogProvider(directory.Path);

        provider.CapabilityProfile.Durability.Should().Be(AIAssetAuthorityDurability.Durable);
    }

    [Fact]
    public async Task StagingArtifact_ShouldNeverBecomeCatalogAuthority()
    {
        using var directory = new TemporaryDirectory();
        var provider = new FileAIAssetCatalogProvider(directory.Path);
        var key = CreateKey();
        var finalPath = AIAssetCatalogPathModel.GetRecordPath(directory.Path, key);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        await File.WriteAllTextAsync(finalPath + ".orphan.tmp", "not-json");

        (await provider.FindExactAsync(key)).Should().BeOfType<ExactCatalogLookupResult.NotFound>();
    }

    [Fact]
    public async Task CommittedPathContentMismatch_ShouldBeInconsistentState()
    {
        using var directory = new TemporaryDirectory();
        var expectedKey = CreateKey();
        var differentKey = new AssetDefinitionKey(
            expectedKey.Type,
            expectedKey.Id,
            new AssetVersion("2.0"));
        var path = AIAssetCatalogPathModel.GetRecordPath(directory.Path, expectedKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = AIAssetCatalogRecordCodec.Serialize(
            new CatalogRecord(differentKey, new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}")));
        await File.WriteAllBytesAsync(path, bytes);
        var provider = new FileAIAssetCatalogProvider(directory.Path);

        var act = async () => await provider.FindExactAsync(expectedKey);

        var exception = await act.Should().ThrowAsync<AIAssetCatalogException>();
        exception.Which.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
    }

    [Theory]
    [InlineData(FileAIAssetCatalogCheckpoint.BeforeCommittedRecordOpen)]
    [InlineData(FileAIAssetCatalogCheckpoint.BeforeCommittedRecordRead)]
    public async Task CommittedRecordOperationalReadFailure_ShouldBeProviderFailure(
        FileAIAssetCatalogCheckpoint checkpoint)
    {
        using var directory = new TemporaryDirectory();
        var record = CreateRecord();
        var writer = new FileAIAssetCatalogProvider(directory.Path);
        (await writer.PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);

        var injector = new TestFileCatalogFaultInjector
        {
            FailureCheckpoint = checkpoint,
            NextFailure = new IOException("Injected committed-record filesystem failure.")
        };
        var reader = new FileAIAssetCatalogProvider(directory.Path, injector);

        var act = async () => await reader.FindExactAsync(record.DefinitionKey);

        var exception = await act.Should().ThrowAsync<AIAssetCatalogException>();
        exception.Which.Category.Should().Be(AIAssetCatalogFailureCategory.ProviderFailure);
        exception.Which.InnerException.Should().BeOfType<IOException>();
    }

    [Fact]
    public async Task FailedCandidateCommit_ShouldRemainProviderFailureAndLeaveNoAuthority()
    {
        using var directory = new TemporaryDirectory();
        var injector = new TestFileCatalogFaultInjector
        {
            FailureCheckpoint = FileAIAssetCatalogCheckpoint.BeforeCommit,
            NextFailure = new IOException("Injected commit preparation failure.")
        };
        var provider = new FileAIAssetCatalogProvider(directory.Path, injector);
        var record = CreateRecord();

        var act = async () => await provider.PublishAsync(record);

        var exception = await act.Should().ThrowAsync<AIAssetCatalogException>();
        exception.Which.Category.Should().Be(AIAssetCatalogFailureCategory.ProviderFailure);
        injector.FailureCheckpoint = null;
        (await provider.FindExactAsync(record.DefinitionKey)).Should().BeOfType<ExactCatalogLookupResult.NotFound>();
        Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

    private static CatalogRecord CreateRecord() =>
        new(CreateKey(), new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}"));

    private sealed class TestFileCatalogFaultInjector : IFileAIAssetCatalogFaultInjector
    {
        public CancellationToken? LastExactLookupToken { get; private set; }
        public CancellationToken? LastLineageLookupToken { get; private set; }
        public CancellationToken? LastPublicationToken { get; private set; }
        public Exception? NextFailure { get; set; }
        public FileAIAssetCatalogCheckpoint? FailureCheckpoint { get; set; }

        public void ObserveToken(FileAIAssetCatalogOperation operation, CancellationToken token)
        {
            switch (operation)
            {
                case FileAIAssetCatalogOperation.ExactLookup:
                    LastExactLookupToken = token;
                    break;
                case FileAIAssetCatalogOperation.LineageLookup:
                    LastLineageLookupToken = token;
                    break;
                case FileAIAssetCatalogOperation.Publication:
                    LastPublicationToken = token;
                    break;
            }
        }

        public void OnCheckpoint(FileAIAssetCatalogCheckpoint checkpoint, string firstPath, string? secondPath)
        {
            if (NextFailure is null)
            {
                return;
            }

            if (FailureCheckpoint is not null && FailureCheckpoint != checkpoint)
            {
                return;
            }

            var failure = NextFailure;
            NextFailure = null;
            throw failure;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PulseStack.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(Path);
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}