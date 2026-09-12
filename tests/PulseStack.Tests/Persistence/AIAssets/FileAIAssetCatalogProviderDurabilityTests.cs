using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class FileAIAssetCatalogProviderDurabilityTests
{
    [Fact]
    public async Task Restart_ShouldRetainCommittedAuthorityAndIgnoreOrphanedStagingArtifacts()
    {
        using var directory = new TemporaryDirectory();
        var writer = new FileAIAssetCatalogProvider(directory.Path);
        var record = CreateRecord();

        (await writer.PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);

        var committedPath = AIAssetCatalogPathModel.GetRecordPath(directory.Path, record.DefinitionKey);
        var stagingPath = committedPath + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(stagingPath, "orphaned-staging-content");

        FileAIAssetCatalogProvider.ResetProcessCoordinationForTests(directory.Path);
        var reopened = new FileAIAssetCatalogProvider(directory.Path + Path.DirectorySeparatorChar);

        var exact = await reopened.FindExactAsync(record.DefinitionKey);
        var found = exact.Should().BeOfType<ExactCatalogLookupResult.Found>().Subject;
        found.Record.Should().Be(record);
        File.Exists(stagingPath).Should().BeTrue("staging cleanup is not authority reconstruction");
    }

    [Fact]
    public async Task EquivalentNormalizedRoots_ShouldCoordinateOneNamespaceAuthority()
    {
        using var directory = new TemporaryDirectory();
        var first = new FileAIAssetCatalogProvider(directory.Path);
        var second = new FileAIAssetCatalogProvider(directory.Path + Path.DirectorySeparatorChar);
        var record = CreateRecord();

        var results = await Task.WhenAll(
            Task.Run(async () => await first.PublishAsync(record)),
            Task.Run(async () => await second.PublishAsync(new CatalogRecord(record.DefinitionKey, record.Urn))));

        results.Should().ContainSingle(result => result == CatalogPublicationResult.Created);
        results.Should().ContainSingle(result => result == CatalogPublicationResult.AlreadyPresent);
    }

    [Fact]
    public async Task PublishedRecord_ShouldUseCanonicalCommittedPathAndLeaveNoStagingArtifact()
    {
        using var directory = new TemporaryDirectory();
        var provider = new FileAIAssetCatalogProvider(directory.Path);
        var record = CreateRecord();

        (await provider.PublishAsync(record)).Should().Be(CatalogPublicationResult.Created);

        var expectedPath = AIAssetCatalogPathModel.GetRecordPath(directory.Path, record.DefinitionKey);
        File.Exists(expectedPath).Should().BeTrue();
        Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
        AIAssetCatalogPathModel.ValidateRecordPath(
            directory.Path,
            expectedPath,
            AIAssetCatalogRecordCodec.Deserialize(await File.ReadAllBytesAsync(expectedPath)));
    }

    private static CatalogRecord CreateRecord()
    {
        var id = AssetId.New();
        return new CatalogRecord(
            new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("1.0")),
            new AssetUrn($"urn:pulsestack:prompt:{id.Value:N}"));
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
