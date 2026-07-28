using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using  PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.WorkflowPackages;

public class WorkflowPackageTests
{
    [Fact]
    public void ShouldCreateWorkflowPackage()
    {
        // Arrange
        var identity = new WorkflowPackageIdentity(WorkflowPackageId.New());
        var metadata = new WorkflowPackageMetadata();
        var workflow = CreateWorkflowWithRunSteps();
        // Act
        var package = new WorkflowPackage
        {
            Identity = identity,
            Metadata = metadata,
            Workflow = workflow
        };

        // Assert
        package.Identity.Should().Be(identity);

        package.Metadata.Should().Be(metadata);

        package.Workflow.Should().Be(workflow);
       
    }

    [Fact]
    public void ShouldBeEqual_WhenAllPropertiesAreEqual() 
    {
        // Arrange
        var identity = new WorkflowPackageIdentity(WorkflowPackageId.New());
        var metadata = new WorkflowPackageMetadata { Title = "Test" };
        var workflow = CreateWorkflowWithRunSteps();

        var package1 = new WorkflowPackage
        {
            Identity = identity,
            Metadata = metadata,
            Workflow = workflow
        };

        var package2 = new WorkflowPackage
        {
            Identity = identity,
            Metadata = metadata,
            Workflow = workflow
        };

        // Assert - Record equality (value-based)
        package1.Should().Be(package2);
        package1.Should().BeEquivalentTo(package2);
        package1.GetHashCode().Should().Be(package2.GetHashCode());
    }

    [Fact]
    public void ShouldNotBeEqual_WhenAnyPropertyDiffers()
    {
        // Arrange
        var identity = new WorkflowPackageIdentity(WorkflowPackageId.New());
        var workflow = CreateWorkflowWithRunSteps();

        var package1 = new WorkflowPackage
        {
            Identity = identity,
            Metadata = new WorkflowPackageMetadata { Title = "Package A" },
            Workflow = workflow
        };

        var package2 = new WorkflowPackage
        {
            Identity = identity,
            Metadata = new WorkflowPackageMetadata { Title = "Package B" }, // Different
            Workflow = workflow
        };

        // Assert
        package1.Should().NotBe(package2);
        package1.GetHashCode().Should().NotBe(package2.GetHashCode());
    }

    [Fact]
    public void ShouldContainExactlyOneWorkflow()
    {
        // Arrange
        var identity = new WorkflowPackageIdentity(WorkflowPackageId.New());
        var workflow = CreateWorkflowWithRunSteps();

        var package = new WorkflowPackage
        {
            Identity = identity,
            Metadata = new WorkflowPackageMetadata { Title = "Package A" },
            Workflow = workflow
        };

        package.Workflow.Should().NotBeNull();
        package.Workflow.Should().BeSameAs(workflow);
        package.Workflow.Steps.Should().HaveCount(2);
        
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