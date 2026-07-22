using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Storage;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Storage;
public sealed class FileWorkflowStoreTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileWorkflowStore _store;
    private readonly WorkflowMapper _mapper = new();
    private readonly FakeAgentResolver _agentResolver = new();
    private readonly JsonWorkflowSerializer _serializer = new();
    private readonly JsonWorkflowDeserializer _deserializer = new();

    public FileWorkflowStoreTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString());

        _store = new FileWorkflowStore(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_Should_Save_Stream()
    {
        //Arange
        var workflow = CreateWorkflowWithRunSteps();
        var document = _mapper.ToDocument(workflow);

        await using var stream = await SerializeToStream(document);

        // Act
        await _store.SaveAsync(workflow.Identity.Id, stream);

        // Assert
        var loadedStream = await _store.LoadAsync(workflow.Identity.Id);
        loadedStream.Should().NotBeNull();

        // Deserialize back to document
        var loadedDocument = await DeserializeFromStream(loadedStream!);
        loadedDocument.Should().BeEquivalentTo(document);
    }

    [Fact]
    public async Task LoadAsync_Should_Return_Previously_Saved_Stream()
    {
        // Arrange

        var originalWorkflow = CreateWorkflowWithRunSteps();
        await StoreWorkflowAsync(originalWorkflow);

        // Act
        var loadedStream = await _store.LoadAsync(originalWorkflow.Identity.Id);
        loadedStream.Should().NotBeNull();

        // Deserialize back to document
        var loadedDocument = await DeserializeFromStream(loadedStream!);
        
        // Reconstruct
        var reconstructedWorkflow = _mapper.FromDocument(loadedDocument, _agentResolver);

        // Assert
        reconstructedWorkflow.Identity.Should()
            .Be(originalWorkflow.Identity);

        reconstructedWorkflow.Definition.Should()
            .BeEquivalentTo(originalWorkflow.Definition);

        reconstructedWorkflow.Steps.Should()
            .HaveCount(originalWorkflow.Steps.Count);

    }

    [Fact]
    public async Task LoadAsync_Should_Return_Null_When_Workflow_Does_Not_Exist()
    {
        // Arrange
        var nonExistentId = WorkflowId.New();

        var result = await _store.LoadAsync(nonExistentId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Saving_Two_Workflows_Should_Not_Conflict()
    {
        // Arrange
        var workflow1 = CreateEmptyWorkflow("Workflow One");
        var workflow2 = CreateWorkflowWithRunSteps(); // has different ID

        // Act
        await StoreWorkflowAsync(workflow1);
        await StoreWorkflowAsync(workflow2);

        // Assert
        (await _store.ExistsAsync(workflow1.Identity.Id)).Should().BeTrue();
        (await _store.ExistsAsync(workflow2.Identity.Id)).Should().BeTrue();

        var loaded1 = await LoadAndReconstructAsync(workflow1.Identity.Id);
        var loaded2 = await LoadAndReconstructAsync(workflow2.Identity.Id);

        loaded1.Identity.Id.Should().NotBe(loaded2.Identity.Id);

        loaded1.Definition.Name.Should().Be("Workflow One");
        loaded2.Definition.Name.Should().Be("Workflow with Steps");

        loaded1.Steps.Should().BeEmpty();
        loaded2.Steps.Should().HaveCount(workflow2.Steps.Count);
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_True_When_Workflow_Exists()
    {
       // Arrange
        var workflow = CreateEmptyWorkflow("Workflow One");
        await StoreWorkflowAsync(workflow);

        // Act
        var exists = await _store.ExistsAsync(workflow.Identity.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Workflow()
    {
        // Arrange
        var workflow = CreateEmptyWorkflow("Workflow One");
        var workflowId = workflow.Identity.Id;

        await StoreWorkflowAsync(workflow);

        // Assert preconditions
        (await _store.ExistsAsync(workflowId)).Should().BeTrue();

        // Act - Delete
        await _store.DeleteAsync(workflowId);

        //  Assert postconditions
        (await _store.ExistsAsync(workflowId)).Should().BeFalse();
        (await _store.LoadAsync(workflowId)).Should().BeNull();

    }

    [Fact]
    public async Task ExistsAsync_Should_Return_False_When_Workflow_Does_Not_Exist()
    {
        var exists = await _store.ExistsAsync(WorkflowId.New());

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_Should_Overwrite_Existing_Workflow()
    {
        // Arrange
        var identity = WorkflowIdentity.Create("1.0.0");

        var version1 = "First Version - v1.0";
        var version2 = "Second Version - v2.0 - Overwritten";

        // Save first version
        var wf1 = CreateEmptyWorkflow(identity, version1);
        await StoreWorkflowAsync(wf1);

        // Act - Overwrite with second version
        var wf2 = CreateEmptyWorkflow(identity, version2);
        await StoreWorkflowAsync(wf2);

        // Assert
        var loaded = await LoadAndReconstructAsync(identity.Id);

        loaded.Definition.Name.Should().Be(version2);
        loaded.Definition.Name.Should().NotBe(version1); 
        loaded.Identity.Should().Be(identity);
    }

    // ====================== Helpers ======================

    private async Task StoreWorkflowAsync(Workflow workflow)
    {
        var document = _mapper.ToDocument(workflow);
        await using var stream = await SerializeToStream(document);
        await _store.SaveAsync(workflow.Identity.Id, stream);
    }

    private async Task<Workflow> LoadAndReconstructAsync(WorkflowId id)
    {
        var stream = await _store.LoadAsync(id);
        var document = await DeserializeFromStream(stream!);
        return _mapper.FromDocument(document, _agentResolver);
    }

    private async Task<MemoryStream> SerializeToStream(WorkflowDocument document)
    {
        var stream = new MemoryStream();
        await _serializer.SerializeAsync(document, stream);
        stream.Position = 0;
        return stream;
    }

    private async Task<WorkflowDocument> DeserializeFromStream(Stream stream)
    {
        stream.Position = 0;
        return await _deserializer.DeserializeAsync(stream);
    }

    private static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For unit testing")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateEmptyWorkflow(
        WorkflowIdentity identity,
        string name = "Empty Test Workflow",
        string? description = "For unit testing")
    {
        return new Workflow(
            identity,
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateWorkflowWithRunSteps()
    {
        var workflow = CreateEmptyWorkflow("Workflow with Steps");
        
        var agent1 = new FakeAgent("agent-alpha", "Test Agent1");
        var agent2 = new FakeAgent("agent-beta", "Test Agent 2");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }
}