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

public sealed class PackagePersistenceCompositionConformanceTests
{
    [Fact]
    public async Task CompletePackage_ShouldComposeAcrossPersistenceBoundaryWithoutResolvingMembers()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider.GetRequiredService<PackageAssetFactory>();
        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();

        var project = Reference(AssetType.Project, "project");
        var library = Reference(AssetType.Library, "library");
        var nestedPackage = Reference(AssetType.Package, "nested-package");
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var providerReference = Reference(AssetType.Provider, "provider");
        var externalModel = Reference(AssetType.Model, "external-model");
        var members = new[]
        {
            project,
            library,
            nestedPackage,
            workflow,
            agent,
            providerReference
        };

        var created = factory.Create(
            new PackageAssetOptions
            {
                Name = "Portable Intelligence Distribution",
                Description = "Package persistence composition conformance fixture.",
                Members = members
            },
            [new AssetDependency(externalModel, false)]);

        var package = created with
        {
            Version = new AssetVersion("3.1.0"),
            Metadata = new AssetMetadata
            {
                Name = created.Options.Name,
                Description = created.Options.Description,
                Author = "PulseStack Conformance",
                Tags = ["package", "distribution", "portable"],
                Category = "Distribution Boundary"
            },
            Lifecycle = AssetLifecycle.Published
        };

        var document = mapper.ToDocument(package)
            .Should().BeOfType<PackageAssetDocument>().Subject;

        document.Members.Should().Equal(document.References);
        document.Members.Should().HaveCount(6);
        document.Members.Select(member => member.AssetType).Should().Equal(
            AIAssetDocumentType.Project,
            AIAssetDocumentType.Library,
            AIAssetDocumentType.Package,
            AIAssetDocumentType.Workflow,
            AIAssetDocumentType.Agent,
            AIAssetDocumentType.Provider);
        document.Dependencies.Should().ContainSingle();

        var validation = await validator.ValidateAsync(document);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<PackageAsset>().Subject;

        reconstructed.Id.Should().Be(package.Id);
        reconstructed.Urn.Should().Be(package.Urn);
        reconstructed.Version.Should().Be(package.Version);
        reconstructed.Lifecycle.Should().Be(AssetLifecycle.Published);
        reconstructed.Metadata.Name.Should().Be(package.Metadata.Name);
        reconstructed.Metadata.Description.Should().Be(package.Metadata.Description);
        reconstructed.Metadata.Author.Should().Be("PulseStack Conformance");
        reconstructed.Metadata.Tags.Should().Equal("package", "distribution", "portable");
        reconstructed.Metadata.Category.Should().Be("Distribution Boundary");
        reconstructed.Options.Members.Should().Equal(members);
        reconstructed.References.Should().Equal(members);
        reconstructed.Dependencies.Should().Equal(package.Dependencies);
        reconstructed.Dependencies.Single().Reference.Should().Be(externalModel);

        var roundTrippedDocument = mapper.ToDocument(reconstructed)
            .Should().BeOfType<PackageAssetDocument>().Subject;

        roundTrippedDocument.Should().Be(document);
    }

    [Fact]
    public void AddPulseStack_ShouldRegisterSinglePackageFactoryWithoutPackageSpecificPersistenceServices()
    {
        var services = new ServiceCollection();

        services.AddPulseStack();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(PackageAssetFactory)
            && descriptor.ImplementationType == typeof(PackageAssetFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAIAssetDocumentMapper));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IAIAssetDocumentValidator));
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
