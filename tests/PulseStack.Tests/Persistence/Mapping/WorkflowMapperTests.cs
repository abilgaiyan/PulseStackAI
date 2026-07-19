using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Mapping;

public class WorkflowMapperTests
{
   

    [Fact]
    public void ToDocument_Should_MapEmptyWorkflow()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var workflow = CreateEmptyWorkflow();

        // Act
        var document = mapper.ToDocument(workflow);

        // Assert
        document.Should().NotBeNull();
        document.Schema.Should().Be(WorkflowDocumentSchema.Name);
        document.SchemaVersion.Should().Be(WorkflowDocumentSchema.Version);
        document.Identity.Should().BeEquivalentTo(workflow.Identity);
        document.Id.Should().Be(workflow.Id);
        document.Definition.Should().BeEquivalentTo(workflow.Definition);
        document.Steps.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToDocument_Should_MapRunStep()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var workflow = CreateWorkflowWithRunSteps();

        // Act
        var document = mapper.ToDocument(workflow);

        // Assert
        document.Should().NotBeNull();
        document.Steps.Should().HaveCount(2);
        document.Steps.Should().AllBeOfType<RunStepDocument>();

        var first = (RunStepDocument)document.Steps[0];
        var second = (RunStepDocument)document.Steps[1];

        first.Should().NotBeNull();
        first!.Kind.Should().Be(WorkflowStepKinds.Run);
        first.AgentReference.Should().NotBeNullOrWhiteSpace();
        first.Name.Should().NotBeNullOrWhiteSpace();
        first.AgentReference.Should().Be("agent-alpha");
        second.AgentReference.Should().Be("agent-beta");

       
        first!.Id.Should().Be(workflow.Steps[0].Id);
        second!.Id.Should().Be(workflow.Steps[1].Id);
    }

    [Fact]
    public void FromDocument_Should_RebuildWorkflow()
    {
        // Arrange
        var mapper = new WorkflowMapper();
        var original = CreateEmptyWorkflow();
        var document = mapper.ToDocument(original);
        var resolver = new FakeAgentResolver();

        // Act
        var reconstructed = mapper.FromDocument(document, resolver);

        // Assert
        reconstructed.Should().NotBeNull();
        reconstructed.Should().NotBeSameAs(original);

        reconstructed.Identity.Should().BeEquivalentTo(original.Identity);
        reconstructed.Definition.Should().BeEquivalentTo(original.Definition);
        reconstructed.Steps.Should().BeEmpty();
        reconstructed.Id.Should().Be(original.Id);
        
    }

    [Fact]
    public void FromDocument_Should_RejectUnknownSchema()
    {
        // Arrange
        var mapper = new WorkflowMapper();

        var document = mapper.ToDocument(CreateEmptyWorkflow());

        document = document with
        {
            Schema = "invalid-schema"
        };

        // Act
        Action act = () => mapper.FromDocument(
            document,
            new FakeAgentResolver());

        // Assert
        act.Should()
        .Throw<NotSupportedException>()
        .WithMessage("*schema*");
        
    }

    [Fact]
    public void FromDocument_Should_RejectUnsupportedSchemaVersion()
    {
         // Arrange
        var mapper = new WorkflowMapper();

        var document = mapper.ToDocument(CreateEmptyWorkflow());

        document = document with
        {
            SchemaVersion = "999.00"
        };

        // Act
        Action act = () => mapper.FromDocument(
            document,
            new FakeAgentResolver());

        // Assert
        act.Should()
        .Throw<NotSupportedException>()
        .WithMessage("*version*");
    }

   [Fact]
    public void FromDocument_Should_ResolveAgentReference()
    {
        // Arrange
        var mapper = new WorkflowMapper();

        var original = CreateWorkflowWithRunSteps();

        var document = mapper.ToDocument(original);

        var resolver = new FakeAgentResolver();

        // Act
        var reconstructed = mapper.FromDocument(document, resolver);

        // Assert
        reconstructed.Steps.Should().HaveCount(2);

        var firstStep = reconstructed.Steps[0].Should().BeOfType<RunStep>().Subject;
        firstStep.Agent.Should().NotBeNull();
        firstStep.Agent.Name.Should().Be("agent-alpha");

        var secondStep = reconstructed.Steps[1].Should().BeOfType<RunStep>().Subject;
        secondStep.Agent.Name.Should().Be("agent-beta");
    }

    // ====================== Helpers ======================

    private static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For unit testing")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateWorkflowWithRunSteps()
    {
        var workflow = CreateEmptyWorkflow();

        var agent1 = new FakeAgent("agent-alpha", "Test Agent1");
        var agent2 = new FakeAgent("agent-beta", "Test Agent 2");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }
}
