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

public class ZipWorkflowPackageRoundTripTests
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
        
    [Fact]
    public async Task RoundTrip_ShouldPreservePackage()
    {
        // Arrange
        var originalPackage = CreatePackage(CreateWorkflowWithRunSteps());
        var builder = CreatePackageBuilder();
        var reader = CreatePackageReader();

        // Act
        await using var stream = await builder.BuildAsync(originalPackage);
        var reconstructedPackage = await reader.ReadAsync(stream);

        // Assert
        reconstructedPackage.Identity.Id.Should().Be(originalPackage.Identity.Id);
        reconstructedPackage.Identity.Version.Should().Be(originalPackage.Identity.Version);
        reconstructedPackage.Workflow.Definition.Name.Should().Be(originalPackage.Workflow.Definition.Name);
        reconstructedPackage.Workflow.Steps.Should().HaveCount(2);
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