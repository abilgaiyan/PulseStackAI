using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class AIAssetDocumentMapperContractTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void ToDocument_ShouldRejectNullAsset()
    {
        var action = () => mapper.ToDocument(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromDocument_ShouldRejectNullDocument()
    {
        var action = () => mapper.FromDocument(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromDocument_ShouldRejectMalformedIdentityId()
    {
        var document = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = "not-a-guid",
                Urn = "urn:pulsestack:prompt:invalid",
                Version = "1.0.0"
            },
            new AIAssetMetadataDocument("Prompt"),
            AIAssetLifecycleDocument.Draft,
            "Be precise.");

        var action = () => mapper.FromDocument(document);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Asset ID 'not-a-guid'*non-empty GUID*");
    }

    [Fact]
    public void FromDocument_ShouldRejectUnsupportedLifecycleValue()
    {
        var id = Guid.NewGuid();
        var document = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = id.ToString(),
                Urn = $"urn:pulsestack:prompt:{id}",
                Version = "1.0.0"
            },
            new AIAssetMetadataDocument("Prompt"),
            (AIAssetLifecycleDocument)999,
            "Be precise.");

        var action = () => mapper.FromDocument(document);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*document lifecycle*999*not supported by schema v1 mapping*");
    }
}
