using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class LibraryPersistenceCompositionConformanceTests
{
    [Fact]
    public async Task CompleteLibrary_ShouldComposeAcrossPersistenceBoundaryWithoutResolvingMembers()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider.GetRequiredService<LibraryAssetFactory>();
        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var catalog = scope.ServiceProvider.GetRequiredService<IAssetDefinitionCatalog>();

        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var model = Reference(AssetType.Model, "model");
        var externalLibrary = Reference(AssetType.Library, "external-library");
        var members = new[] { workflow, agent, prompt, model };

        foreach (var member in members)
            (await catalog.FindAsync(AssetDefinitionKey.From(member))).Should().BeNull();
        (await catalog.FindAsync(AssetDefinitionKey.From(externalLibrary))).Should().BeNull();

        var created = factory.Create(
            new LibraryAssetOptions
            {
                Name = "Reusable Operations Library",
                Description = "Library persistence composition conformance fixture.",
                Members = members
            },
            [new AssetDependency(externalLibrary, false)]);

        var library = created with
        {
            Version = new AssetVersion("2.4.1"),
            Metadata = new AssetMetadata
            {
                Name = created.Options.Name,
                Description = created.Options.Description,
                Author = "PulseStack Conformance",
                Tags = ["library", "portable", "conformance"],
                Category = "Reusable Definitions"
            },
            Lifecycle = AssetLifecycle.Published
        };

        var document = mapper.ToDocument(library)
            .Should().BeOfType<LibraryAssetDocument>().Subject;

        document.Members.Should().HaveCount(4);
        document.References.Should().HaveCount(4);
        document.Dependencies.Should().ContainSingle();

        var validation = await validator.ValidateAsync(document);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<LibraryAsset>().Subject;

        reconstructed.Id.Should().Be(library.Id);
        reconstructed.Urn.Should().Be(library.Urn);
        reconstructed.Version.Should().Be(library.Version);
        reconstructed.Lifecycle.Should().Be(AssetLifecycle.Published);
        reconstructed.Metadata.Name.Should().Be(library.Metadata.Name);
        reconstructed.Metadata.Description.Should().Be(library.Metadata.Description);
        reconstructed.Metadata.Author.Should().Be("PulseStack Conformance");
        reconstructed.Metadata.Tags.Should().Equal("library", "portable", "conformance");
        reconstructed.Metadata.Category.Should().Be("Reusable Definitions");
        reconstructed.Options.Members.Should().Equal(workflow, agent, prompt, model);
        reconstructed.References.Should().Equal(workflow, agent, prompt, model);
        reconstructed.Dependencies.Should().Equal(library.Dependencies);
        reconstructed.Dependencies.Single().Reference.Should().Be(externalLibrary);

        var roundTrippedDocument = mapper.ToDocument(reconstructed)
            .Should().BeOfType<LibraryAssetDocument>().Subject;

        roundTrippedDocument.Should().Be(document);
    }

    private static AssetReference Reference(AssetType type, string name)
    {
        var id = AssetId.New();
        return new AssetReference(
            type,
            id,
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}:{id}"),
            AssetVersion.Initial);
    }
}
