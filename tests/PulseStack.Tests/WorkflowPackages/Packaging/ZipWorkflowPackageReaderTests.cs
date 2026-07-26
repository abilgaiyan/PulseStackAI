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

public class ZipWorkflowPackageReaderTests
{
    private readonly WorkflowMapper _mapper = new();
    private readonly JsonWorkflowSerializer _serializer = new();
    private readonly JsonWorkflowDeserializer _deserializer = new();
    private readonly WorkflowValidator _validator = new();
    private readonly FakeAgentResolver _agentResolver = new();

    private ZipWorkflowPackageBuilder CreatePackageBuilder()
        => new(_validator, _mapper, _serializer);

    private ZipWorkflowPackageReader CreatePackageReader()
        => new(_mapper, _deserializer, _agentResolver);

    // ====================== Constructor Guards ======================

    [Fact]
    public void Constructor_ShouldThrow_WhenMapperIsNull()
    {
        // Act
        Action action = () =>
            new ZipWorkflowPackageReader(null!, _deserializer, _agentResolver);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("mapper");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDeserializerIsNull()
    {
        // Act
        Action action = () =>
            new ZipWorkflowPackageReader(_mapper, null!, _agentResolver);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("deserializer");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAgentResolverIsNull()
    {
        // Act
        Action action = () =>
            new ZipWorkflowPackageReader(_mapper, _deserializer, null!);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("agentResolver");
    }

    // ====================== ReadAsync Guards ======================

    [Fact]
    public async Task ReadAsync_ShouldThrow_WhenInputIsNull()
    {
        // Arrange
        var sut = CreatePackageReader();

        // Act
        Func<Task> action = () =>
            sut.ReadAsync(null!).AsTask();

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("input");
    }

    [Fact]
    public async Task ReadAsync_ShouldThrow_WhenManifestIsMissing()
    {
        // Arrange
        await using var stream = await CreateInvalidPackageAsync(includeManifest: false);
        var sut = CreatePackageReader();

        // Act
        Func<Task> action = () =>
            sut.ReadAsync(stream).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("manifest.json");
    }

    [Fact]
    public async Task ReadAsync_ShouldThrow_WhenWorkflowIsMissing()
    {
        // Arrange
        await using var stream = await CreateInvalidPackageAsync(includeWorkflow: false);
        var sut = CreatePackageReader();

        // Act
        Func<Task> action = () =>
            sut.ReadAsync(stream).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("workflow.json");
    }

    // ====================== Happy Path ======================

    [Fact]
    public async Task ReadAsync_ShouldReturnPackage()
    {
        // Arrange
        var originalPackage = CreatePackage(CreateWorkflowWithRunSteps());
        await using var stream = await CreatePackageBuilder().BuildAsync(originalPackage);

        var sut = CreatePackageReader();

        // Act
        var package = await sut.ReadAsync(stream);

        // Assert
        package.Should().NotBeNull();
        package.Identity.Id.Should().Be(originalPackage.Identity.Id);
        package.Identity.Version.Should().Be(originalPackage.Identity.Version);
        package.Workflow.Should().NotBeNull();
        package.Workflow.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadAsync_ShouldPreserveWorkflowDefinition()
    {
        // Arrange
        var originalWorkflow = CreateWorkflowWithRunSteps();
        var originalPackage = CreatePackage(originalWorkflow);
        await using var stream = await CreatePackageBuilder().BuildAsync(originalPackage);

        var sut = CreatePackageReader();

        // Act
        var package = await sut.ReadAsync(stream);

        // Assert
        package.Workflow.Definition.Name.Should().Be(originalWorkflow.Definition.Name);
        package.Workflow.Definition.Description.Should().Be(originalWorkflow.Definition.Description);
    }

    [Fact]
    public async Task ReadAsync_ShouldResolveAgents()
    {
        // Arrange
        var originalPackage = CreatePackage(CreateWorkflowWithRunSteps());
        await using var stream = await CreatePackageBuilder().BuildAsync(originalPackage);

        var sut = CreatePackageReader();

        // Act
        var package = await sut.ReadAsync(stream);

        // Assert
        var firstStep = package.Workflow.Steps[0].Should().BeOfType<RunStep>().Subject;
        firstStep.Agent.Should().NotBeNull();
        firstStep.Agent.Name.Should().Be("agent-alpha");

        var secondStep = package.Workflow.Steps[1].Should().BeOfType<RunStep>().Subject;
        secondStep.Agent.Name.Should().Be("agent-beta");
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

    /// <summary>
    /// Creates a deliberately invalid package for negative tests.
    /// </summary>
    private async Task<MemoryStream> CreateInvalidPackageAsync(
        bool includeManifest = true,
        bool includeWorkflow = true)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (includeManifest)
            {
                var entry = archive.CreateEntry(WorkflowPackageConstants.ManifestEntry);
                await using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(
                    entryStream,
                    new WorkflowPackageManifest
                    {
                        PackageId = WorkflowPackageId.New(),
                        PackageVersion = "1.0.0",
                        PackageFormatVersion = WorkflowPackageConstants.PackageFormatVersion,
                        MinimumRuntimeVersion = WorkflowPackageConstants.MinimumRuntimeVersion,
                        CreatedAt = DateTimeOffset.UtcNow,
                        EntryWorkflow = WorkflowPackageConstants.WorkflowEntry
                    },
                    WorkflowPackageJsonOptions.Default);
            }

            if (includeWorkflow)
            {
                var entry = archive.CreateEntry(WorkflowPackageConstants.WorkflowEntry);
                await using var entryStream = entry.Open();
                await _serializer.SerializeAsync(
                    _mapper.ToDocument(CreateWorkflowWithRunSteps()),
                    entryStream);
            }
        }

        stream.Position = 0;
        return stream;
    }
}