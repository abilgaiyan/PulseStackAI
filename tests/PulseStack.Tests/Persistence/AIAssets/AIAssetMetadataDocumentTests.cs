using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetMetadataDocumentTests
{
    [Fact]
    public void Constructor_ShouldSnapshotTags()
    {
        var tags = new List<string> { "assistant", "system" };

        var document = new AIAssetMetadataDocument(
            "System Prompt",
            tags: tags);

        tags.Clear();

        document.Tags.Should().Equal("assistant", "system");
    }
}
