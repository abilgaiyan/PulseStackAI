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

public class FileWorkflowPackageStoreTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileWorkflowPackageStore _store;

    private readonly WorkflowMapper _mapper = new();
    private readonly JsonWorkflowSerializer _serializer = new();
    private readonly WorkflowValidator _validator = new();
    private readonly FakeAgentResolver _agentResolver = new();

    public FileWorkflowPackageStoreTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "PulseStack_PackageStore_" + Guid.NewGuid());

        Directory.CreateDirectory(_rootPath);
        _store = new FileWorkflowPackageStore(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private ZipWorkflowPackageBuilder CreateBuilder()
        => new(_validator, _mapper, _serializer);

    private ZipWorkflowPackageReader CreateReader()
        => new(_mapper, new JsonWorkflowDeserializer(), _agentResolver);

    // ====================== Save / Load ======================

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
    }

    [Fact]
    public async Task SaveAsync_ShouldCreatePackageFile()
    {
        // Arrange
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);

        // Act
        await _store.SaveAsync(package.Identity.Id, stream);

        // Assert
        var expectedPath = Path.Combine(_rootPath, $"{package.Identity.Id}.pspkg");
        File.Exists(expectedPath).Should().BeTrue();
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
        reconstructed.Workflow.Definition.Name.Should().Be(original.Workflow.Definition.Name);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNull_WhenPackageDoesNotExist()
    {
        var result = await _store.LoadAsync(WorkflowPackageId.New());
        result.Should().BeNull();
    }

    // ====================== Exists ======================

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenPackageExists()
    {
        // Arrange
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);
        await _store.SaveAsync(package.Identity.Id, stream);

        // Act
        var exists = await _store.ExistsAsync(package.Identity.Id);

        // Assert
        exists.Should().BeTrue();
        File.Exists(Path.Combine(_rootPath, $"{package.Identity.Id}.pspkg")).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenPackageDoesNotExist()
    {
        var exists = await _store.ExistsAsync(WorkflowPackageId.New());
        exists.Should().BeFalse();
    }

    // ====================== Delete ======================

    [Fact]
    public async Task DeleteAsync_ShouldRemovePackage()
    {
        // Arrange
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);
        await _store.SaveAsync(package.Identity.Id, stream);

        var filePath = Path.Combine(_rootPath, $"{package.Identity.Id}.pspkg");
        File.Exists(filePath).Should().BeTrue();

        // Act
        await _store.DeleteAsync(package.Identity.Id);

        // Assert
        (await _store.ExistsAsync(package.Identity.Id)).Should().BeFalse();
        File.Exists(filePath).Should().BeFalse();
        (await _store.LoadAsync(package.Identity.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotThrow_WhenPackageDoesNotExist()
    {
        // Arrange
        var package = WorkflowPackageTestHelpers.CreatePackage();
        await using var stream = await CreateBuilder().BuildAsync(package);
        await _store.SaveAsync(package.Identity.Id, stream);

        var filePath = Path.Combine(_rootPath, $"{package.Identity.Id}.pspkg");
        File.Exists(filePath).Should().BeTrue();

        // Act
        await _store.DeleteAsync(package.Identity.Id);

        // Assert

        (await _store.ExistsAsync(package.Identity.Id))
            .Should().BeFalse();    
    }

    // ====================== Overwrite ======================

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

    // ====================== Invalid Id ======================

    [Fact]
    public async Task SaveAsync_ShouldThrow_WhenPackageIdIsEmpty()
    {
        // Arrange
        await using var stream = new MemoryStream("test"u8.ToArray());

        // Act
        Func<Task> action = () =>
            _store.SaveAsync(WorkflowPackageId.Empty, stream).AsTask();

        // Assert
        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task LoadAsync_ShouldThrow_WhenPackageIdIsEmpty()
    {
        Func<Task> action = () =>
            _store.LoadAsync(WorkflowPackageId.Empty).AsTask();

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("cannot be empty");
    }
}