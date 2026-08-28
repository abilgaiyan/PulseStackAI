using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentTypeTests
{
    [Fact]
    public void DocumentType_ShouldRemainAlignedWithCurrentAssetTypes()
    {
        Enum.GetNames<AIAssetDocumentType>().Should().Equal(
            Enum.GetNames<AssetType>());
    }
}
