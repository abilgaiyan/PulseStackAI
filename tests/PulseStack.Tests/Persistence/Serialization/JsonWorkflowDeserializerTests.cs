using Xunit;
using FluentAssertions;
using System.Text.Json;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Serialization;

public class JsonWorkflowDeserializerTests
{
    [Fact]
    public async Task DeserializeAsync_Should_ReadWorkflowDocument()
    {
        // Arrange
        var deserializer = new JsonWorkflowDeserializer();

        var originalDocument = CreateSampleDocument();
        await using var stream = await SerializeToStream(originalDocument);

        // Act
        var deserialized = await deserializer.DeserializeAsync(stream);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Schema.Should().Be(WorkflowDocumentSchema.Name);
        deserialized.SchemaVersion.Should().Be(WorkflowDocumentSchema.Version);
        deserialized.Identity.Should().BeEquivalentTo(originalDocument.Identity);
        deserialized.Definition.Should().BeEquivalentTo(originalDocument.Definition);
        deserialized.Id.Should().Be(originalDocument.Id);
        deserialized.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task DeserializeAsync_Should_ReadRunStepDocument()
    {
        // Arrange
        var deserializer = new JsonWorkflowDeserializer();
        var originalDocument = CreateSampleDocumentWithRunStep();
        await using var stream = await SerializeToStream(originalDocument);

        // Act
        var deserialized = await deserializer.DeserializeAsync(stream);

        // Assert
        deserialized.Steps.Should().HaveCount(1);

        var step = deserialized.Steps[0]
            .Should()
            .BeOfType<RunStepDocument>()
            .Subject;

        var originalStep =
            (RunStepDocument)originalDocument.Steps[0];

        step.Kind.Should().Be(originalStep.Kind);
        step.Name.Should().Be(originalStep.Name);
        step.AgentReference.Should().Be(originalStep.AgentReference);
        step.Id.Should().Be(originalStep.Id);
    }

   [Fact]
    public async Task DeserializeAsync_Should_Throw_WhenInputIsNull()
    {
        // Arrange
        var deserializer = new JsonWorkflowDeserializer();

        // Act
        Func<Task> action = () =>
            deserializer.DeserializeAsync(null!).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<ArgumentNullException>();

        exception.Which.ParamName.Should().Be("input");
    }

    [Fact]
    public async Task DeserializeAsync_Should_Throw_WhenJsonIsInvalid()
    {
        var deserializer = new JsonWorkflowDeserializer();
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("invalid json {"));

        // Act
        Func<Task> action = () =>
                deserializer.DeserializeAsync(stream).AsTask();

        await action.Should()
            .ThrowAsync<JsonException>();
    }

     [Fact]
    public async Task DeserializeAsync_Should_Throw_WhenJsonContainsNull()
    {
        // Arrange
        var deserializer = new JsonWorkflowDeserializer();

        await using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("null"));

        // Act
        Func<Task> act = () =>
            deserializer.DeserializeAsync(stream).AsTask();

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*valid PulseStack workflow document*");
    }

    // ====================== Helpers ======================

    private static async Task<MemoryStream> SerializeToStream(WorkflowDocument document)
    {
        var serializer = new JsonWorkflowSerializer();
        var stream = new MemoryStream();
        await serializer.SerializeAsync(document, stream);
        stream.Position = 0;
        return stream;
    }

    private static WorkflowDocument CreateSampleDocument()
    {
        return new WorkflowDocument
        {
            Schema = WorkflowDocumentSchema.Name,
            SchemaVersion = WorkflowDocumentSchema.Version,
            Identity = WorkflowIdentity.Create("1.0.0"),
            Id = WorkflowStepId.New(),
            Definition = new WorkflowDefinition("Test Workflow", "Deserialization test"),
            Steps = []
        };
    }

    private static WorkflowDocument CreateSampleDocumentWithRunStep()
    {
        var mapper = new WorkflowMapper();

        var workflow = new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition("Run Step Test"));

        var agent = new FakeAgent("agent-alpha", "Test Agent");
        workflow.Add(new RunStep(agent));

        return mapper.ToDocument(workflow);
    }

}