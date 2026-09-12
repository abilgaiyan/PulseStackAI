using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogPathCanonicalityTests
{
    [Fact]
    public void ParseRecordPath_ShouldRejectFilesystemEquivalentButNonCanonicalSpelling()
    {
        var key = new AssetDefinitionKey(
            AssetType.Prompt,
            new AssetId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),
            new AssetVersion("1.0"));
        var canonical = AIAssetCatalogPathModel.GetRecordPath("catalog-root", key);
        var nonCanonical = canonical.Replace(
            $"records{Path.DirectorySeparatorChar}",
            $"records{Path.DirectorySeparatorChar}.{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

        var act = () => AIAssetCatalogPathModel.ParseRecordPath("catalog-root", nonCanonical);

        act.Should().Throw<InvalidDataException>();
    }
}
