using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Persistence.Validation;

public class WorkflowValidatorTests
{
    [Fact]
    public async Task ValidateAsync_Should_ReturnError_When_WorkflowIdIsMissing()
    {
        // Arrange
        var validator = new WorkflowValidator();
        var document = CreateValidDocument() with
            {
                Identity = new WorkflowIdentity(WorkflowId.Empty, "1.0.0")
            };

        // Act
        var result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            e => e.Code == WorkflowDiagnosticDescriptors.WorkflowIdMissing.Code);
    }
    
     [Fact]
    public async Task ValidateAsync_Should_ReturnError_When_WorkflowVersionIsMissing()
    {
        // Arrange
        var validator = new WorkflowValidator();
        var document = CreateValidDocument() with
        {
            Identity = new WorkflowIdentity(WorkflowId.New(), "")
        };

        // Act
        var result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            e => e.Code == WorkflowDiagnosticDescriptors.WorkflowVersionMissing.Code);
    }

     [Fact]
    public async Task ValidateAsync_Should_ReturnError_When_WorkflowNameIsMissing()
    {
        // Arrange
        var validator = new WorkflowValidator();
        var document = CreateValidDocument() with
        {
            Definition = new WorkflowDefinition("")
        };

        // Act
        var result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            e => e.Code == WorkflowDiagnosticDescriptors.WorkflowNameMissing.Code);
        
    }

     [Fact]
    public async Task ValidateAsync_Should_ReturnError_When_WorkflowHasNoSteps()
    {
        // Arrange
        var validator = new WorkflowValidator();
        var document = CreateValidDocument() with { Steps = [] };

        // Act
        var result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            e => e.Code == WorkflowDiagnosticDescriptors.EmptyWorkflow.Code);
        
    }

    [Fact]
    public async Task ValidateAsync_Should_ReturnAllValidationErrors()
    {
        var validator = new WorkflowValidator();

        var document = CreateValidDocument() with
        {
            Identity = new WorkflowIdentity(
                WorkflowId.Empty,
                string.Empty),

            Definition = new WorkflowDefinition(string.Empty),

            Steps = []
        };

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeFalse();

        result.Errors.Should().HaveCount(4);

        result.Errors.Should().Contain(e =>
            e.Code == WorkflowDiagnosticDescriptors.WorkflowIdMissing.Code);

        result.Errors.Should().Contain(e =>
            e.Code == WorkflowDiagnosticDescriptors.WorkflowVersionMissing.Code);

        result.Errors.Should().Contain(e =>
            e.Code == WorkflowDiagnosticDescriptors.WorkflowNameMissing.Code);

        result.Errors.Should().Contain(e =>
            e.Code == WorkflowDiagnosticDescriptors.EmptyWorkflow.Code);
    }

    [Fact]
    public async Task ValidateAsync_Should_ReturnSuccess_ForValidWorkflow()
    {
       
        // Arrange
        var validator = new WorkflowValidator();
        var mapper = new WorkflowMapper();
        var validWorkflow = CreateValidSampleWorkflow();
        var document = mapper.ToDocument(validWorkflow);

        // Act
        var result = await validator.ValidateAsync(document);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ====================== Helpers ======================

    private static WorkflowDocument CreateValidDocument()
    {
        return new WorkflowDocument
        {
            Schema = WorkflowDocumentSchema.Name,
            SchemaVersion = WorkflowDocumentSchema.Version,
            Identity = WorkflowIdentity.Create("1.0.0"),
            Id = WorkflowStepId.New(),
            Definition = new WorkflowDefinition("Valid Test Workflow"),
            Steps = [CreateSampleRunStepDocument()]
        };
    }

    private static RunStepDocument CreateSampleRunStepDocument()
    {
        return new RunStepDocument
        {
            Id = WorkflowStepId.New(),
            Kind = WorkflowStepKinds.Run,
            Name = "Test Step",
            AgentReference = "agent-alpha"
        };
    }

    private Workflow CreateValidSampleWorkflow()
    {
        var workflow = new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition("Valid Integration Test"));

        var agent = new FakeAgent("agent-alpha", "Test Agent");
        workflow.Add(new RunStep(agent));

        return workflow;
    }
}