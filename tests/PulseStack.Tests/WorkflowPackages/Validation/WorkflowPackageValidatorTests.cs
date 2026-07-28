using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Core.WorkflowPackages.Validation;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.WorkflowPackages.Validation;

public sealed class WorkflowPackageValidatorTests
{
    #region Constructor

    [Fact]
    public void Constructor_ShouldThrow_WhenMapperIsNull()
    {
        // Act
        Action action = () =>
            new WorkflowPackageValidator(null!, new WorkflowValidator());

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("mapper");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenWorkflowValidatorIsNull()
    {
        // Act
        Action action = () =>
            new WorkflowPackageValidator(new WorkflowMapper(), null!);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("workflowValidator");
    }

    #endregion

    #region ValidateAsync

    [Fact]
    public async Task ValidateAsync_ShouldThrow_WhenPackageIsNull()
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        Func<Task> act = () =>
            validator.ValidateAsync(null!).AsTask();

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("package");
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnValidResult_WhenPackageIsValid()
    {
        // Arrange
        var validator = CreateValidator();
        var package = CreateValidPackage();

        // Act
        var result = await validator.ValidateAsync(package);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnError_WhenPackageIdIsMissing()
    {
        // Arrange
        var validator = CreateValidator();
        var package = CreateValidPackage();

        // Force missing package id
        package = package with
        {
            Identity = new WorkflowPackageIdentity(
                WorkflowPackageId.Empty,          // empty id
                package.Identity.Version)
        };

        // Act
        var result = await validator.ValidateAsync(package);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PKG100");
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnError_WhenPackageVersionIsMissing()
    {
        // Arrange
        var validator = CreateValidator();
        var package = CreateValidPackage();

        package = package with
        {
            Identity = new WorkflowPackageIdentity(
                package.Identity.Id,
                "")                               // empty version
        };

        // Act
        var result = await validator.ValidateAsync(package);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PKG101");
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnError_WhenMetadataIsMissing()
    {
        // Arrange
        var validator = CreateValidator();
        var package = CreateValidPackage();

        package = package with
        {
            Metadata = null!
        };

        // Act
        var result = await validator.ValidateAsync(package);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PKG150");
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnWorkflowErrors_WhenWorkflowIsInvalid()
    {
        // Arrange
        var validator = CreateValidator();
        var invalidWorkflow = CreateEmptyWorkflow(); // no steps → WF300
        var package = CreatePackage(invalidWorkflow);

        // Act
        var result = await validator.ValidateAsync(package);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "WF300");
    }

    #endregion

    // ====================== Helpers ======================

    private static WorkflowPackageValidator CreateValidator()
    {
        return new WorkflowPackageValidator(
            new WorkflowMapper(),
            new WorkflowValidator());
    }

    private static WorkflowPackage CreateValidPackage()
    {
        return CreatePackage(CreateValidWorkflow());
    }

    private static WorkflowPackage CreatePackage(Workflow workflow)
    {
        return new WorkflowPackage
        {
            Identity = new WorkflowPackageIdentity(
                WorkflowPackageId.New(),
                "1.0.0"),
            Metadata = new WorkflowPackageMetadata
            {
                Title = "Test Package",
                Description = "Package validation tests"
            },
            Workflow = workflow
        };
    }

    private static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For package validation tests")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    private static Workflow CreateValidWorkflow()
    {
        var workflow = CreateEmptyWorkflow("Valid Package Workflow");

        var agent1 = new FakeAgent("agent-alpha", "Agent Alpha");
        var agent2 = new FakeAgent("agent-beta", "Agent Beta");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }
}
