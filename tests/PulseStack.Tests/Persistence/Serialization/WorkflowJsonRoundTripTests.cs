using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Serialization;

public class WorkflowJsonRoundTripTests
{
    [Fact]
    public async Task RoundTrip_Should_PreserveEmptyWorkflowDocument()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var originalWorkflow = CreateEmptyWorkflow();
        var originalDocument = mapper.ToDocument(originalWorkflow);

        // Act
        var roundTrippedDocument = await SerializeAndDeserializeAsync(originalDocument);

        // Assert
        roundTrippedDocument.Should().BeEquivalentTo(originalDocument);
    
    }

    [Fact]
    public async Task RoundTrip_Should_PreserveRunStep()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var originalWorkflow = CreateSampleWorkflow();
        var originalDocument = mapper.ToDocument(originalWorkflow);

        // Act
        var roundTrippedDocument = await SerializeAndDeserializeAsync(originalDocument);

        // Assert
        roundTrippedDocument.Should().BeEquivalentTo(originalDocument);
        roundTrippedDocument.Steps.Should().HaveCount(1);

        // Additional specific checks for RunStep
        var originalStep =
            (RunStepDocument)originalDocument.Steps[0];

        var step = roundTrippedDocument.Steps[0].Should().BeOfType<RunStepDocument>().Subject;
        step.Kind.Should().Be(WorkflowStepKinds.Run);
        step.AgentReference.Should().Be("agent-alpha");
        step.Name.Should().NotBeNullOrWhiteSpace();
        step.Id.Should().Be(originalStep.Id);
        
    }

    [Fact]
    public async Task RoundTrip_Should_PreserveWorkflowIdentity()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var originalWorkflow = CreateEmptyWorkflow();
        var originalDocument = mapper.ToDocument(originalWorkflow);

        // Act
        var roundTripped = await SerializeAndDeserializeAsync(originalDocument);

        // Assert
        roundTripped.Identity.Should().BeEquivalentTo(originalDocument.Identity);
        roundTripped.Identity.Id.Should().Be(originalDocument.Identity.Id);
        roundTripped.Identity.Version.Should().Be(originalDocument.Identity.Version);

    }

    [Fact]
    public async Task RoundTrip_Should_PreserveWorkflowDefinition()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var originalWorkflow = CreateEmptyWorkflow("Custom Name", "Custom Description");
        var originalDocument = mapper.ToDocument(originalWorkflow);

        // Act
        var roundTripped = await SerializeAndDeserializeAsync(originalDocument);

        // Assert
        roundTripped.Definition.Should().BeEquivalentTo(originalDocument.Definition);
        roundTripped.Definition.Name.Should().Be("Custom Name");
        roundTripped.Definition.Description.Should().Be("Custom Description");

    }

    // ====================== Helpers ======================

    private static async Task<WorkflowDocument> SerializeAndDeserializeAsync(WorkflowDocument document)
    {
        var serializer = new JsonWorkflowSerializer();
        var deserializer = new JsonWorkflowDeserializer();

        await using var stream = new MemoryStream();

        // Serialize
        await serializer.SerializeAsync(document, stream);

        // Prepare for deserialization
        stream.Position = 0;

        // Deserialize
        return await deserializer.DeserializeAsync(stream);
    }

    private static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For roundtrip testing")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateSampleWorkflow()
    {
        var workflow = CreateEmptyWorkflow("Serialization Test");
        
        var agent = new FakeAgent("agent-alpha", "Test Agent");
        workflow.Add(new RunStep(agent));

        return workflow;
    }

}