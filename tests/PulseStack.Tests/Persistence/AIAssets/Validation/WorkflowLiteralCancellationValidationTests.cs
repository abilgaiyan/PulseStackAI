using System.Collections;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowLiteralCancellationValidationTests
{
    [Fact]
    public void ValidateLiteral_ShouldHonorCancellationBetweenNestedArrayItems()
    {
        using var source = new CancellationTokenSource();
        var errors = new CancellingErrorCollection(source);
        var literal = new ArrayWorkflowLiteralDocument(
            [
                new StringWorkflowLiteralDocument(null!),
                new StringWorkflowLiteralDocument(null!)
            ]);

        var validatorType = typeof(AIAssetDocumentValidator).Assembly.GetType(
            "PulseStack.Core.Persistence.AIAssets.Validation.WorkflowDocumentStructuralValidator",
            throwOnError: true)!;
        var validateLiteral = validatorType.GetMethod(
            "ValidateLiteral",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var act = () => validateLiteral.Invoke(
            null,
            [literal, "$.literal", errors, source.Token]);

        var exception = act.Should().Throw<TargetInvocationException>().Which;
        exception.InnerException.Should().BeOfType<OperationCanceledException>();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new AIAssetDocumentValidationError(
                AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue,
                "Workflow string literal value is required.",
                "$.literal.items[0].value"));
    }

    private sealed class CancellingErrorCollection : ICollection<AIAssetDocumentValidationError>
    {
        private readonly List<AIAssetDocumentValidationError> inner = [];
        private readonly CancellationTokenSource source;

        public CancellingErrorCollection(CancellationTokenSource source)
        {
            this.source = source;
        }

        public int Count => inner.Count;

        public bool IsReadOnly => false;

        public void Add(AIAssetDocumentValidationError item)
        {
            inner.Add(item);
            source.Cancel();
        }

        public void Clear() => inner.Clear();

        public bool Contains(AIAssetDocumentValidationError item) => inner.Contains(item);

        public void CopyTo(AIAssetDocumentValidationError[] array, int arrayIndex)
            => inner.CopyTo(array, arrayIndex);

        public IEnumerator<AIAssetDocumentValidationError> GetEnumerator()
            => inner.GetEnumerator();

        public bool Remove(AIAssetDocumentValidationError item)
            => inner.Remove(item);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
