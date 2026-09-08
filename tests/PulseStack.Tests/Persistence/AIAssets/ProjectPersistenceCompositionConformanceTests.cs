using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class ProjectPersistenceCompositionConformanceTests
{
    [Fact]
    public async Task CompleteProject_ShouldComposeAcrossPersistenceBoundaryWithoutRuntimeResolution()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();

        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ProjectAssetFactory>();
        var mapper = provider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = provider.GetRequiredService<IAIAssetDocumentValidator>();

        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var externalModel = Reference(AssetType.Model, "external-model");

        var project = factory.Create(
            new ProjectAssetOptions
            {
                Name = "Factory Intelligence",
                Description = "Project persistence composition conformance fixture.",
                EntryWorkflow = entry,
                OwnedAssets = [agent, entry, prompt]
            },
            [new AssetDependency(externalModel, false)]);

        var document = mapper.ToDocument(project)
            .Should().BeOfType<ProjectAssetDocument>().Subject;

        document.EntryWorkflow.Should().NotBeNull();
        document.OwnedAssets.Should().HaveCount(3);
        document.References.Should().HaveCount(3);
        document.Dependencies.Should().ContainSingle();

        var validation = await validator.ValidateAsync(document);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<ProjectAsset>().Subject;

        reconstructed.Id.Should().Be(project.Id);
        reconstructed.Urn.Should().Be(project.Urn);
        reconstructed.Version.Should().Be(project.Version);
        reconstructed.Lifecycle.Should().Be(project.Lifecycle);
        reconstructed.Metadata.Name.Should().Be(project.Metadata.Name);
        reconstructed.Metadata.Description.Should().Be(project.Metadata.Description);
        reconstructed.Options.EntryWorkflow.Should().Be(entry);
        reconstructed.Options.OwnedAssets.Should().Equal(agent, entry, prompt);
        reconstructed.References.Should().Equal(entry, agent, prompt);
        reconstructed.Dependencies.Should().Equal(project.Dependencies);

        var roundTrippedDocument = mapper.ToDocument(reconstructed)
            .Should().BeOfType<ProjectAssetDocument>().Subject;

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
