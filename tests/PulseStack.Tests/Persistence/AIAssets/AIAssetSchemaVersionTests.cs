using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetSchemaVersionTests
{
    [Fact]
    public void V1_ShouldRepresentPersistenceSchemaVersion_IndependentlyFromAssetVersion()
    {
        var schemaVersion = AIAssetSchemaVersion.V1;
        var assetVersion = AssetVersion.Initial;

        schemaVersion.Value.Should().Be("1.0");
        schemaVersion.ToString().Should().Be("1.0");
        assetVersion.Value.Should().Be("1.0.0");
        schemaVersion.GetType().Should().NotBe(assetVersion.GetType());
    }
}
