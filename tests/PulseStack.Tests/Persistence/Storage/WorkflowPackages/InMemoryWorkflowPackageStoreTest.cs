using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Core.WorkflowPackages.Packaging;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Storage.WorkflowPackages;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Storage.WorkflowPackages;

public class InMemoryWorkflowPackageStoreTests
{
    private readonly InMemoryWorkflowPackageStore _store = new();
    private readonly WorkflowMapper _mapper = new();
    private readonly JsonWorkflowSerializer _serializer = new();
    private readonly WorkflowValidator _validator = new();
    private readonly FakeAgentResolver _agentResolver = new();

    private ZipWorkflowPackageBuilder CreateBuilder()
        => new(_validator, _mapper, _serializer);

    private ZipWorkflowPackageReader CreateReader()
        => new(_mapper, new JsonWorkflowDeserializer(), _agentResolver);

    [Fact]
    public async Task SaveAsync_ShouldStorePackage()
    {
        // Arrange
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);

        // Act
        await _store.SaveAsync(package.Identity.Id, stream);

        // Assert
        var loadedStream = await _store.LoadAsync(package.Identity.Id);
        loadedStream.Should().NotBeNull();
        loadedStream!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnStoredPackage()
    {
        // Arrange
        var original = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(original);
        await _store.SaveAsync(original.Identity.Id, stream);

        // Act
        var loadedStream = await _store.LoadAsync(original.Identity.Id);
        var reconstructed = await CreateReader().ReadAsync(loadedStream!);

        // Assert
        reconstructed.Identity.Id.Should().Be(original.Identity.Id);
        reconstructed.Workflow.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNull_WhenPackageDoesNotExist()
    {
        var result = await _store.LoadAsync(WorkflowPackageId.New());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenPackageExists()
    {
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);
        await _store.SaveAsync(package.Identity.Id, stream);

        (await _store.ExistsAsync(package.Identity.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenPackageDoesNotExist()
    {
        (await _store.ExistsAsync(WorkflowPackageId.New())).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemovePackage()
    {
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);
        await _store.SaveAsync(package.Identity.Id, stream);

        await _store.DeleteAsync(package.Identity.Id);

        (await _store.ExistsAsync(package.Identity.Id)).Should().BeFalse();
        (await _store.LoadAsync(package.Identity.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ShouldOverwriteExistingPackage()
    {
        // Arrange
        var packageId = WorkflowPackageId.New();

        var first = WorkflowPackageTestHelpers.CreatePackage(
            WorkflowPackageTestHelpers.CreateValidWorkflow("First Version")) with
        {
            Identity = new WorkflowPackageIdentity(packageId, "1.0.0")
        };

        var second = WorkflowPackageTestHelpers.CreatePackage(
            WorkflowPackageTestHelpers.CreateValidWorkflow("Second Version")) with
        {
            Identity = new WorkflowPackageIdentity(packageId, "1.0.0")
        };

        // Act
        await using var stream1 = await CreateBuilder().BuildAsync(first);
        await _store.SaveAsync(packageId, stream1);

        await using var stream2 = await CreateBuilder().BuildAsync(second);
        await _store.SaveAsync(packageId, stream2);

        // Assert
        var loadedStream = await _store.LoadAsync(packageId);
        var reconstructed = await CreateReader().ReadAsync(loadedStream!);

        reconstructed.Workflow.Definition.Name.Should().Be("Second Version");
    }

    [Fact]
    public async Task SaveAsync_ShouldThrow_WhenPackageIdIsEmpty()
    {
        await using var stream = new MemoryStream("test"u8.ToArray());

        Func<Task> action = () =>
            _store.SaveAsync(WorkflowPackageId.Empty, stream).AsTask();

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("cannot be empty");
    }
}