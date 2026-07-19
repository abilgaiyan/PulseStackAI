using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Serialization;

public class JsonWorkflowSerializerTests
{
    [Fact]
    public async Task SerializeAsync_Should_WriteWorkflowDocument()
    {
        // Arrange
        var serializer = new JsonWorkflowSerializer();
        var workflow = CreateSampleWorkflow();
        var document = new WorkflowMapper().ToDocument(workflow);
        await using var stream = new MemoryStream();

        // Act
        await serializer.SerializeAsync(document, stream);

        // Assert
        stream.Length.Should().BeGreaterThan(0);

        stream.Position = 0;

        var json = await new StreamReader(stream).ReadToEndAsync();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain(WorkflowDocumentSchema.Name);
        json.Should().Contain(WorkflowStepKinds.Run);
        json.Should().Contain("agent-alpha");
    }

    [Fact]
    public async Task SerializeAsync_Should_WriteIndentedJson()
    {
        //Arrange
        var serializer = new JsonWorkflowSerializer();
        var document = CreateMinimalDocument();
        await using var stream = new MemoryStream();

        //Act  
        await serializer.SerializeAsync(document, stream);

        
        stream.Position = 0;
        var json = await new StreamReader(stream).ReadToEndAsync();

        //Assert
        // Indented JSON usually has newlines and extra whitespace
        json.Should().Contain(Environment.NewLine);
        json.Should().Contain("  ");
        
    }

    [Fact]
    public async Task SerializeAsync_Should_Throw_WhenDocumentIsNull()
    {
        // Arrange
        var serializer = new JsonWorkflowSerializer();
        await using var stream = new MemoryStream();

        // Act
        Func<Task> action = () =>
            serializer.SerializeAsync(null!, stream).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<ArgumentNullException>();

        exception.Which.ParamName.Should().Be("document");
    }

    [Fact]
    public async Task SerializeAsync_Should_Throw_WhenOutputIsNull()
    {
        // Arrange
        var serializer = new JsonWorkflowSerializer();
        var document = CreateMinimalDocument();

        // Act
        Func<Task> action = () =>
            serializer.SerializeAsync(document, null!).AsTask();

        // Assert
        var exception = await action.Should()
            .ThrowAsync<ArgumentNullException>();

        exception.Which.ParamName.Should().Be("output");
    }

    // ====================== Helpers ======================
    private static Workflow CreateSampleWorkflow()
    {
        var workflow = new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition("Serialization Test"));

        var agent = new FakeAgent("agent-alpha", "Test Agent");
        workflow.Add(new RunStep(agent));

        return workflow;
    }

    private static WorkflowDocument CreateMinimalDocument()
    {
        return new WorkflowDocument
        {
            Schema = WorkflowDocumentSchema.Name,
            SchemaVersion = WorkflowDocumentSchema.Version,
            Identity = WorkflowIdentity.Create("1.0.0"),
            Id = WorkflowStepId.New(),
            Definition = new WorkflowDefinition("Test Workflow"),
            Steps = []
        };
    }
    
}