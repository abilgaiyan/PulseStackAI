using Xunit;
using FluentAssertions;
using System.Text.Json;
using System.IO.Compression;
using PulseStack.Core.WorkflowPackages.Packaging;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.WorkflowPackages.Packaging;

public class ZipWorkflowPackageBuilderTests
{
    private readonly WorkflowMapper _mapper = new();
    private readonly JsonWorkflowSerializer _serializer = new();
    private readonly WorkflowValidator _validator = new();

    private ZipWorkflowPackageBuilder CreatePackageBuilder()
        => new(_validator, _mapper, _serializer);

    // ====================== Constructor Guards ======================

    [Fact]
    public void Constructor_ShouldThrow_WhenValidatorIsNull()
    {
        // Arrange
        // Act
        Action action = () =>
            new ZipWorkflowPackageBuilder(null!, _mapper, _serializer);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("validator");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMapperIsNull()
    {
        // Arrange
        // Act
        Action action = () =>
            new ZipWorkflowPackageBuilder(_validator, null!, _serializer);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("mapper");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        // Arrange
        // Act
        Action action = () =>
            new ZipWorkflowPackageBuilder(_validator, _mapper, null!);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("serializer");
    }

    // ====================== BuildAsync Guards ======================

    [Fact]
    public async Task BuildAsync_ShouldThrow_WhenPackageIsNull()
    {
        // Arrange
        var sut = CreatePackageBuilder();

        // Act
        Func<Task> action = () =>
            sut.BuildAsync(null!).AsTask();

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("package");
    }

    [Fact]
    public async Task BuildAsync_ShouldThrow_WhenWorkflowValidationFails()
    {
        // Arrange
        var invalidWorkflow = CreateEmptyWorkflow(); // no steps → validation fails
        var package = CreatePackage(invalidWorkflow);
        var sut = CreatePackageBuilder();

        // Act
        Func<Task> action = () =>
            sut.BuildAsync(package).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("Workflow validation failed");
        exception.Which.Message.Should().Contain("WF300"); // Empty workflow
    }

    [Fact]
    public async Task BuildAsync_ShouldResetStreamPosition()
    {
        // Arrange
        var workflow = CreateWorkflowWithRunSteps();
        var package = CreatePackage(workflow);
        var sut = CreatePackageBuilder();

        // Act
        var stream = await sut.BuildAsync(package);

        // Assert
        stream.Position.Should().Be(0);
    }

    [Fact]
    public async Task BuildAsync_ShouldReturnZipArchive()
    {
        // Arrange
        var workflow = CreateWorkflowWithRunSteps();
        var package = CreatePackage(workflow);
        var sut = CreatePackageBuilder();

        // Act
        var stream = await sut.BuildAsync(package);

        // Assert
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BuildAsync_ShouldContainManifestAndWorkflowEntries()
    {
         // Arrange
        var workflow = CreateWorkflowWithRunSteps();
        var package = CreatePackage(workflow);
        var sut = CreatePackageBuilder();

        // Act
        var stream = await sut.BuildAsync(package);
        using var archive = OpenArchive(stream);

        // Assert 
        archive.Entries.Should()
            .Contain(e => e.FullName == "manifest.json");

        archive.Entries.Should()
            .Contain(e => e.FullName == "workflow.json");
    }

    [Fact]
    public async  Task BuildAsync_ShouldWriteManifest()
    {
        // Arrange
        var workflow = CreateWorkflowWithRunSteps();
        var package = CreatePackage(workflow);
        var sut = CreatePackageBuilder();

        // Act
        var stream = await sut.BuildAsync(package);
        using var archive = OpenArchive(stream);

        var entry = archive.GetEntry("manifest.json");

        entry.Should().NotBeNull();

        await using var entryStream = entry!.Open();
       
        var manifest = await JsonSerializer.DeserializeAsync<WorkflowPackageManifest>(
            entryStream,
            WorkflowPackageJsonOptions.Default);

        
        manifest.Should().NotBeNull();
        manifest!.PackageId.Should().Be(package.Identity.Id);

        manifest.PackageVersion.Should().Be(package.Identity.Version);

        manifest.PackageFormatVersion.Should().Be(WorkflowPackageConstants.PackageFormatVersion);

        manifest.MinimumRuntimeVersion.Should().Be(WorkflowPackageConstants.MinimumRuntimeVersion);

        manifest.EntryWorkflow.Should().Be(WorkflowPackageConstants.WorkflowEntry);

        manifest.CreatedAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async  Task BuildAsync_ShouldWriteWorkflow()
    {
        // Arrange
        var workflow = CreateWorkflowWithRunSteps();
        var package = CreatePackage(workflow);
        var sut = CreatePackageBuilder();

        // Act
        var stream = await sut.BuildAsync(package);
        using var archive = OpenArchive(stream);

        var entry = archive.GetEntry("workflow.json");

        entry.Should().NotBeNull();

        await using var entryStream = entry!.Open();
        var deserializer = new JsonWorkflowDeserializer();

        var document = await deserializer.DeserializeAsync(entryStream);

        document.Should().NotBeNull();

        document.Definition.Name.Should().Be(workflow.Definition.Name);

        document.Steps.Should().HaveCount(2);

    }

    // ====================== Helpers ======================

    private static WorkflowPackage CreatePackage(Workflow workflow)
    {
        return new WorkflowPackage
        {
            Identity = new WorkflowPackageIdentity(WorkflowPackageId.New(), "1.0.0"),
            Metadata = new WorkflowPackageMetadata(),
            Workflow = workflow
        };
    }
    private static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For packaging tests")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateWorkflowWithRunSteps()
    {
        var workflow = CreateEmptyWorkflow("Package Test Workflow");

        var agent1 = new FakeAgent("agent-alpha", "Agent Alpha");
        var agent2 = new FakeAgent("agent-beta", "Agent Beta");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }

    private static ZipArchive OpenArchive(Stream stream)
    {
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
    }
}