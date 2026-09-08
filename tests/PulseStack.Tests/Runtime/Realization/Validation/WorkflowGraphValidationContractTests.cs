using System.Collections;
using FluentAssertions;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Validation;

public sealed class WorkflowGraphValidationContractTests
{
    [Fact]
    public void DiagnosticCodes_ShouldMatchFrozenVocabulary()
    {
        WorkflowGraphValidationCodes.AgentDefinitionUnavailable.Should().Be("WFG001");
        WorkflowGraphValidationCodes.AgentReferenceUrnConflict.Should().Be("WFG002");
        WorkflowGraphValidationCodes.CatalogDefinitionMismatch.Should().Be("WFG003");
        WorkflowGraphValidationCodes.AgentGraphInvalid.Should().Be("WFG004");
        WorkflowGraphValidationCodes.ConditionBindingUnavailable.Should().Be("WFG005");
    }

    [Fact]
    public void Success_ShouldBeValidAndContainNoErrors()
    {
        var result = WorkflowGraphValidationResult.Success();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldBeInvalidAndPreserveErrorOrder()
    {
        var first = Error("WFG001", "first", "$.steps[0].agent");
        var second = Error("WFG005", "second", "$.steps[1].condition.name");

        var result = WorkflowGraphValidationResult.Failure(first, second);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Equal(first, second);
    }

    [Fact]
    public void Result_ShouldMaterializeInputEnumerationExactlyOnce()
    {
        var source = new SingleEnumerationSequence<WorkflowGraphValidationError>(
            [Error("WFG001", "missing", "$.steps[0].agent")]);

        var result = new WorkflowGraphValidationResult(source);

        result.Errors.Should().ContainSingle();
        source.EnumerationCount.Should().Be(1);
    }

    [Fact]
    public void Result_ShouldSnapshotSourceAndExposeReadOnlyErrors()
    {
        var source = new List<WorkflowGraphValidationError>
        {
            Error("WFG001", "missing", "$.steps[0].agent")
        };

        var result = new WorkflowGraphValidationResult(source);
        source.Add(Error("WFG005", "condition", "$.steps[1].condition.name"));

        result.Errors.Should().ContainSingle();
        var list = result.Errors.Should().BeAssignableTo<IList<WorkflowGraphValidationError>>().Subject;
        var mutate = () => list.Add(Error("WFG003", "mismatch", "$.steps[2].agent"));
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Error_ShouldSnapshotNestedAgentErrorsAndPreserveAggDiagnostics()
    {
        var nested = new List<AgentGraphValidationError>
        {
            new("AGG003", "definition unavailable", "$.model")
        };

        var error = new WorkflowGraphValidationError(
            WorkflowGraphValidationCodes.AgentGraphInvalid,
            "Agent graph is not ready.",
            "$.steps[0].agent",
            nested);
        nested.Add(new AgentGraphValidationError("AGG005", "urn mismatch", "$.tools[0]"));

        error.AgentErrors.Should().ContainSingle();
        error.AgentErrors[0].Code.Should().Be("AGG003");
        error.AgentErrors[0].Message.Should().Be("definition unavailable");
        error.AgentErrors[0].Path.Should().Be("$.model");
    }

    [Fact]
    public void Error_ShouldMaterializeNestedAgentEnumerationExactlyOnce()
    {
        var source = new SingleEnumerationSequence<AgentGraphValidationError>(
            [new AgentGraphValidationError("AGG001", "missing model", "$.model")]);

        var error = new WorkflowGraphValidationError(
            WorkflowGraphValidationCodes.AgentGraphInvalid,
            "Agent graph is not ready.",
            "$.steps[0].agent",
            source);

        error.AgentErrors.Should().ContainSingle();
        source.EnumerationCount.Should().Be(1);
    }

    [Fact]
    public void Error_ShouldExposeNestedAgentErrorsAsReadOnlySnapshot()
    {
        var error = new WorkflowGraphValidationError(
            WorkflowGraphValidationCodes.AgentGraphInvalid,
            "Agent graph is not ready.",
            "$.steps[0].agent",
            [new AgentGraphValidationError("AGG001", "missing model", "$.model")]);

        var list = error.AgentErrors.Should().BeAssignableTo<IList<AgentGraphValidationError>>().Subject;
        var mutate = () => list.Add(new AgentGraphValidationError("AGG002", "wrong type", "$.model"));

        mutate.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(WorkflowGraphValidationCodes.AgentDefinitionUnavailable)]
    [InlineData(WorkflowGraphValidationCodes.AgentReferenceUrnConflict)]
    [InlineData(WorkflowGraphValidationCodes.CatalogDefinitionMismatch)]
    [InlineData(WorkflowGraphValidationCodes.ConditionBindingUnavailable)]
    public void NonAgentGraphDiagnostics_ShouldHaveCanonicalEmptyAgentDetails(string code)
    {
        var error = Error(code, "diagnostic", "$.steps[0]");

        error.AgentErrors.Should().BeEmpty();
    }

    [Fact]
    public void Contracts_ShouldLiveInAbstractionsAssemblyOnly()
    {
        var expectedAssembly = typeof(AgentGraphValidationError).Assembly;

        typeof(IWorkflowGraphValidator).Assembly.Should().BeSameAs(expectedAssembly);
        typeof(WorkflowGraphValidationCodes).Assembly.Should().BeSameAs(expectedAssembly);
        typeof(WorkflowGraphValidationError).Assembly.Should().BeSameAs(expectedAssembly);
        typeof(WorkflowGraphValidationResult).Assembly.Should().BeSameAs(expectedAssembly);
        expectedAssembly.GetName().Name.Should().Be("PulseStack.Abstractions");
    }

    private static WorkflowGraphValidationError Error(
        string code,
        string message,
        string path)
        => new(code, message, path);

    private sealed class SingleEnumerationSequence<T>(IReadOnlyList<T> items) : IEnumerable<T>
    {
        private int enumerationCount;

        public int EnumerationCount => enumerationCount;

        public IEnumerator<T> GetEnumerator()
        {
            if (Interlocked.Increment(ref enumerationCount) != 1)
            {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
