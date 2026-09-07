using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class WorkflowDocumentDiscriminatorMatrixTests
{
    [Fact]
    public void StepDocumentMatrix_ShouldCoverEveryDeclaredDiscriminatorExactlyOnce()
    {
        var matrix = new (Type ConcreteType, WorkflowStepDocumentKind Kind)[]
        {
            (typeof(RunStepDocument), WorkflowStepDocumentKind.Run),
            (typeof(ParallelStepDocument), WorkflowStepDocumentKind.Parallel),
            (typeof(ConditionalStepDocument), WorkflowStepDocumentKind.Conditional),
            (typeof(RetryStepDocument), WorkflowStepDocumentKind.Retry),
            (typeof(LoopStepDocument), WorkflowStepDocumentKind.Loop),
            (typeof(SwitchStepDocument), WorkflowStepDocumentKind.Switch)
        };

        matrix.Select(entry => entry.ConcreteType).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().Equal(Enum.GetValues<WorkflowStepDocumentKind>());
    }

    [Fact]
    public void ConditionDocumentMatrix_ShouldCoverEveryDeclaredDiscriminatorExactlyOnce()
    {
        var matrix = new (Type ConcreteType, WorkflowConditionDocumentKind Kind)[]
        {
            (typeof(NamedConditionDocument), WorkflowConditionDocumentKind.Named)
        };

        matrix.Select(entry => entry.ConcreteType).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().Equal(Enum.GetValues<WorkflowConditionDocumentKind>());
    }

    [Fact]
    public void ValueDocumentMatrix_ShouldCoverEveryDeclaredDiscriminatorExactlyOnce()
    {
        var matrix = new (Type ConcreteType, WorkflowValueDocumentKind Kind)[]
        {
            (typeof(InputValueDocument), WorkflowValueDocumentKind.Input),
            (typeof(CurrentOutputValueDocument), WorkflowValueDocumentKind.CurrentOutput),
            (typeof(ContextItemValueDocument), WorkflowValueDocumentKind.ContextItem),
            (typeof(LiteralValueDocument), WorkflowValueDocumentKind.Literal)
        };

        matrix.Select(entry => entry.ConcreteType).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().Equal(Enum.GetValues<WorkflowValueDocumentKind>());
    }

    [Fact]
    public void LiteralDocumentMatrix_ShouldCoverEveryDeclaredDiscriminatorExactlyOnce()
    {
        var matrix = new (Type ConcreteType, WorkflowLiteralDocumentKind Kind)[]
        {
            (typeof(NullWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Null),
            (typeof(StringWorkflowLiteralDocument), WorkflowLiteralDocumentKind.String),
            (typeof(BooleanWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Boolean),
            (typeof(IntegerWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Integer),
            (typeof(DecimalWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Decimal),
            (typeof(ArrayWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Array),
            (typeof(ObjectWorkflowLiteralDocument), WorkflowLiteralDocumentKind.Object)
        };

        matrix.Select(entry => entry.ConcreteType).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().OnlyHaveUniqueItems();
        matrix.Select(entry => entry.Kind).Should().Equal(Enum.GetValues<WorkflowLiteralDocumentKind>());
    }
}
