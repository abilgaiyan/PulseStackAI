using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence;

public class WorkflowPersistenceIntegrationTests
{
    [Fact]
    public async Task Persistence_Should_RoundTripEmptyWorkflow()
    {
        // Arrange
        var originalWorkflow = CreateEmptyWorkflow();

        // Act - Full pipeline: Domain to Document to JSON to Document to Domain
        var roundTrippedWorkflow = await RoundTripWorkflowAsync(originalWorkflow);

        // Assert
        roundTrippedWorkflow.Should().NotBeNull();
        roundTrippedWorkflow.Should().NotBeSameAs(originalWorkflow);

        roundTrippedWorkflow.Identity.Should().BeEquivalentTo(originalWorkflow.Identity);
        roundTrippedWorkflow.Definition.Should().BeEquivalentTo(originalWorkflow.Definition);
        roundTrippedWorkflow.Steps.Should().BeEmpty();
        roundTrippedWorkflow.Id.Should().Be(originalWorkflow.Id);
        
        roundTrippedWorkflow.Identity.Id
            .Should().Be(originalWorkflow.Identity.Id);

        roundTrippedWorkflow.Identity.Version
            .Should().Be(originalWorkflow.Identity.Version);
        
    }

    [Fact]
    public async Task Persistence_Should_RoundTripRunStepWorkflow()
    {
        // Arrange
        var originalWorkflow = CreateWorkflowWithRunSteps();

        // Act
        var roundTrippedWorkflow = await RoundTripWorkflowAsync(originalWorkflow);

        // Assert
        roundTrippedWorkflow.Steps.Should().HaveCount(2);

        var firstStep = roundTrippedWorkflow.Steps[0].Should().BeOfType<RunStep>().Subject;
        firstStep.Agent.Name.Should().Be("agent-alpha");

        var secondStep = roundTrippedWorkflow.Steps[1].Should().BeOfType<RunStep>().Subject;
        secondStep.Agent.Name.Should().Be("agent-beta");
        
    }

     [Fact]
    public async Task Persistence_Should_PreserveWorkflowIdentity()
    {
        // Arrange
        var originalWorkflow = CreateEmptyWorkflow();
        var originalId = originalWorkflow.Identity.Id;

        // Act
        var roundTrippedWorkflow = await RoundTripWorkflowAsync(originalWorkflow);

        // Assert
        roundTrippedWorkflow.Identity.Id.Should().Be(originalId);
        roundTrippedWorkflow.Identity.Version.Should().Be(originalWorkflow.Identity.Version);
    
    }

     [Fact]
    public async Task Persistence_Should_PreserveWorkflowStepIds()
    {
        // Arrange
        var originalWorkflow = CreateWorkflowWithRunSteps();
        var originalStepIds = originalWorkflow.Steps.Select(s => s.Id).ToList();

        // Act
        var roundTrippedWorkflow = await RoundTripWorkflowAsync(originalWorkflow);

        // Assert
        roundTrippedWorkflow.Steps.Should().HaveSameCount(originalStepIds);

        for (int i = 0; i < originalStepIds.Count; i++)
        {
            roundTrippedWorkflow.Steps[i].Id.Should().Be(originalStepIds[i]);
        }
    }

    // ====================== Helpers ======================

    private async Task<Workflow> RoundTripWorkflowAsync(Workflow originalWorkflow)
    {
        var mapper = new WorkflowMapper();
        var serializer = new JsonWorkflowSerializer();
        var deserializer = new JsonWorkflowDeserializer();
        var agentResolver = new FakeAgentResolver();

        // 1. Domain to Document
        var document = mapper.ToDocument(originalWorkflow);

        // 2. Document to JSON
        await using var stream = new MemoryStream();
        await serializer.SerializeAsync(document, stream);

        // 3. JSON to Document
        stream.Position = 0;
        var deserializedDocument = await deserializer.DeserializeAsync(stream);

        // 4. Document to Domain
        return mapper.FromDocument(deserializedDocument, agentResolver);
    }

    private static Workflow CreateEmptyWorkflow()
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition("Empty Integration Test", "Testing full persistence pipeline"));
    }

    private Workflow CreateWorkflowWithRunSteps()
    {
        var workflow = CreateEmptyWorkflow();

        var agent1 = new FakeAgent("agent-alpha", "Agent Alpha");
        var agent2 = new FakeAgent("agent-beta", "Agent Beta");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }
}