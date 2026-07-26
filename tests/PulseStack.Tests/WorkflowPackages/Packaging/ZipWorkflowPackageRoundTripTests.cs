using Xunit;
using FluentAssertions;
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
    public async Task BuildAndRead_ShouldPreserveWorkflowPackage()
    {
        // Arrange
        var originalPackage = CreatePackage(CreateWorkflowWithRunSteps());
        var builder = CreatePackageBuilder();
        var reader = CreatePackageReader();

        // Act
        await using var stream = await builder.BuildAsync(originalPackage);
        var reconstructedPackage = await reader.ReadAsync(stream);

        // Assert
        reconstructedPackage.Workflow.Definition.Description
            .Should().Be(originalPackage.Workflow.Definition.Description);

        reconstructedPackage.Workflow.Identity
            .Should().Be(originalPackage.Workflow.Identity);

        reconstructedPackage.Workflow.Steps
            .Should().HaveCount(originalPackage.Workflow.Steps.Count);

        var step1 = reconstructedPackage.Workflow.Steps[0]
            .Should()
            .BeOfType<RunStep>()
            .Subject;

        step1.Agent.Name.Should().Be("agent-alpha");

        var step2 = reconstructedPackage.Workflow.Steps[1]
            .Should()
            .BeOfType<RunStep>()
            .Subject;

        step2.Agent.Name.Should().Be("agent-beta");            
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

}