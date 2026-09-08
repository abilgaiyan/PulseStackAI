using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentTypeTests
{
    [Fact]
    public void DocumentType_ShouldFreezeSchemaV1Vocabulary()
    {
        Enum.GetNames<AIAssetDocumentType>().Should().Equal(
            nameof(AIAssetDocumentType.Project),
            nameof(AIAssetDocumentType.Library),
            nameof(AIAssetDocumentType.Package),
            nameof(AIAssetDocumentType.Workflow),
            nameof(AIAssetDocumentType.Agent),
            nameof(AIAssetDocumentType.Prompt),
            nameof(AIAssetDocumentType.Tool),
            nameof(AIAssetDocumentType.Knowledge),
            nameof(AIAssetDocumentType.Memory),
            nameof(AIAssetDocumentType.Policy),
            nameof(AIAssetDocumentType.Provider),
            nameof(AIAssetDocumentType.Model));
    }
}
